$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
Set-Location $repo
. (Join-Path $PSScriptRoot 'advertified-compose.ps1')
Assert-AdvertifiedComposeProject -RequireExisting

$manifestPath = Join-Path $repo 'artifacts/inventory-corpus/source-manifest.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.documentCount -ne 43 -or $manifest.documents.Count -ne 43) {
    throw 'The governed corpus manifest must contain exactly 43 source files.'
}

$minio = @(& docker ps -q `
    --filter 'label=com.docker.compose.project=advertified-os2-dev' `
    --filter 'label=com.docker.compose.service=minio')
if ($LASTEXITCODE -ne 0 -or $minio.Count -ne 1) {
    throw 'Expected exactly one advertified-os2-dev MinIO container.'
}
if (-not (Get-Command aws -ErrorAction SilentlyContinue)) {
    throw 'AWS CLI is required to read the local S3-compatible OS2 object store.'
}

$containerEnvironment = @(& docker inspect --format `
    '{{range .Config.Env}}{{println .}}{{end}}' $minio[0])
if ($LASTEXITCODE -ne 0) {
    throw 'Unable to read the existing OS2 MinIO service configuration.'
}
$rootUser = ($containerEnvironment |
    Where-Object { $_ -like 'MINIO_ROOT_USER=*' } |
    Select-Object -First 1) -replace '^MINIO_ROOT_USER=', ''
$rootPassword = ($containerEnvironment |
    Where-Object { $_ -like 'MINIO_ROOT_PASSWORD=*' } |
    Select-Object -First 1) -replace '^MINIO_ROOT_PASSWORD=', ''
if (-not $rootUser -or -not $rootPassword) {
    throw 'The running OS2 MinIO credentials could not be resolved.'
}

$previousAccessKey = $env:AWS_ACCESS_KEY_ID
$previousSecretKey = $env:AWS_SECRET_ACCESS_KEY
$previousRegion = $env:AWS_DEFAULT_REGION
$env:AWS_ACCESS_KEY_ID = $rootUser
$env:AWS_SECRET_ACCESS_KEY = $rootPassword
$env:AWS_DEFAULT_REGION = 'us-east-1'

$tenantKey = '10000000000000000000000000000020'
$outputRoot = Join-Path $repo 'artifacts/inventory-corpus/physical-sources'
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$results = @()
try {
    foreach ($document in $manifest.documents) {
        $hash = [string]$document.sha256
        $fileName = [string]$document.relativePath
        $expectedBytes = [int64]$document.bytes
        $destination = Join-Path $outputRoot $fileName
        $existingMatches = $false
        if (Test-Path -LiteralPath $destination) {
            $existing = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant()
            $existingMatches = $existing -eq $hash
        }
        if (-not $existingMatches) {
            $key = "protected/$tenantKey/$hash"
            & aws --endpoint-url http://127.0.0.1:59000 `
                s3api get-object `
                --bucket advertified-inventory `
                --key $key `
                $destination | Out-Null
            if ($LASTEXITCODE -ne 0) {
                throw "Unable to read protected OS2 source $fileName."
            }
        }
        $actualHash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant()
        $actualBytes = (Get-Item -LiteralPath $destination).Length
        if ($actualHash -ne $hash) {
            throw "Materialized source hash mismatch for $fileName."
        }
        if ($actualBytes -ne $expectedBytes) {
            throw "Materialized source size mismatch for ${fileName}: expected $expectedBytes; actual $actualBytes."
        }
        $results += [pscustomobject]@{
            fileName = $fileName
            sourceHash = $hash
            bytes = $actualBytes
            verified = $true
            path = (Resolve-Path -LiteralPath $destination).Path
        }
        Write-Host "Verified physical source: $fileName"
    }
}
finally {
    $env:AWS_ACCESS_KEY_ID = $previousAccessKey
    $env:AWS_SECRET_ACCESS_KEY = $previousSecretKey
    $env:AWS_DEFAULT_REGION = $previousRegion
}

$resultsPath = Join-Path $outputRoot 'materialization-register.json'
$results | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $resultsPath -Encoding utf8
Write-Host "Materialized and SHA-256 verified $($results.Count)/43 physical source files."
Write-Host "Register: $resultsPath"

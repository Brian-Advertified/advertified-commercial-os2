param(
    [string]$AwsProfile = $env:ADVERTIFIED_AWS_PROFILE
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $repo

Write-Host 'Verifying the 43-file physical certification gate...'
python .\tools\assert_physical_corpus_certified.py
if ($LASTEXITCODE -ne 0) {
    throw 'Bedrock activation is prohibited until all 43 physical files pass.'
}

$projects = @(
    docker ps -a --format '{{.Label "com.docker.compose.project"}}' |
        Where-Object { $_ -and $_ -like 'advertified*' } |
        Sort-Object -Unique
)
if ($projects.Count -ne 1 -or $projects[0] -ne 'advertified-os2-dev') {
    throw "Expected only advertified-os2-dev; found: $($projects -join ', ')"
}

if (-not $AwsProfile) {
    $profiles = @(aws configure list-profiles 2>$null)
    $preferred = @('default', 'advertified', 'AdvertifiedBedrock', 'AdvertifiedCodexAudit')
    $candidates = @($preferred + $profiles | Select-Object -Unique)
    foreach ($candidate in $candidates) {
        if (-not $candidate -or $profiles -notcontains $candidate) { continue }
        aws sts get-caller-identity --profile $candidate --region us-east-1 *> $null
        if ($LASTEXITCODE -ne 0) { continue }
        aws bedrock list-foundation-models --profile $candidate --region us-east-1 *> $null
        if ($LASTEXITCODE -eq 0) {
            $AwsProfile = $candidate
            break
        }
    }
}
if (-not $AwsProfile) {
    throw 'No authenticated AWS profile with Bedrock control-plane access was found.'
}
$env:ADVERTIFIED_AWS_PROFILE = $AwsProfile

$apiContainer = 'advertified-os2-dev-api-1'
$configCsv = docker inspect --format '{{ index .Config.Labels "com.docker.compose.project.config_files" }}' $apiContainer
if ($LASTEXITCODE -ne 0 -or -not $configCsv) {
    throw 'Could not read the active OS2 Compose configuration.'
}
$configFiles = @($configCsv -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
$corpusOverride = (Resolve-Path '.\artifacts\inventory-corpus\docker-compose.override.yml').Path
if ($configFiles -notcontains $corpusOverride) {
    $configFiles += $corpusOverride
}
$composeArgs = @('-p', 'advertified-os2-dev')
foreach ($file in $configFiles) {
    $composeArgs += @('-f', $file)
}

Write-Host 'Validating the single-project Bedrock configuration...'
& docker compose @composeArgs config --quiet
if ($LASTEXITCODE -ne 0) { throw 'Compose validation failed.' }

Write-Host 'Recreating only the existing OS2 runtime and API with governed Bedrock enabled...'
& docker compose @composeArgs up -d --no-deps --force-recreate agent-runtime api
if ($LASTEXITCODE -ne 0) { throw 'OS2 runtime/API recreation failed.' }

$deadline = (Get-Date).AddMinutes(5)
do {
    Start-Sleep -Seconds 3
    $runtimeHealth = docker inspect --format '{{.State.Health.Status}}' advertified-os2-dev-agent-runtime-1 2>$null
    $apiHealth = docker inspect --format '{{.State.Health.Status}}' advertified-os2-dev-api-1 2>$null
} while (($runtimeHealth -ne 'healthy' -or $apiHealth -ne 'healthy') -and (Get-Date) -lt $deadline)
if ($runtimeHealth -ne 'healthy' -or $apiHealth -ne 'healthy') {
    throw "OS2 did not become healthy. runtime=$runtimeHealth api=$apiHealth"
}

python .\tools\verify_bedrock_corpus_preflight.py
if ($LASTEXITCODE -ne 0) {
    throw 'The governed Bedrock preflight did not pass.'
}

Write-Host "OS2 Bedrock corpus mode activated with AWS profile '$AwsProfile'."
Write-Host 'No model inference has been executed by this activation script.'

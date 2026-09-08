param(
    [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$Filter,
    [string]$IntegrationFilter,
    [switch]$SkipBuild
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$validationImage = 'advertified/api-validation:local'
$validationBuilder = (& docker context show).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Unable to resolve the active Docker context.' }
$validationCacheBudget = '1GB'
if ($SkipBuild -and -not $IntegrationFilter) {
    throw 'SkipBuild is restricted to a selected integration rerun.'
}
# Explicit Docker-pinned validation only: never kill host processes or launch a stack.
# The build is socket-free and reuses Docker's bounded local cache so the pinned
# SDK is not downloaded into a new VHD allocation on every validation run.
# An explicitly selected integration run below uses disposable Testcontainers
# databases, never the application's running database.
try {
    if (-not $SkipBuild) {
        $buildArguments = @(
            'buildx', 'build', '--builder', $validationBuilder,
            '--file', (Join-Path $repoRoot 'api/Dockerfile'),
            '--target', 'tests', '--build-arg', "TEST_FILTER=$Filter",
            '--progress', 'plain'
        )
        if ($IntegrationFilter) {
            $buildArguments += @('--tag', $validationImage, '--load')
        }
        else {
            $buildArguments += @('--output', 'type=cacheonly')
        }
        $buildArguments += $repoRoot
        & docker @buildArguments
        if ($LASTEXITCODE -ne 0) {
            throw "Docker-pinned API validation failed with exit code $LASTEXITCODE."
        }
    }
    if ($IntegrationFilter) {
        & docker run --rm --mount type=bind,source=/var/run/docker.sock,target=/var/run/docker.sock `
            --add-host host.docker.internal:host-gateway -e TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal `
            $validationImage dotnet test `
            api/tests/Advertified.Commercial.Api.Tests/Advertified.Commercial.Api.Tests.csproj `
            --configuration Release --no-build --no-restore --filter $IntegrationFilter --logger 'console;verbosity=normal'
        if ($LASTEXITCODE -ne 0) {
            throw "Disposable API integration validation failed with exit code $LASTEXITCODE."
        }
    }
}
finally {
    $ErrorActionPreference = 'Continue'
    if ($IntegrationFilter) {
        & docker image rm $validationImage *> $null
    }
    if (-not $SkipBuild) {
        & docker builder prune --force --max-used-space $validationCacheBudget --reserved-space 256MB *> $null
    }
}

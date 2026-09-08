$ErrorActionPreference = "Stop"

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$webRoot = Join-Path $repositoryRoot "web"
$composeBase = Join-Path $repositoryRoot "infrastructure/docker-compose.yml"
$composeApp = Join-Path $repositoryRoot "infrastructure/docker-compose.app.yml"
$composeProject = "advertified-os2-dev"
$playwrightConfig = "playwright.session-durability.config.ts"

function Invoke-PlaywrightStep {
    param([Parameter(Mandatory = $true)][string]$Spec)

    Push-Location $webRoot
    try {
        & npx.cmd playwright test $Spec "--config=$playwrightConfig"
        if ($LASTEXITCODE -ne 0) {
            throw "Browser session durability step failed: $Spec"
        }
    }
    finally {
        Pop-Location
    }
}

function Restart-CommercialApi {
    $composeArgs = @(
        "compose",
        "--project-name", $composeProject,
        "--file", $composeBase,
        "--file", $composeApp,
        "restart", "api"
    )
    & docker @composeArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Commercial API restart failed."
    }

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(60)
    do {
        try {
            $response = Invoke-WebRequest -Uri "http://127.0.0.1:5097/health/ready" -UseBasicParsing -TimeoutSec 3
            if ($response.StatusCode -eq 200) {
                return
            }
        }
        catch {
            Start-Sleep -Seconds 1
        }
    } while ([DateTimeOffset]::UtcNow -lt $deadline)

    throw "Commercial API did not become ready within 60 seconds."
}

Invoke-PlaywrightStep "e2e/session-durability.seed.spec.ts"
Restart-CommercialApi
Invoke-PlaywrightStep "e2e/session-durability.verify.spec.ts"
Restart-CommercialApi
Invoke-PlaywrightStep "e2e/session-durability.revoked.spec.ts"

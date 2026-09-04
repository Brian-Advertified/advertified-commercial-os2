$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
Set-Location $repo
. (Join-Path $PSScriptRoot 'advertified-compose.ps1')

$composeFiles = @(
    'infrastructure/docker-compose.yml',
    'infrastructure/docker-compose.app.yml',
    'artifacts/inventory-corpus/docker-compose.override.yml'
)

Assert-AdvertifiedComposeProject -RequireExisting

$runtimeMode = Invoke-AdvertifiedCompose $composeFiles @(
    'exec', '-T', 'agent-runtime',
    'python', '-c', "import os; print(os.getenv('ADVERTIFIED_AGENT_RUNTIME_MODE', ''))"
)
if ($LASTEXITCODE -ne 0 -or $runtimeMode.Trim() -ne 'deterministic') {
    throw 'The OS2 agent runtime is not in deterministic zero-cost mode.'
}

Write-Host 'Building and replacing only the OS2 API...'
Invoke-AdvertifiedCompose $composeFiles @('build', 'api')
Invoke-AdvertifiedCompose $composeFiles @('up', '-d', '--no-build', '--no-deps', 'api')
Wait-AdvertifiedService $composeFiles 'api'

Write-Host 'Running the DMS retained-workbook repair through local Docling only...'
& python .\tools\repair_dms_local.py
if ($LASTEXITCODE -ne 0) {
    throw 'The DMS local reprojection did not pass physical-file gold.'
}

Assert-AdvertifiedComposeProject -RequireExisting

Write-Host ''
Write-Host 'DMS repair finished. Bedrock remained disabled and nothing was published.'
Invoke-AdvertifiedCompose $composeFiles @('ps')
& docker system df

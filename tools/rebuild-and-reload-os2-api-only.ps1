param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('BuildApi', 'RestartApi', 'BuildMigrator', 'ApplyMigrations')]
    [string]$Action,
    [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string[]]$ComposeFiles
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'advertified-compose.ps1')
Push-Location $repoRoot
try {
    Assert-AdvertifiedComposeProject -RequireExisting
    # Each invocation performs exactly the requested action. Migration, restart and
    # build authority are separate. No pruning, cleanup, seed or provider calls.
    switch ($Action) {
        'BuildApi' { Invoke-AdvertifiedCompose $ComposeFiles @('build', 'api') }
        'BuildMigrator' { Invoke-AdvertifiedCompose $ComposeFiles @('build', 'migrator') }
        'RestartApi' {
            Invoke-AdvertifiedCompose $ComposeFiles @('up', '-d', '--no-build', '--no-deps', '--force-recreate', 'api')
            Wait-AdvertifiedService $ComposeFiles 'api'
        }
        'ApplyMigrations' {
            Invoke-AdvertifiedCompose $ComposeFiles @('up', '--no-build', '--no-deps', '--force-recreate',
                '--abort-on-container-exit', '--exit-code-from', 'migrator', 'migrator')
        }
    }
}
finally { Pop-Location }

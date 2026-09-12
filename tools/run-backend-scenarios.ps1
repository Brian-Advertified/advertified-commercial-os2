param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-z0-9][a-z0-9-]{2,55}$')]
    [string]$EvidenceName
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$scenarioRepo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$scenarioApiName = "$EvidenceName-api"
$scenarioRoot = Join-Path $scenarioRepo 'artifacts/backend-production-completion'
foreach ($name in @($EvidenceName, $scenarioApiName)) {
    if (Test-Path -LiteralPath (Join-Path $scenarioRoot $name)) {
        throw 'Use a new evidence name; existing execution evidence is retained.'
    }
}
& python -B (Join-Path $PSScriptRoot 'generate_backend_scenario_catalog.py') --check
if ($LASTEXITCODE -ne 0) { throw 'The C# catalogue does not match its canonical source.' }
$scenarioApiFailed = $false
try {
    $scenarioFilter = 'FullyQualifiedName~CanonicalOpportunityScenario|FullyQualifiedName~CanonicalInventoryScenario|FullyQualifiedName~CanonicalPlanningScenario|FullyQualifiedName~CanonicalTransactionScenario|FullyQualifiedName~CanonicalFundingScenario|FullyQualifiedName~CanonicalMeasurementScenario|FullyQualifiedName~ManualFundingRouteRetainsIndependentHumanReconciliation|FullyQualifiedName~ManualPartnerFundingMigrationTests'
    & (Join-Path $PSScriptRoot 'run-api-memory-tests.ps1') -Integration -PartitionByCategory -Filter $scenarioFilter -EvidenceName $scenarioApiName
}
catch {
    $scenarioApiFailed = $true
    Write-Warning "API execution failed: $($_.Exception.Message)"
}
# Failed or missing API executions remain failed in the consolidated catalogue.
# This invokes deterministic ASGI fixtures only, never a paid provider.
Push-Location (Join-Path $scenarioRepo 'agent-runtime')
try {
    & python -B -m business_scenarios.backend_scenario_executor --evidence-name $EvidenceName --api-evidence (Join-Path $scenarioRoot $scenarioApiName)
    $scenarioExitCode = $LASTEXITCODE
}
finally { Pop-Location }
if ($scenarioApiFailed -or $scenarioExitCode -ne 0) {
    throw "The 100-case campaign is incomplete or failed. Review $scenarioRoot/$EvidenceName/summary.json."
}

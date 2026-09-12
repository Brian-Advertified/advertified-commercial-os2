param(
    [Parameter(Mandatory = $true)][string]$PriorCostReceipt,
    [Parameter(Mandatory = $true)][ValidatePattern('^[a-z0-9-]{1,64}$')][string]$EvidenceName,
    [Parameter(Mandatory = $true)][ValidateRange(1, 500)][int]$MaximumCalls,
    [ValidateRange(1, 500)][int]$CostCapMinor = 2,
    [ValidateSet('amazon.nova-lite-v1:0', 'amazon.nova-pro-v1:0')][string]$Model = 'amazon.nova-lite-v1:0',
    [Parameter(Mandatory = $true)][string[]]$Tests,
    [switch]$ValidateOnly
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$evidenceRoot = Join-Path $repoRoot "artifacts/backend-production-completion/$EvidenceName"
$container = 'advertified-os2-dev-postgres-1'
$tenant = [Guid]'11111111-1111-1111-1111-111111111111'
$maximumMicros = [long]$MaximumCalls * $CostCapMinor * 10000
if ($maximumMicros -gt 5000000) { throw 'Requested calls exceed the existing aggregate owner ceiling.' }

function Resolve-RepositoryFile([string]$Relative) {
    if ([IO.Path]::IsPathRooted($Relative)) { throw 'Use a repository-relative evidence path.' }
    $resolved = (Resolve-Path -LiteralPath (Join-Path $repoRoot $Relative)).Path
    if (-not $resolved.StartsWith($repoRoot + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase)) { throw 'Evidence must remain inside this repository.' }
    return $resolved
}

# Historical direct calls bypassed the API ledger. Unknown is never reset to zero.
$priorPath = Resolve-RepositoryFile $PriorCostReceipt
$prior = Get-Content -LiteralPath $priorPath -Raw | ConvertFrom-Json
if ($prior.schemaVersion -ne 'advertified.prior-live-cost-reconciliation.v1') {
    throw 'A reconciled prior-live-cost receipt is required.'
}
$priorRun = [Guid]$prior.reconciliationId
$priorStep = [Guid]$prior.reconciliationStepId
if ($null -eq $prior.unreservedMaximumCostUsdMicros -or
    ($prior.unreservedMaximumCostUsdMicros -isnot [int] -and
     $prior.unreservedMaximumCostUsdMicros -isnot [long])) {
    throw 'Historical unreserved spending is unknown; live invocation is blocked.'
}
$priorMaximum = [long]$prior.unreservedMaximumCostUsdMicros
if ($priorMaximum -lt 0 -or $priorMaximum -gt 5000000 -or
    $priorMaximum -ne $prior.unreservedMaximumCostUsdMicros) {
    throw 'Historical maximum cost must be an integer within the owner ceiling.'
}
$through = [DateTimeOffset]::Parse($prior.reconciledThroughUtc)
if ($through -gt [DateTimeOffset]::UtcNow -or $through -lt [DateTimeOffset]'2026-09-12T00:00:00Z') {
    throw 'Reconciliation must cover the handover live calls and cannot be future dated.'
}
if (@($prior.sourceReceipts).Count -eq 0) { throw 'Prior cost reconciliation has no supporting evidence.' }
foreach ($source in $prior.sourceReceipts) {
    $sourcePath = Resolve-RepositoryFile $source.path
    if ((Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash -ne $source.sha256) {
        throw 'Historical cost evidence hash mismatch.'
    }
}
foreach ($test in $Tests) {
    if ($test -notmatch '^business_scenarios/test_[a-z0-9_]+_live\.py(?:::[a-z0-9_]+(?:\[[a-z0-9_-]+\])?)?$') {
        throw 'Only explicit live business-scenario test files and exact cases are accepted.'
    }
    $testFile = ($test -split '::')[0]
    $null = Resolve-RepositoryFile ("agent-runtime/" + $testFile)
}
if (Test-Path -LiteralPath $evidenceRoot) { throw 'Evidence names must be new; permits cannot be replayed.' }
if ($ValidateOnly) {
    Write-Output 'Validated input only. No budget reserved and no provider called.'
    exit 0
}

$labelsJson = & docker inspect --format '{{json .Config.Labels}}' $container
if ($LASTEXITCODE -ne 0) { throw 'The development database could not be inspected.' }
$labels = $labelsJson | ConvertFrom-Json
if ($labels.'com.docker.compose.project' -ne 'advertified-os2-dev') {
    throw 'The existing development database could not be verified.'
}
$run = [Guid]::NewGuid()
$step = [Guid]::NewGuid()
# One atomic transaction reuses the canonical C# migration-owned reservation function.
# Existing history holds are reused only when their exact amount agrees.
$sql = @'
BEGIN;
SELECT pg_advisory_xact_lock(hashtextextended('advertified:ai:owner-budget', 0));
DO $guard$
BEGIN
    IF EXISTS (SELECT 1 FROM governance.ai_monthly_budget_ledger
        WHERE run_id = '{0}' AND step_id = '{1}'
          AND maximum_cost_usd_micros <> {2}) THEN
        RAISE EXCEPTION 'Historical reservation differs from reconciliation';
    END IF;
    IF {2} > 0 AND NOT EXISTS (
        SELECT 1 FROM governance.ai_monthly_budget_ledger
        WHERE run_id = '{0}' AND step_id = '{1}') THEN
        IF NOT governance.reserve_ai_monthly_budget(
            date_trunc('month', CURRENT_DATE)::date, '{0}', '{1}', '{3}', {2}) THEN
            RAISE EXCEPTION 'Historical spend cannot be reserved within the existing ceiling';
        END IF;
    END IF;
    IF NOT governance.reserve_ai_monthly_budget(
        date_trunc('month', CURRENT_DATE)::date, '{4}', '{5}', '{3}', {6}) THEN
        RAISE EXCEPTION 'Shared owner budget cannot fund these calls';
    END IF;
END
$guard$;
COMMIT;
'@ -f $priorRun, $priorStep, $priorMaximum, $tenant, $run, $step, $maximumMicros
$sql | & docker exec -i $container psql -U advertified -d advertified -v ON_ERROR_STOP=1 -q
if ($LASTEXITCODE -ne 0) { throw 'Canonical reservation failed; no provider may be invoked.' }
New-Item -ItemType Directory -Path $evidenceRoot | Out-Null
$permitPath = Join-Path $evidenceRoot 'live-permit.json'
@{
    schemaVersion = 'advertified.live-certification-permit.v1'
    canonicalReservationAccepted = $true
    reservationRunId = "$run"; reservationStepId = "$step"; tenantId = "$tenant"
    model = $Model; maximumCalls = $MaximumCalls; costCapMinor = $CostCapMinor
    maximumCostUsdMicros = $maximumMicros
    priorCostReceiptSha256 = (Get-FileHash -LiteralPath $priorPath -Algorithm SHA256).Hash
    expiresAtUtc = [DateTimeOffset]::UtcNow.AddHours(1).ToString('o')
} | ConvertTo-Json | Set-Content -LiteralPath $permitPath -Encoding UTF8
$settings = @{
    ADVERTIFIED_BUSINESS_SCENARIO_BUDGET_PERMIT = $permitPath
    ADVERTIFIED_BUSINESS_SCENARIO_MODEL = $Model
    ADVERTIFIED_BUSINESS_SCENARIO_COST_CAP_MINOR = "$CostCapMinor"
}
$previous = @{}
foreach ($name in $settings.Keys) {
    $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
    [Environment]::SetEnvironmentVariable($name, $settings[$name], 'Process')
}
$runExitCode = -1
Push-Location (Join-Path $repoRoot 'agent-runtime')
try {
    & python -u -B -m pytest @Tests -q -p no:cacheprovider --tb=short -rP "--junitxml=$evidenceRoot/live.xml" 2>&1 |
        Tee-Object -FilePath (Join-Path $evidenceRoot 'live.log')
    $runExitCode = $LASTEXITCODE
}
finally {
    Pop-Location
    foreach ($name in $settings.Keys) {
        [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process')
    }
    @{ exitCode = $runExitCode; reservationReleased = $false; tests = $Tests
       maximumCostUsdMicros = $maximumMicros; utc = [DateTimeOffset]::UtcNow.ToString('o') } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $evidenceRoot 'run.json') -Encoding UTF8
}
if ($runExitCode -ne 0) { throw "Live certification failed with exit code $runExitCode; reservation retained." }

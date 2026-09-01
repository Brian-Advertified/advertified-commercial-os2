param(
    [ValidateSet('All', 'Format', 'Secrets', 'Limits', 'Compose', 'Hygiene', 'Evidence')]
    [string]$Check = 'All'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot '../../..')

$sourceFiles = @(
    'api/Program.cs',
    'api/Endpoints/HealthEndpoints.cs',
    'api/Background/OutboxDispatchDispatcher.cs',
    'api/Background/OutboxDispatchMetrics.cs',
    'api/Background/OutboxDispatchProcessor.Lease.cs',
    'api/Background/OutboxDispatchProcessor.Outcomes.cs',
    'api/Background/OutboxDispatchProcessor.cs',
    'api/Background/OutboxDispatchReadiness.cs',
    'api/Startup/OutboxDispatchRegistration.cs',
    'api/src/Advertified.Commercial.Application/Outbox/OutboxDeliveryContracts.cs',
    'api/src/Advertified.Commercial.Infrastructure/Outbox/DeterministicOutboxTransport.cs',
    'api/src/Advertified.Commercial.Infrastructure/Outbox/OutboxDispatchClaim.cs',
    'api/src/Advertified.Commercial.Infrastructure/Outbox/OutboxDispatchOptions.cs',
    'api/src/Advertified.Commercial.Infrastructure/Outbox/OutboxDispatchStore.cs',
    'api/src/Advertified.Commercial.Infrastructure/Migrations/202609010027_OutboxDispatchDurability.cs',
    'api/src/Advertified.Commercial.Infrastructure/Migrations/202609010027_OutboxDispatchDurability.Schema.cs',
    'api/src/Advertified.Commercial.Infrastructure/Migrations/202609010027_OutboxDispatchDurability.Functions.cs',
    'api/src/Advertified.Commercial.Infrastructure/Migrations/202609010027_OutboxDispatchDurability.Transitions.cs',
    'api/src/Advertified.Commercial.Infrastructure/Migrations/202609010027_OutboxDispatchDurability.Rollback.cs',
    'api/src/Advertified.Commercial.Infrastructure/Migrations/GovernanceDbContextModelSnapshot.Platform.cs',
    'api/src/Advertified.Commercial.Infrastructure/Persistence/Configurations/PlatformRecordConfigurations.cs',
    'api/src/Advertified.Commercial.Infrastructure/Persistence/Records/OutboxMessageRow.cs',
    'api/tests/Advertified.Commercial.Api.Tests/OutboxDeliveryContractTests.cs',
    'api/tests/Advertified.Commercial.Api.Tests/OutboxDispatchAcceptanceTests.cs',
    'api/tests/Advertified.Commercial.Api.Tests/OutboxDispatchAcceptanceTests.Database.cs',
    'api/tests/Advertified.Commercial.Api.Tests/OutboxDispatchAcceptanceTests.Heartbeat.cs',
    'api/tests/Advertified.Commercial.Api.Tests/OutboxDispatchAcceptanceTests.Security.cs',
    'api/tests/Advertified.Commercial.Api.Tests/OutboxDispatchAcceptanceTests.Transport.cs',
    'api/tests/Advertified.Commercial.Api.Tests/OutboxDispatchDurabilityMigrationTests.cs',
    'api/tests/Advertified.Commercial.Api.Tests/OutboxDispatchReadinessTests.cs',
    'docs/GATE12_OUTBOX_DISPATCH_DURABILITY_WORK_PACKET.md'
)

function Invoke-FormatCheck {
    $applicationFiles = @(
        'api/src/Advertified.Commercial.Application/Outbox/OutboxDeliveryContracts.cs')
    dotnet format api/src/Advertified.Commercial.Application/Advertified.Commercial.Application.csproj `
        --no-restore --verify-no-changes --include $applicationFiles
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $infrastructureFiles = @(
        'api/src/Advertified.Commercial.Infrastructure/Outbox/DeterministicOutboxTransport.cs',
        'api/src/Advertified.Commercial.Infrastructure/Outbox/OutboxDispatchClaim.cs',
        'api/src/Advertified.Commercial.Infrastructure/Outbox/OutboxDispatchOptions.cs',
        'api/src/Advertified.Commercial.Infrastructure/Outbox/OutboxDispatchStore.cs',
        'api/src/Advertified.Commercial.Infrastructure/Migrations/202609010027_OutboxDispatchDurability.cs',
        'api/src/Advertified.Commercial.Infrastructure/Migrations/202609010027_OutboxDispatchDurability.Schema.cs',
        'api/src/Advertified.Commercial.Infrastructure/Migrations/202609010027_OutboxDispatchDurability.Functions.cs',
        'api/src/Advertified.Commercial.Infrastructure/Migrations/202609010027_OutboxDispatchDurability.Transitions.cs',
        'api/src/Advertified.Commercial.Infrastructure/Migrations/202609010027_OutboxDispatchDurability.Rollback.cs',
        'api/src/Advertified.Commercial.Infrastructure/Migrations/GovernanceDbContextModelSnapshot.Platform.cs',
        'api/src/Advertified.Commercial.Infrastructure/Persistence/Configurations/PlatformRecordConfigurations.cs',
        'api/src/Advertified.Commercial.Infrastructure/Persistence/Records/OutboxMessageRow.cs')
    dotnet format api/src/Advertified.Commercial.Infrastructure/Advertified.Commercial.Infrastructure.csproj `
        --no-restore --verify-no-changes --include $infrastructureFiles
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $apiFiles = @(
        'api/Program.cs', 'api/Endpoints/HealthEndpoints.cs',
        'api/Background/OutboxDispatchDispatcher.cs',
        'api/Background/OutboxDispatchMetrics.cs',
        'api/Background/OutboxDispatchProcessor.Lease.cs',
        'api/Background/OutboxDispatchProcessor.Outcomes.cs',
        'api/Background/OutboxDispatchProcessor.cs',
        'api/Background/OutboxDispatchReadiness.cs',
        'api/Startup/OutboxDispatchRegistration.cs')
    dotnet format api/Advertified.Commercial.Api.csproj `
        --no-restore --verify-no-changes --include $apiFiles
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $testFiles = @(
        'api/tests/Advertified.Commercial.Api.Tests/OutboxDeliveryContractTests.cs',
        'api/tests/Advertified.Commercial.Api.Tests/OutboxDispatchAcceptanceTests.cs',
        'api/tests/Advertified.Commercial.Api.Tests/OutboxDispatchAcceptanceTests.Database.cs',
        'api/tests/Advertified.Commercial.Api.Tests/OutboxDispatchAcceptanceTests.Heartbeat.cs',
        'api/tests/Advertified.Commercial.Api.Tests/OutboxDispatchAcceptanceTests.Security.cs',
        'api/tests/Advertified.Commercial.Api.Tests/OutboxDispatchAcceptanceTests.Transport.cs',
        'api/tests/Advertified.Commercial.Api.Tests/OutboxDispatchDurabilityMigrationTests.cs',
        'api/tests/Advertified.Commercial.Api.Tests/OutboxDispatchReadinessTests.cs')
    dotnet format api/tests/Advertified.Commercial.Api.Tests/Advertified.Commercial.Api.Tests.csproj `
        --no-restore --verify-no-changes --include $testFiles
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

function Invoke-SecretCheck {
    foreach ($file in $sourceFiles) {
        gitleaks dir $file --redact --no-banner --no-color --log-level error
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
    Write-Output ('SCOPED_GITLEAKS_FILE_COUNT=' + $sourceFiles.Count)
}

function Invoke-LimitCheck {
    $newOutboxFiles = @($sourceFiles | Where-Object {
        $_ -notin @(
            'api/Program.cs',
            'api/Endpoints/HealthEndpoints.cs',
            'api/src/Advertified.Commercial.Infrastructure/Migrations/GovernanceDbContextModelSnapshot.Platform.cs',
            'api/src/Advertified.Commercial.Infrastructure/Persistence/Configurations/PlatformRecordConfigurations.cs',
            'api/src/Advertified.Commercial.Infrastructure/Persistence/Records/OutboxMessageRow.cs',
            'docs/GATE12_OUTBOX_DISPATCH_DURABILITY_WORK_PACKET.md')
    })
    foreach ($file in $newOutboxFiles) {
        if ((Get-Content -LiteralPath $file).Count -gt 300) {
            throw "New outbox file exceeds the 300-line target: $file"
        }
    }
    if ((Get-Content -LiteralPath 'api/Program.cs').Count -gt 400) {
        throw 'Program.cs exceeds the 400-line hard limit.'
    }

    $migrationFiles = @(
        'api/src/Advertified.Commercial.Infrastructure/Migrations/202609010027_OutboxDispatchDurability.Functions.cs',
        'api/src/Advertified.Commercial.Infrastructure/Migrations/202609010027_OutboxDispatchDurability.Transitions.cs')
    foreach ($migrationFile in $migrationFiles) {
        $lines = Get-Content -LiteralPath $migrationFile
        for ($index = 0; $index -lt $lines.Count; $index++) {
            if ($lines[$index] -notmatch 'CREATE FUNCTION commercial\.([a-z_]+)') { continue }
            $functionName = $Matches[1]
            $endPattern = '\$' + [Regex]::Escape($functionName) + '\$;'
            $endIndex = $index
            while ($endIndex -lt $lines.Count -and $lines[$endIndex] -notmatch $endPattern) {
                $endIndex++
            }
            if ($endIndex -ge $lines.Count) { throw "Missing SQL delimiter for $functionName" }
            $lineCount = @($lines[$index..$endIndex] | Where-Object {
                -not [string]::IsNullOrWhiteSpace($_)
            }).Count
            if ($lineCount -gt 60) { throw "$functionName exceeds 60 lines: $lineCount" }
            Write-Output ("SQL_FUNCTION_LINES={0}:{1}" -f $functionName, $lineCount)
        }
    }
}

function Invoke-ComposeCheck {
    docker compose -f infrastructure/docker-compose.yml config --quiet
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    $services = @(docker compose -f infrastructure/docker-compose.yml ps --format json |
        ConvertFrom-Json)
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    $healthy = @($services | Where-Object { $_.Health -eq 'healthy' })
    if ($services.Count -ne 6 -or $healthy.Count -ne 6) {
        throw 'Expected six healthy local Compose services.'
    }
    Write-Output ('SERVICE_COUNT=' + $services.Count)
    Write-Output ('HEALTHY_COUNT=' + $healthy.Count)
}

function Invoke-HygieneCheck {
    git diff --check
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    if (@(git diff --cached --name-only).Count -ne 0) { throw 'Staged files found.' }
    if (@(git ls-files .artifacts artifacts).Count -ne 0) { throw 'Tracked artifacts found.' }
    $checkedFiles = @($sourceFiles + @(
        'docs/evidence/gate12-outbox-dispatch-durability-20260901/REPORT.md',
        'docs/evidence/gate12-outbox-dispatch-durability-20260901/manifest.json',
        'docs/evidence/gate12-outbox-dispatch-durability-20260901/VERIFY.ps1'))
    if (@(Select-String -Path $checkedFiles -Pattern '[ \t]+$').Count -ne 0) {
        throw 'Scoped trailing whitespace found.'
    }
}

function Invoke-EvidenceCheck {
    $manifestPath = Join-Path $PSScriptRoot 'manifest.json'
    $schemaPath = Join-Path $PSScriptRoot '../manifest.schema.json'
    $manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
    $schema = Get-Content -Raw -LiteralPath $schemaPath | ConvertFrom-Json
    $present = @($manifest.PSObject.Properties.Name)
    if (@($schema.required | Where-Object { $_ -notin $present }).Count -ne 0) {
        throw 'Evidence manifest is missing required properties.'
    }
    $allowed = @($schema.properties.PSObject.Properties.Name)
    if (@($present | Where-Object { $_ -notin $allowed }).Count -ne 0) {
        throw 'Evidence manifest contains unexpected properties.'
    }
    foreach ($item in $manifest.checks) {
        if ([string]::IsNullOrWhiteSpace($item.name) -or
            [string]::IsNullOrWhiteSpace($item.command) -or
            $item.outcome -notin @('PASS', 'FAIL', 'BLOCKED', 'PENDING')) {
            throw 'Evidence manifest contains an invalid check.'
        }
    }
    if ($manifest.baseCommit -notmatch '^[0-9a-f]{40}$') {
        throw 'Evidence base commit is invalid.'
    }
}

Push-Location $repositoryRoot
try {
    if ($Check -in @('All', 'Format')) { Invoke-FormatCheck }
    if ($Check -in @('All', 'Secrets')) { Invoke-SecretCheck }
    if ($Check -in @('All', 'Limits')) { Invoke-LimitCheck }
    if ($Check -in @('All', 'Compose')) { Invoke-ComposeCheck }
    if ($Check -in @('All', 'Hygiene')) { Invoke-HygieneCheck }
    if ($Check -in @('All', 'Evidence')) { Invoke-EvidenceCheck }
    Write-Output ("OUTBOX_VERIFICATION_CHECK={0}:PASS" -f $Check)
}
finally {
    Pop-Location
}

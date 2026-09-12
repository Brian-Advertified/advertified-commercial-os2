param(
    [Parameter(Mandatory = $true)][string]$HostEnvironmentFile,
    [Parameter(Mandatory = $true)][string]$ApiEnvironmentFile,
    [Parameter(Mandatory = $true)][string]$WorkerEnvironmentFile,
    [Parameter(Mandatory = $true)][string]$MigratorEnvironmentFile,
    [Parameter(Mandatory = $true)][string]$AgentEnvironmentFile,
    [string]$ComposeFile = "infrastructure/docker-compose.production.yml"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Read-AdvertifiedEnvironment {
    param([string]$Path)
    $resolved = (Resolve-Path $Path).Path
    $values = @{}
    foreach ($line in Get-Content -LiteralPath $resolved) {
        $value = $line.Trim()
        if (-not $value -or $value.StartsWith("#")) { continue }
        $separator = $value.IndexOf("=")
        if ($separator -lt 1) { throw "Invalid environment entry in $resolved." }
        $key = $value.Substring(0, $separator).Trim()
        if ($values.ContainsKey($key)) { throw "Duplicate environment key '$key' in $resolved." }
        $values[$key] = $value.Substring($separator + 1).Trim()
    }
    return $values
}

function Require-AdvertifiedValue {
    param([hashtable]$Values, [string]$Key)
    if (-not $Values.ContainsKey($Key) -or [string]::IsNullOrWhiteSpace([string]$Values[$Key])) {
        throw "Missing production configuration key '$Key'."
    }
    return [string]$Values[$Key]
}

function Assert-AdvertifiedExact {
    param([hashtable]$Values, [string]$Key, [string]$Expected)
    $actual = Require-AdvertifiedValue $Values $Key
    if ($actual -cne $Expected) { throw "Production configuration key '$Key' must be '$Expected'." }
}

function Read-AdvertifiedPositiveInt64 {
    param([hashtable]$Values, [string]$Key)
    $raw = Require-AdvertifiedValue $Values $Key
    [long]$parsed = 0
    if (-not [long]::TryParse($raw, [ref]$parsed) -or $parsed -le 0) {
        throw "Production configuration key '$Key' must be a positive integer."
    }
    return $parsed
}

function Assert-AdvertifiedHttps {
    param([hashtable]$Values, [string]$Key)
    $actual = Require-AdvertifiedValue $Values $Key
    $uri = $null
    if (-not [Uri]::TryCreate($actual, [UriKind]::Absolute, [ref]$uri) -or
        $uri.Scheme -cne "https" -or $uri.UserInfo) {
        throw "Production endpoint '$Key' must be credential-free HTTPS."
    }
}

function Assert-AdvertifiedAgentRuntimeEndpoint {
    param([hashtable]$Values)
    $actual = Require-AdvertifiedValue $Values "AgentRuntime__BaseUrl"
    $uri = $null
    if (-not [Uri]::TryCreate($actual, [UriKind]::Absolute, [ref]$uri)) {
        throw "Production agent runtime endpoint is invalid."
    }
    $secure = $uri.Scheme -ceq "https" -and -not $uri.UserInfo
    $privateCompose = $uri.Scheme -ceq "http" -and $uri.Host -ceq "agent-runtime" -and
        $uri.Port -eq 8080 -and -not $uri.UserInfo -and $uri.AbsolutePath -ceq "/" -and
        -not $uri.Query -and -not $uri.Fragment
    if (-not ($secure -or $privateCompose)) {
        throw "Production agent runtime must use credential-free HTTPS or the exact private Compose endpoint 'http://agent-runtime:8080'."
    }
}

function Assert-AdvertifiedResearchEndpoint {
    param([AllowNull()][string]$Value, [string]$PublicHost, [string]$Name)
    $uri = $null
    if ([string]::IsNullOrWhiteSpace($Value) -or $Value -like '*REPLACE_WITH*' -or
        -not [Uri]::TryCreate($Value, [UriKind]::Absolute, [ref]$uri) -or
        $uri.Scheme -cne 'https' -or $uri.UserInfo -or $uri.Host -ieq $PublicHost) {
        throw "Production $Name must use an explicit non-public credential-free HTTPS provider endpoint."
    }
}

function Assert-AdvertifiedProviderUserAgent {
    param([AllowNull()][string]$Value, [string]$Name)
    if ([string]::IsNullOrWhiteSpace($Value) -or $Value.Contains("`r") -or $Value.Contains("`n")) {
        throw "Production $Name must configure a safe provider user agent."
    }
}

function Assert-AdvertifiedNoDevelopmentValue {
    param([hashtable[]]$ValueSets)
    $forbidden = @("REPLACE_WITH", "localhost", "127.0.0.1", "advertified-local-only", "mailhog", "minio:")
    foreach ($values in $ValueSets) {
        foreach ($pair in $values.GetEnumerator()) {
            foreach ($marker in $forbidden) {
                if ([string]$pair.Value -like "*$marker*") {
                    throw "Production configuration key '$($pair.Key)' contains a forbidden development value."
                }
            }
        }
    }
}

function Assert-AdvertifiedWorkerBoundary {
    param([hashtable]$ApiValues, [hashtable]$WorkerValues)
    $allowed = @("ConnectionStrings__WorkerSchedulerDatabase")
    $unexpected = @($WorkerValues.Keys | Where-Object { $allowed -cnotcontains $_ })
    if ($unexpected.Count -gt 0) {
        throw "Worker-only environment contains unrelated application configuration: $($unexpected -join ', ')."
    }
    if ($ApiValues.ContainsKey("ConnectionStrings__WorkerSchedulerDatabase")) {
        throw "API environment must not contain the worker scheduler database connection."
    }
    $applicationConnection = Require-AdvertifiedValue $ApiValues "ConnectionStrings__CommercialDatabase"
    $workerConnection = Require-AdvertifiedValue $WorkerValues "ConnectionStrings__WorkerSchedulerDatabase"
    if ($workerConnection -ceq $applicationConnection) {
        throw "Production API and worker database connections must be distinct."
    }
}

function Assert-AdvertifiedMigratorBoundary {
    param([hashtable]$ApiValues, [hashtable]$WorkerValues, [hashtable]$MigratorValues)
    $allowed = @("ASPNETCORE_ENVIRONMENT", "ADVERTIFIED_MIGRATION_CONNECTION_STRING")
    $unexpected = @($MigratorValues.Keys | Where-Object { $allowed -cnotcontains $_ })
    if ($unexpected.Count -gt 0) {
        throw "Migration-only environment contains unrelated application configuration: $($unexpected -join ', ')."
    }
    Assert-AdvertifiedExact $MigratorValues "ASPNETCORE_ENVIRONMENT" "Production"
    $migrationConnection = Require-AdvertifiedValue $MigratorValues "ADVERTIFIED_MIGRATION_CONNECTION_STRING"
    $applicationConnection = Require-AdvertifiedValue $ApiValues "ConnectionStrings__CommercialDatabase"
    $workerConnection = Require-AdvertifiedValue $WorkerValues "ConnectionStrings__WorkerSchedulerDatabase"
    if ($migrationConnection -ceq $applicationConnection -or $migrationConnection -ceq $workerConnection) {
        throw "Production migration connection must be distinct from API and worker database connections."
    }
}

function Assert-AdvertifiedModelRouting {
    param([hashtable]$HostValues, [hashtable]$ApiValues, [hashtable]$AgentValues)
    $configPath = Require-AdvertifiedValue $HostValues 'ADVERTIFIED_API_CONFIG_FILE'
    $config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json

    Assert-AdvertifiedResearchEndpoint ([string]$config.LocationIntelligence.Discovery.Endpoint) `
        'nominatim.openstreetmap.org' 'location discovery'
    Assert-AdvertifiedProviderUserAgent ([string]$config.LocationIntelligence.Discovery.UserAgent) `
        'location discovery'
    Assert-AdvertifiedResearchEndpoint ([string]$config.LocationIntelligence.PoiDiscovery.Endpoint) `
        'overpass-api.de' 'POI discovery'
    Assert-AdvertifiedProviderUserAgent ([string]$config.LocationIntelligence.PoiDiscovery.UserAgent) `
        'POI discovery'

    $models = @{}
    foreach ($property in $config.AgentRuntime.Models.PSObject.Properties) {
        if ($property.Value -isnot [string]) {
            throw 'Model routes must contain explicit model identifiers, not nested environment configuration.'
        }
        $models[$property.Name] = $property.Value
    }
    $registryPath = Join-Path $PSScriptRoot '../shared/contracts/master-data.json'
    $registry = Get-Content -LiteralPath $registryPath -Raw | ConvertFrom-Json
    $agentCodes = @($registry.collections.agentTypes | Where-Object { $_.isActive } | ForEach-Object { $_.code })
    $allowlist = @((Require-AdvertifiedValue $AgentValues 'ADVERTIFIED_BEDROCK_MODEL_ALLOWLIST').Split(',') |
        ForEach-Object { $_.Trim() } | Where-Object { $_ })
    foreach ($agentCode in $agentCodes) { [void](Require-AdvertifiedValue $models $agentCode) }
    foreach ($route in $models.GetEnumerator()) {
        $agentCode = ($route.Key -split '__', 2)[0]
        if ($agentCodes -cnotcontains $agentCode -or $allowlist -cnotcontains $route.Value -or
            $route.Value -eq 'fixture-v1' -or $route.Value -like '*REPLACE_WITH*') {
            throw "Model route '$($route.Key)' is not an explicitly approved, allow-listed active agent route."
        }
    }
    foreach ($key in $ApiValues.Keys) {
        if ($key.StartsWith('AgentRuntime__Models__')) {
            throw 'Supply model routing through the mounted JSON, not ambiguous double-underscore environment keys.'
        }
        if ($key.StartsWith('AgentRuntime__CostCapsMinor__')) {
            $agentCode = $key.Substring('AgentRuntime__CostCapsMinor__'.Length)
            if ($agentCodes -cnotcontains $agentCode) { throw "The cost cap '$key' refers to a retired or unknown agent." }
        }
    }
    $semanticModel = Require-AdvertifiedValue $ApiValues 'InventorySemantic__ModelId'
    if ($allowlist -cnotcontains $semanticModel -or
        $models['inventory_intelligence__source_transcription'] -cne $semanticModel -or
        $models['inventory_intelligence__semantic_enrichment'] -cne $semanticModel) {
        throw 'Inventory semantic extraction must use the same explicitly allow-listed model as its governed runtime routes.'
    }
    $inputPrice = Read-AdvertifiedPositiveInt64 $ApiValues 'InventorySemantic__InputPricePerMillionTokensUsdMicros'
    $outputPrice = Read-AdvertifiedPositiveInt64 $ApiValues 'InventorySemantic__OutputPricePerMillionTokensUsdMicros'
    $perCallMicros = Read-AdvertifiedPositiveInt64 $ApiValues 'InventorySemantic__PerCallCostCapUsdMicros'
    $certificationMicros = Read-AdvertifiedPositiveInt64 $ApiValues 'InventorySemantic__CertificationBudgetUsdMicros'
    if ($perCallMicros -gt $certificationMicros -or $certificationMicros -gt 5000000) {
        throw 'Inventory semantic extraction cost limits exceed the governed certification budget.'
    }
    $inventoryCapMinor = Read-AdvertifiedPositiveInt64 $ApiValues 'AgentRuntime__CostCapsMinor__inventory_intelligence'
    $expectedCapMinor = [long][Math]::Ceiling($perCallMicros / 10000.0)
    if ($inventoryCapMinor -ne $expectedCapMinor) {
        throw 'Inventory semantic per-call cost and inventory-intelligence runtime cost cap do not reconcile.'
    }
    [void]$inputPrice
    [void]$outputPrice
    $serviceKey = Require-AdvertifiedValue $ApiValues 'AgentRuntime__ServiceKey'
    if ($serviceKey -cne (Require-AdvertifiedValue $AgentValues 'ADVERTIFIED_AGENT_RUNTIME_SERVICE_KEY')) {
        throw 'Commercial API and agent-runtime service keys do not match.'
    }
}

function Assert-AdvertifiedEnvironmentBinding {
    param([hashtable]$HostValues, [string]$Key, [string]$ValidatedFile)
    $composePath = (Resolve-Path (Require-AdvertifiedValue $HostValues $Key)).Path
    if ($composePath -ne (Resolve-Path $ValidatedFile).Path) {
        throw "Compose environment reference '$Key' does not match the file being validated."
    }
}

$hostValues = Read-AdvertifiedEnvironment $HostEnvironmentFile
$apiValues = Read-AdvertifiedEnvironment $ApiEnvironmentFile
$workerValues = Read-AdvertifiedEnvironment $WorkerEnvironmentFile
$migratorValues = Read-AdvertifiedEnvironment $MigratorEnvironmentFile
$agentValues = Read-AdvertifiedEnvironment $AgentEnvironmentFile

foreach ($imageKey in @(
    "ADVERTIFIED_API_IMAGE", "ADVERTIFIED_MIGRATOR_IMAGE",
    "ADVERTIFIED_AGENT_RUNTIME_IMAGE", "ADVERTIFIED_WEB_IMAGE"
)) {
    $image = Require-AdvertifiedValue $hostValues $imageKey
    if ($image -cnotmatch "^.+@sha256:[0-9a-f]{64}$") {
        throw "Production image '$imageKey' must use an immutable sha256 digest."
    }
}

$apiRequired = @(
    "ConnectionStrings__CommercialDatabase",
    "Authentication__Oidc__ClientId", "Authentication__Oidc__ClientSecret",
    "Authentication__DataProtection__CertificatePassword", "InventoryProtection__Bucket",
    "InventorySemantic__ModelId",
    "InventorySemantic__InputPricePerMillionTokensUsdMicros",
    "InventorySemantic__OutputPricePerMillionTokensUsdMicros",
    "InventorySemantic__PerCallCostCapUsdMicros", "InventorySemantic__CertificationBudgetUsdMicros",
    "InventorySemantic__BudgetScope", "InventorySemantic__PromptVersion", "AgentRuntime__ServiceKey",
    "EmailAutomation__ResendApiKey", "EmailAutomation__ResendWebhookSecret", "OutboxDispatch__EventBusName"
)
foreach ($key in $apiRequired) { [void](Require-AdvertifiedValue $apiValues $key) }

Assert-AdvertifiedExact $apiValues "ASPNETCORE_ENVIRONMENT" "Production"
Assert-AdvertifiedExact $apiValues "Process__Role" "Api"
Assert-AdvertifiedExact $apiValues "Authentication__Mode" "Oidc"
Assert-AdvertifiedExact $apiValues "Authentication__BrowserSession__SecureCookie" "true"
Assert-AdvertifiedExact $apiValues "InventoryProtection__ObjectStoreMode" "AwsS3"
Assert-AdvertifiedExact $apiValues "InventoryProtection__ScannerMode" "ExternalVerdict"
Assert-AdvertifiedExact $apiValues "InventoryExtraction__Mode" "Native"
Assert-AdvertifiedExact $apiValues "InventoryProcessing__Paused" "false"
Assert-AdvertifiedExact $apiValues "InventorySemantic__Enabled" "true"
Assert-AdvertifiedExact $apiValues "AgentRuntime__Mode" "Http"
Assert-AdvertifiedExact $apiValues "AgentRuntime__Provider" "bedrock"
Assert-AdvertifiedExact $apiValues "AgentRuntime__AllowLive" "true"
Assert-AdvertifiedExact $apiValues "EmailAutomation__Mode" "Resend"
Assert-AdvertifiedExact $apiValues "EmailAutomation__ProcessInline" "false"
Assert-AdvertifiedExact $apiValues "OutboxDispatch__Mode" "EventBridge"
Assert-AdvertifiedHttps $apiValues "Authentication__Oidc__Authority"
Assert-AdvertifiedHttps $apiValues "Authentication__Oidc__LogoutEndpoint"
Assert-AdvertifiedAgentRuntimeEndpoint $apiValues
Assert-AdvertifiedHttps $apiValues "EmailAutomation__ResendApiBaseUrl"
Assert-AdvertifiedWorkerBoundary $apiValues $workerValues
Assert-AdvertifiedMigratorBoundary $apiValues $workerValues $migratorValues

$agentRequired = @(
    "ADVERTIFIED_AGENT_RUNTIME_SERVICE_KEY", "ADVERTIFIED_BEDROCK_REGION",
    "ADVERTIFIED_BEDROCK_MODEL_ALLOWLIST", "ADVERTIFIED_BEDROCK_PRICING_JSON"
)
foreach ($key in $agentRequired) { [void](Require-AdvertifiedValue $agentValues $key) }
Assert-AdvertifiedExact $agentValues "ADVERTIFIED_AGENT_RUNTIME_MODE" "bedrock"
Assert-AdvertifiedExact $agentValues "ADVERTIFIED_INVENTORY_PROCESSING_PAUSED" "false"

Assert-AdvertifiedNoDevelopmentValue @($hostValues, $apiValues, $workerValues, $migratorValues, $agentValues)
Assert-AdvertifiedEnvironmentBinding $hostValues 'ADVERTIFIED_API_ENV_FILE' $ApiEnvironmentFile
Assert-AdvertifiedEnvironmentBinding $hostValues 'ADVERTIFIED_WORKER_ENV_FILE' $WorkerEnvironmentFile
Assert-AdvertifiedEnvironmentBinding $hostValues 'ADVERTIFIED_MIGRATOR_ENV_FILE' $MigratorEnvironmentFile
Assert-AdvertifiedEnvironmentBinding $hostValues 'ADVERTIFIED_AGENT_ENV_FILE' $AgentEnvironmentFile
Assert-AdvertifiedModelRouting $hostValues $apiValues $agentValues

$composePath = if ([System.IO.Path]::IsPathRooted($ComposeFile)) { $ComposeFile } else {
    Join-Path (Join-Path $PSScriptRoot '..') $ComposeFile
}
$compose = (Resolve-Path $composePath).Path
& docker compose --env-file $HostEnvironmentFile --file $compose config --quiet
if ($LASTEXITCODE -ne 0) { throw "The production Compose configuration is invalid." }

Write-Output "Advertified production configuration passed fail-closed validation."

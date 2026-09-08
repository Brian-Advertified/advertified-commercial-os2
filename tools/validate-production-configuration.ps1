param(
    [Parameter(Mandatory = $true)][string]$HostEnvironmentFile,
    [Parameter(Mandatory = $true)][string]$ApiEnvironmentFile,
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
        if (-not $value -or $value.StartsWith("#")) {
            continue
        }
        $separator = $value.IndexOf("=")
        if ($separator -lt 1) {
            throw "Invalid environment entry in $resolved."
        }
        $key = $value.Substring(0, $separator).Trim()
        if ($values.ContainsKey($key)) {
            throw "Duplicate environment key '$key' in $resolved."
        }
        $values[$key] = $value.Substring($separator + 1).Trim()
    }
    return $values
}

function Require-AdvertifiedValue {
    param(
        [hashtable]$Values,
        [string]$Key
    )
    if (-not $Values.ContainsKey($Key) -or
        [string]::IsNullOrWhiteSpace([string]$Values[$Key])) {
        throw "Missing production configuration key '$Key'."
    }
    return [string]$Values[$Key]
}

function Assert-AdvertifiedExact {
    param(
        [hashtable]$Values,
        [string]$Key,
        [string]$Expected
    )
    $actual = Require-AdvertifiedValue $Values $Key
    if ($actual -cne $Expected) {
        throw "Production configuration key '$Key' must be '$Expected'."
    }
}

function Assert-AdvertifiedHttps {
    param(
        [hashtable]$Values,
        [string]$Key
    )
    $actual = Require-AdvertifiedValue $Values $Key
    $uri = $null
    if (-not [Uri]::TryCreate($actual, [UriKind]::Absolute, [ref]$uri) -or
        $uri.Scheme -cne "https" -or $uri.UserInfo) {
        throw "Production endpoint '$Key' must be credential-free HTTPS."
    }
}

function Assert-AdvertifiedNoDevelopmentValue {
    param([hashtable[]]$ValueSets)
    $forbidden = @(
        "REPLACE_WITH",
        "localhost",
        "127.0.0.1",
        "advertified-local-only",
        "mailhog",
        "minio:"
    )
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

$hostValues = Read-AdvertifiedEnvironment $HostEnvironmentFile
$apiValues = Read-AdvertifiedEnvironment $ApiEnvironmentFile
$agentValues = Read-AdvertifiedEnvironment $AgentEnvironmentFile

foreach ($imageKey in @(
    "ADVERTIFIED_API_IMAGE",
    "ADVERTIFIED_MIGRATOR_IMAGE",
    "ADVERTIFIED_AGENT_RUNTIME_IMAGE",
    "ADVERTIFIED_WEB_IMAGE"
)) {
    $image = Require-AdvertifiedValue $hostValues $imageKey
    if ($image -cnotmatch "^.+@sha256:[0-9a-f]{64}$") {
        throw "Production image '$imageKey' must use an immutable sha256 digest."
    }
}

$apiRequired = @(
    "ConnectionStrings__CommercialDatabase",
    "ConnectionStrings__WorkerSchedulerDatabase",
    "Authentication__Oidc__ClientId",
    "Authentication__Oidc__ClientSecret",
    "Authentication__DataProtection__CertificatePassword",
    "InventoryProtection__Bucket",
    "InventoryProtection__ClamAvHost",
    "InventoryExtraction__ApiKey",
    "AgentRuntime__ServiceKey",
    "EmailAutomation__ResendApiKey",
    "EmailAutomation__ResendWebhookSecret",
    "OutboxDispatch__EventBusName"
)
foreach ($key in $apiRequired) {
    [void](Require-AdvertifiedValue $apiValues $key)
}

Assert-AdvertifiedExact $apiValues "ASPNETCORE_ENVIRONMENT" "Production"
Assert-AdvertifiedExact $apiValues "Process__Role" "Api"
Assert-AdvertifiedExact $apiValues "Authentication__Mode" "Oidc"
Assert-AdvertifiedExact $apiValues "Authentication__BrowserSession__SecureCookie" "true"
Assert-AdvertifiedExact $apiValues "InventoryProtection__ObjectStoreMode" "AwsS3"
Assert-AdvertifiedExact $apiValues "InventoryProtection__ScannerMode" "ClamAv"
Assert-AdvertifiedExact $apiValues "InventoryExtraction__Mode" "Deterministic"
Assert-AdvertifiedExact $apiValues "InventoryProcessing__Paused" "true"
Assert-AdvertifiedExact $apiValues "AgentRuntime__Mode" "Http"
Assert-AdvertifiedExact $apiValues "AgentRuntime__Provider" "bedrock"
Assert-AdvertifiedExact $apiValues "AgentRuntime__AllowLive" "true"
Assert-AdvertifiedExact $apiValues "EmailAutomation__Mode" "Resend"
Assert-AdvertifiedExact $apiValues "EmailAutomation__ProcessInline" "false"
Assert-AdvertifiedExact $apiValues "OutboxDispatch__Mode" "EventBridge"
Assert-AdvertifiedHttps $apiValues "Authentication__Oidc__Authority"
Assert-AdvertifiedHttps $apiValues "Authentication__Oidc__LogoutEndpoint"
Assert-AdvertifiedHttps $apiValues "InventoryExtraction__BaseUrl"
Assert-AdvertifiedHttps $apiValues "AgentRuntime__BaseUrl"
Assert-AdvertifiedHttps $apiValues "EmailAutomation__ResendApiBaseUrl"

$agentRequired = @(
    "ADVERTIFIED_AGENT_RUNTIME_SERVICE_KEY",
    "ADVERTIFIED_BEDROCK_REGION",
    "ADVERTIFIED_BEDROCK_MODEL_ALLOWLIST",
    "ADVERTIFIED_BEDROCK_PRICING_JSON"
)
foreach ($key in $agentRequired) {
    [void](Require-AdvertifiedValue $agentValues $key)
}
Assert-AdvertifiedExact $agentValues "ADVERTIFIED_AGENT_RUNTIME_MODE" "bedrock"
Assert-AdvertifiedExact $agentValues "ADVERTIFIED_INVENTORY_PROCESSING_PAUSED" "true"

Assert-AdvertifiedNoDevelopmentValue @($hostValues, $apiValues, $agentValues)

$compose = (Resolve-Path $ComposeFile).Path
& docker compose --env-file $HostEnvironmentFile --file $compose config --quiet
if ($LASTEXITCODE -ne 0) {
    throw "The production Compose configuration is invalid."
}

Write-Output "Advertified production configuration passed fail-closed validation."

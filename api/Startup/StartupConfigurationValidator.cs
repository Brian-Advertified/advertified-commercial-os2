using Advertified.Commercial.Api.Authentication;
using Advertified.Commercial.Infrastructure.EmailAutomation;
using Advertified.Commercial.Infrastructure.Inventory;
using Advertified.Commercial.Infrastructure.LocationIntelligence;
using Advertified.Commercial.Infrastructure.Opportunity;
using Advertified.Commercial.Infrastructure.Outbox;

namespace Advertified.Commercial.Api.Startup;

internal static class StartupConfigurationValidator
{
    private const string PrivateAgentRuntimeHost = "agent-runtime";
    private const int PrivateAgentRuntimePort = 8080;
    private const string PublicNominatimHost = "nominatim.openstreetmap.org";
    private const string PublicOverpassHost = "overpass-api.de";

    internal static string ValidateAndGetConnectionString(
        WebApplicationBuilder builder,
        ProcessRoleOptions processRole,
        string? authenticationMode,
        AgentRuntimeOptions agentRuntime,
        InventoryProtectionOptions inventoryProtection,
        InventoryExtractionOptions inventoryExtraction,
        EmailAutomationOptions emailAutomation)
    {
        var localEnvironment = builder.Environment.IsDevelopment() ||
            builder.Environment.IsEnvironment("Test");
        if (!localEnvironment)
        {
            EnsureProductionProcessRole(processRole);
            EnsureProductionBoundaries(
                builder.Configuration, authenticationMode, agentRuntime,
                inventoryProtection, inventoryExtraction, emailAutomation);
        }

        var connectionString = builder.Configuration.GetConnectionString("CommercialDatabase");
        return string.IsNullOrWhiteSpace(connectionString)
            ? throw new InvalidOperationException(
                "The commercial database connection is not configured.")
            : connectionString;
    }

    private static void EnsureProductionProcessRole(
        ProcessRoleOptions processRole)
    {
        if (processRole.Role == ProcessRoleOptions.CombinedRole)
        {
            throw new InvalidOperationException(
                "Production requires separate API and worker processes.");
        }
    }

    private static void EnsureProductionBoundaries(
        ConfigurationManager configuration,
        string? authenticationMode,
        AgentRuntimeOptions agentRuntime,
        InventoryProtectionOptions inventoryProtection,
        InventoryExtractionOptions inventoryExtraction,
        EmailAutomationOptions emailAutomation)
    {
        var releaseSmoke = configuration.GetValue<bool>("ReleaseSmoke:Enabled");
        EnsureAuthenticationAndRuntime(
            authenticationMode, agentRuntime, releaseSmoke);
        EnsureCertificationOverridesDisabled(configuration);
        EnsureInventoryProtection(inventoryProtection);
        EnsureHttpEdge(configuration);
        if (!releaseSmoke)
        {
            EnsureProductionLocationIntelligence(configuration);
            EnsureCompleteProductionServices(
                configuration, agentRuntime, inventoryExtraction, emailAutomation);
        }
    }

    private static void EnsureAuthenticationAndRuntime(
        string? authenticationMode,
        AgentRuntimeOptions agentRuntime,
        bool releaseSmoke)
    {
        if (authenticationMode is LocalIdentityDefaults.DeterministicMode or
            LocalIdentityDefaults.DeterministicSessionMode)
        {
            throw new InvalidOperationException(
                "Deterministic authentication and sessions are restricted to development and test.");
        }
        if (authenticationMode != LocalIdentityDefaults.OidcMode &&
            !(releaseSmoke && authenticationMode == LocalIdentityDefaults.DisabledMode))
        {
            throw new InvalidOperationException(
                "Production authentication must use OIDC. Disabled authentication requires explicit release-smoke mode.");
        }
        if (agentRuntime.Mode == AgentRuntimeOptions.HttpDeterministicMode)
        {
            throw new InvalidOperationException(
                "Development-only agent runtime modes are restricted to development and test.");
        }
        if (agentRuntime.UsesHttp && !HasSafeAgentRuntimeTransport(agentRuntime.BaseUrl))
        {
            throw new InvalidOperationException(
                "Agent runtime transport must use HTTPS unless it is the exact private production Compose service endpoint.");
        }
    }

    internal static bool HasSafeAgentRuntimeTransport(string value) =>
        SafeHttps(value) || SafePrivateAgentRuntime(value);

    private static bool SafePrivateAgentRuntime(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return false;
        }
        return uri.Scheme == Uri.UriSchemeHttp &&
            string.Equals(uri.Host, PrivateAgentRuntimeHost, StringComparison.Ordinal) &&
            uri.Port == PrivateAgentRuntimePort &&
            string.IsNullOrEmpty(uri.UserInfo) &&
            uri.AbsolutePath == "/" &&
            string.IsNullOrEmpty(uri.Query) &&
            string.IsNullOrEmpty(uri.Fragment);
    }

    private static void EnsureCertificationOverridesDisabled(ConfigurationManager configuration)
    {
        if (!string.IsNullOrWhiteSpace(configuration[CampaignLifecycleClock.OverrideKey]))
        {
            throw new InvalidOperationException(
                "Campaign certification time overrides are restricted to development and test.");
        }
    }

    private static void EnsureInventoryProtection(
        InventoryProtectionOptions inventoryProtection)
    {
        if (inventoryProtection.ObjectStoreMode != InventoryProtectionOptions.AwsS3Mode)
        {
            throw new InvalidOperationException(
                "Production file protection requires private AWS S3 object storage.");
        }
        if (inventoryProtection.ScannerMode != InventoryProtectionOptions.ExternalVerdictScanner)
        {
            throw new InvalidOperationException(
                "Production file protection requires the governed external malware-verdict mode.");
        }
    }

    private static void EnsureProductionLocationIntelligence(
        ConfigurationManager configuration)
    {
        var discoveryEndpoint = configuration[$"{LocationDiscoveryOptions.SectionName}:Endpoint"];
        var discoveryUserAgent = configuration[$"{LocationDiscoveryOptions.SectionName}:UserAgent"];
        var poiEndpoint = configuration[$"{PoiDiscoveryOptions.SectionName}:Endpoint"];
        var poiUserAgent = configuration[$"{PoiDiscoveryOptions.SectionName}:UserAgent"];

        if (!HasSafeProductionLocationEndpoint(discoveryEndpoint, PublicNominatimHost) ||
            !HasSafeProviderUserAgent(discoveryUserAgent))
        {
            throw new InvalidOperationException(
                "Production location discovery requires an explicit non-public HTTPS Nominatim-compatible provider and user agent.");
        }
        if (!HasSafeProductionLocationEndpoint(poiEndpoint, PublicOverpassHost) ||
            !HasSafeProviderUserAgent(poiUserAgent))
        {
            throw new InvalidOperationException(
                "Production POI discovery requires an explicit non-public HTTPS Overpass-compatible provider and user agent.");
        }
    }

    internal static bool HasSafeProductionLocationEndpoint(
        string? value,
        string publicServiceHost)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return false;
        }
        return uri.Scheme == Uri.UriSchemeHttps &&
            string.IsNullOrEmpty(uri.UserInfo) &&
            !string.Equals(uri.Host, publicServiceHost, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasSafeProviderUserAgent(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        !value.Contains('\r') &&
        !value.Contains('\n');

    private static void EnsureHttpEdge(ConfigurationManager configuration)
    {
        var allowedHosts = configuration["AllowedHosts"];
        if (string.IsNullOrWhiteSpace(allowedHosts) ||
            allowedHosts.Split(';', StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries)
                .Any(host => host == "*"))
        {
            throw new InvalidOperationException(
                "Production must configure an explicit AllowedHosts allow-list.");
        }
        if (!TrustedProxyConfiguration.HasExplicitTrustBoundary(configuration))
        {
            throw new InvalidOperationException(
                "Production must configure an explicit trusted reverse-proxy boundary.");
        }
        if (configuration.GetValue<bool?>(
                $"{BrowserSessionOptions.SectionName}:SecureCookie") is false)
        {
            throw new InvalidOperationException(
                "Production browser sessions must use secure cookies.");
        }
    }

    private static void EnsureCompleteProductionServices(
        ConfigurationManager configuration,
        AgentRuntimeOptions agentRuntime,
        InventoryExtractionOptions inventoryExtraction,
        EmailAutomationOptions emailAutomation)
    {
        if (agentRuntime.Mode != AgentRuntimeOptions.HttpMode ||
            agentRuntime.Provider != AgentRuntimeOptions.BedrockProvider ||
            !agentRuntime.AllowLive)
        {
            throw new InvalidOperationException(
                "Production AI must use the governed live Bedrock HTTP runtime.");
        }
        if (inventoryExtraction.Mode != InventoryExtractionOptions.NativeMode ||
            configuration.GetValue<bool>(
                $"{InventoryProcessingOptions.SectionName}:Paused") ||
            !configuration.GetValue<bool>(
                $"{InventorySemanticOptions.SectionName}:Enabled"))
        {
            throw new InvalidOperationException(
                "Production inventory processing requires native source preprocessing, governed semantic extraction, and an unpaused worker.");
        }
        if (emailAutomation.Mode != EmailAutomationOptions.ResendMode || emailAutomation.ProcessInline)
        {
            throw new InvalidOperationException(
                "Production email automation must use Resend with worker-managed processing.");
        }
        var outbox = configuration.GetSection(OutboxDispatchOptions.SectionName)
            .Get<OutboxDispatchOptions>() ?? new OutboxDispatchOptions();
        if (outbox.Mode != OutboxDispatchOptions.EventBridgeMode)
        {
            throw new InvalidOperationException(
                "Production outbox delivery must use EventBridge.");
        }
    }

    private static bool SafeHttps(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        string.IsNullOrEmpty(uri.UserInfo);
}

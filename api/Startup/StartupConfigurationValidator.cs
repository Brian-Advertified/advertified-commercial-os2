using Advertified.Commercial.Api.Authentication;
using Advertified.Commercial.Infrastructure.EmailAutomation;
using Advertified.Commercial.Infrastructure.Inventory;
using Advertified.Commercial.Infrastructure.Opportunity;
using Advertified.Commercial.Infrastructure.Outbox;

namespace Advertified.Commercial.Api.Startup;

internal static class StartupConfigurationValidator
{
    internal static string ValidateAndGetConnectionString(
        WebApplicationBuilder builder,
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
        EnsureInventoryProtection(inventoryProtection);
        EnsureHttpEdge(configuration);
        if (!releaseSmoke)
        {
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
        if (agentRuntime.UsesHttp && !SafeHttps(agentRuntime.BaseUrl))
        {
            throw new InvalidOperationException(
                "Non-local agent runtime transport must use an HTTPS URL without embedded credentials.");
        }
    }

    private static void EnsureInventoryProtection(
        InventoryProtectionOptions inventoryProtection)
    {
        if (inventoryProtection.ObjectStoreMode == InventoryProtectionOptions.InMemoryMode ||
            inventoryProtection.ScannerMode == InventoryProtectionOptions.DeterministicScanner)
        {
            throw new InvalidOperationException(
                "Deterministic inventory protection is restricted to development and test.");
        }
        if (inventoryProtection.ObjectStoreMode == InventoryProtectionOptions.MinioMode &&
            !inventoryProtection.UseTls)
        {
            throw new InvalidOperationException(
                "Production S3-compatible object storage must require TLS.");
        }
    }

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
        if (inventoryExtraction.Mode != InventoryExtractionOptions.DoclingMode ||
            configuration.GetValue<bool>("InventoryProcessing:Paused"))
        {
            throw new InvalidOperationException(
                "Production inventory extraction must use Docling and processing must not be paused.");
        }
        if (configuration[SuppliedBriefConfiguration.ModeKey] != SuppliedBriefConfiguration.Http)
        {
            throw new InvalidOperationException(
                "Production supplied-Brief understanding must use the HTTP agent runtime.");
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

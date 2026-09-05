using Advertified.Commercial.Api.Authentication;
using Advertified.Commercial.Infrastructure.Inventory;
using Advertified.Commercial.Infrastructure.Opportunity;

namespace Advertified.Commercial.Api.Startup;

internal static class StartupConfigurationValidator
{
    internal static string ValidateAndGetConnectionString(
        WebApplicationBuilder builder,
        string? authenticationMode,
        AgentRuntimeOptions agentRuntime,
        InventoryProtectionOptions inventoryProtection)
    {
        var localEnvironment = builder.Environment.IsDevelopment() ||
            builder.Environment.IsEnvironment("Test");
        EnsureProductionBoundaries(
            builder.Configuration, authenticationMode, agentRuntime,
            inventoryProtection, localEnvironment);

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
        bool localEnvironment)
    {
        if ((authenticationMode is LocalIdentityDefaults.DeterministicMode or
                LocalIdentityDefaults.DeterministicSessionMode) && !localEnvironment)
        {
            throw new InvalidOperationException(
                "Deterministic authentication and sessions are restricted to development and test.");
        }
        if (!localEnvironment && authenticationMode is not (
                LocalIdentityDefaults.DisabledMode or LocalIdentityDefaults.OidcMode))
        {
            throw new InvalidOperationException(
                "Non-local authentication must use OIDC or be explicitly disabled for release smoke checks.");
        }
        if (!localEnvironment &&
            agentRuntime.Mode is AgentRuntimeOptions.InProcessMode or
                AgentRuntimeOptions.HttpDeterministicMode)
        {
            throw new InvalidOperationException(
                "Development-only agent runtime modes are restricted to development and test.");
        }
        if (!localEnvironment && agentRuntime.UsesHttp &&
            (!Uri.TryCreate(agentRuntime.BaseUrl, UriKind.Absolute, out var runtimeUri) ||
             runtimeUri.Scheme != Uri.UriSchemeHttps ||
             !string.IsNullOrEmpty(runtimeUri.UserInfo)))
        {
            throw new InvalidOperationException(
                "Non-local agent runtime transport must use an HTTPS URL without embedded credentials.");
        }
        if ((inventoryProtection.ObjectStoreMode == InventoryProtectionOptions.InMemoryMode ||
                inventoryProtection.ScannerMode == InventoryProtectionOptions.DeterministicScanner) &&
            !localEnvironment)
        {
            throw new InvalidOperationException(
                "Deterministic inventory protection is restricted to development and test.");
        }
        if (!localEnvironment &&
            inventoryProtection.ObjectStoreMode == InventoryProtectionOptions.MinioMode &&
            !inventoryProtection.UseTls)
        {
            throw new InvalidOperationException(
                "Production S3-compatible object storage must require TLS.");
        }
        var allowedHosts = configuration["AllowedHosts"];
        if (!localEnvironment && (string.IsNullOrWhiteSpace(allowedHosts) ||
                allowedHosts.Split(';', StringSplitOptions.RemoveEmptyEntries |
                        StringSplitOptions.TrimEntries)
                    .Any(host => host == "*")))
        {
            throw new InvalidOperationException(
                "Production must configure an explicit AllowedHosts allow-list.");
        }
        if (!localEnvironment &&
            !TrustedProxyConfiguration.HasExplicitTrustBoundary(configuration))
        {
            throw new InvalidOperationException(
                "Production must configure an explicit trusted reverse-proxy boundary.");
        }
        if (!localEnvironment &&
            configuration.GetValue<bool?>(
                $"{BrowserSessionOptions.SectionName}:SecureCookie") is false)
        {
            throw new InvalidOperationException(
                "Production browser sessions must use secure cookies.");
        }
    }
}

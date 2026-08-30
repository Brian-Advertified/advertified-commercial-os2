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
        if ((authenticationMode is LocalIdentityDefaults.DeterministicMode or
                LocalIdentityDefaults.DeterministicSessionMode) && !localEnvironment)
        {
            throw new InvalidOperationException(
                "Deterministic authentication and sessions are restricted to development and test.");
        }
        if (agentRuntime.Mode != AgentRuntimeOptions.DisabledMode && !localEnvironment)
        {
            throw new InvalidOperationException(
                "The deterministic agent runtime is restricted to development and test.");
        }
        if ((inventoryProtection.ObjectStoreMode == InventoryProtectionOptions.InMemoryMode ||
                inventoryProtection.ScannerMode == InventoryProtectionOptions.DeterministicScanner) &&
            !localEnvironment)
        {
            throw new InvalidOperationException(
                "Deterministic inventory protection is restricted to development and test.");
        }

        var connectionString = builder.Configuration.GetConnectionString("CommercialDatabase");
        return string.IsNullOrWhiteSpace(connectionString)
            ? throw new InvalidOperationException(
                "The commercial database connection is not configured.")
            : connectionString;
    }
}

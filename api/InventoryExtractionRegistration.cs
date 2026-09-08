using Advertified.Commercial.Api.Startup;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Inventory;
using Advertified.Commercial.Infrastructure.Opportunity;

namespace Advertified.Commercial.Api;

internal static class InventoryExtractionRegistration
{
    internal static void AddInventoryExtraction(
        this WebApplicationBuilder builder,
        InventoryExtractionOptions settings)
    {
        EnsureEnvironmentIsSafe(builder, settings);
        builder.Services.AddOptions<InventoryProcessingOptions>()
            .Bind(builder.Configuration.GetSection(InventoryProcessingOptions.SectionName));
        builder.Services.AddOptions<InventoryExtractionOptions>()
            .Bind(builder.Configuration.GetSection(InventoryExtractionOptions.SectionName))
            .Validate(InventoryExtractionOptions.HasSupportedMode,
                "The inventory extraction mode is invalid.")
            .ValidateOnStart();
        builder.Services.AddScoped<IInventoryDocumentExtractionAdapter>(
            _ => new NativeInventoryExtractionAdapter());
        builder.Services.AddScoped<InventorySchemaExecutionGuard>();
    }

    internal static void AddInventorySemantic(
        this WebApplicationBuilder builder,
        AgentRuntimeOptions agentRuntime)
    {
        builder.Services.AddOptions<InventorySemanticOptions>()
            .Bind(builder.Configuration.GetSection(
                InventorySemanticOptions.SectionName))
            .Validate(InventorySemanticOptions.IsValid,
                "The inventory semantic extraction limits, prices, budget, scope or prompt version are invalid.")
            .Validate(
                options => !options.Enabled ||
                    (agentRuntime.Mode == AgentRuntimeOptions.HttpMode &&
                     agentRuntime.Provider == AgentRuntimeOptions.BedrockProvider &&
                     agentRuntime.AllowLive &&
                     agentRuntime.ModelFor(
                         MasterDataCodes.AgentTypes.InventoryIntelligence,
                         InventorySemanticOperations.SourceTranscription) !=
                            "fixture-v1" &&
                     agentRuntime.ModelFor(
                         MasterDataCodes.AgentTypes.InventoryIntelligence,
                         InventorySemanticOperations.SemanticEnrichment) !=
                            "fixture-v1"),
                "Inventory semantic extraction requires an explicitly enabled live Bedrock agent route.")
            .ValidateOnStart();
    }

    private static void EnsureEnvironmentIsSafe(
        WebApplicationBuilder builder,
        InventoryExtractionOptions settings)
    {
        if (builder.Environment.IsDevelopment() ||
            builder.Environment.IsEnvironment("Test"))
        {
            return;
        }
        if (settings.Mode == InventoryExtractionOptions.DeterministicMode &&
            !builder.Configuration.GetValue<bool>(
                $"{InventoryProcessingOptions.SectionName}:Paused"))
        {
            throw new InvalidOperationException(
                "Production deterministic extraction is permitted only while inventory processing is paused.");
        }
    }
}

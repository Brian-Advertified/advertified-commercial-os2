using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class InventorySemanticConfigurationGuard
{
    internal static void EnsureLive(
        AgentRuntimeOptions runtime,
        InventorySemanticOptions semantic)
    {
        var expectedCapMinor =
            (semantic.PerCallCostCapUsdMicros + 9_999L) / 10_000L;
        if (!InventorySemanticOptions.IsPlanningValid(semantic) ||
            runtime.Mode != AgentRuntimeOptions.HttpMode ||
            runtime.Provider != AgentRuntimeOptions.BedrockProvider ||
            !runtime.AllowLive ||
            !UsesConfiguredModel(runtime, semantic) ||
            runtime.CostCapFor(MasterDataCodes.AgentTypes.InventoryIntelligence) !=
                expectedCapMinor)
        {
            throw new InvalidOperationException(
                "Semantic extraction requires the exact governed and " +
                "preflighted live route.");
        }
    }

    private static bool UsesConfiguredModel(
        AgentRuntimeOptions runtime,
        InventorySemanticOptions semantic) =>
        ModelMatches(runtime, semantic, InventoryExtractionTraceCodes.SchemaDiscovery) &&
        ModelMatches(runtime, semantic, InventorySemanticOperations.SemanticEnrichment) &&
        ModelMatches(runtime, semantic, InventorySemanticOperations.SourceTranscription);

    private static bool ModelMatches(
        AgentRuntimeOptions runtime,
        InventorySemanticOptions semantic,
        string operation) =>
        string.Equals(
            runtime.ModelFor(
                MasterDataCodes.AgentTypes.InventoryIntelligence,
                operation),
            semantic.ModelId,
            StringComparison.Ordinal);
}

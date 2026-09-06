using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class AgentRuntimeModelRoutingTests
{
    private const string ClaudeSonnet46 =
        "global.anthropic.claude-sonnet-4-6";
    private const string AmazonNova2Lite =
        "global.amazon.nova-2-lite-v1:0";
    private const string SuppliedBriefOperation =
        "SUPPLIED_BRIEF_UNDERSTANDING";
    private const string SchemaDiscoveryOperation =
        "SCHEMA_DISCOVERY";
    private const string SemanticEnrichmentOperation =
        "SEMANTIC_ENRICHMENT";

    [Fact]
    public void BedrockRoutesReasoningStagesToClaudeSonnet46()
    {
        var settings = BedrockSettings();
        var agents = new[]
        {
            MasterDataCodes.AgentTypes.BusinessInterpretation,
            MasterDataCodes.AgentTypes.OpportunityIntelligence,
            MasterDataCodes.AgentTypes.Strategy,
            MasterDataCodes.AgentTypes.CriticReadiness,
            MasterDataCodes.AgentTypes.BriefDrafting,
            MasterDataCodes.AgentTypes.Audience,
            MasterDataCodes.AgentTypes.InventoryIntelligence,
            MasterDataCodes.AgentTypes.MediaPlanning,
            MasterDataCodes.AgentTypes.ProposalNarrative,
            MasterDataCodes.AgentTypes.Creative,
            MasterDataCodes.AgentTypes.Measurement,
        };

        Assert.All(
            agents,
            agent => Assert.Equal(
                ClaudeSonnet46,
                settings.ModelFor(agent)));
        Assert.Equal(
            ClaudeSonnet46,
            settings.ModelFor(
                MasterDataCodes.AgentTypes.BriefDrafting,
                SuppliedBriefOperation));
    }

    [Fact]
    public void BedrockRoutesBoundedInventoryExtractionToNova2Lite()
    {
        var settings = BedrockSettings();

        Assert.Equal(
            AmazonNova2Lite,
            settings.ModelFor(
                MasterDataCodes.AgentTypes.InventoryIntelligence,
                SchemaDiscoveryOperation));
        Assert.Equal(
            AmazonNova2Lite,
            settings.ModelFor(
                MasterDataCodes.AgentTypes.InventoryIntelligence,
                SemanticEnrichmentOperation));
    }

    [Fact]
    public void OperationRouteIsSealedIntoInvocationProviderPolicy()
    {
        var settings = BedrockSettings();

        var invocation = AgentRuntimeHttpSupport.CreateInvocation(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            MasterDataCodes.AgentTypes.InventoryIntelligence,
            "InventoryImport",
            Guid.NewGuid(),
            1,
            [],
            settings,
            modelOperation: SemanticEnrichmentOperation);

        Assert.Equal(
            AmazonNova2Lite,
            invocation.ProviderPolicy.Model);
        Assert.Equal(1, invocation.ProviderPolicy.MaxAttempts);
    }

    [Fact]
    public void UnconfiguredOperationAndMissingRouteFailClosed()
    {
        var settings = BedrockSettings();
        Assert.Throws<InvalidOperationException>(() =>
            settings.ModelFor(
                MasterDataCodes.AgentTypes.InventoryIntelligence,
                "SOURCE_TRANSCRIPTION"));

        settings.Models.Remove(
            MasterDataCodes.AgentTypes.BusinessInterpretation);

        Assert.False(
            AgentRuntimeOptions.HasSafeProviderPolicy(settings));
        Assert.Throws<InvalidOperationException>(() =>
            AgentRuntimeHttpSupport.CreateInvocation(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                MasterDataCodes.AgentTypes.BusinessInterpretation,
                "Opportunity",
                Guid.NewGuid(),
                1,
                [],
                settings));
    }

    [Fact]
    public void DeterministicRuntimeNeverUsesLiveModelAssignments()
    {
        var settings = new AgentRuntimeOptions
        {
            Provider = AgentRuntimeOptions.DeterministicProvider,
            Models = BedrockModels(),
        };

        Assert.Equal(
            "fixture-v1",
            settings.ModelFor(
                MasterDataCodes.AgentTypes.InventoryIntelligence,
                SemanticEnrichmentOperation));
    }

    private static AgentRuntimeOptions BedrockSettings() => new()
    {
        Mode = AgentRuntimeOptions.HttpMode,
        Provider = AgentRuntimeOptions.BedrockProvider,
        DefaultCostCapMinor = 1,
        AllowLive = true,
        Models = BedrockModels(),
    };

    private static Dictionary<string, string> BedrockModels()
    {
        var result = new Dictionary<string, string>(
            StringComparer.Ordinal);
        foreach (var agent in new[]
        {
            MasterDataCodes.AgentTypes.BusinessInterpretation,
            MasterDataCodes.AgentTypes.OpportunityIntelligence,
            MasterDataCodes.AgentTypes.Strategy,
            MasterDataCodes.AgentTypes.CriticReadiness,
            MasterDataCodes.AgentTypes.BriefDrafting,
            MasterDataCodes.AgentTypes.Audience,
            MasterDataCodes.AgentTypes.InventoryIntelligence,
            MasterDataCodes.AgentTypes.MediaPlanning,
            MasterDataCodes.AgentTypes.ProposalNarrative,
            MasterDataCodes.AgentTypes.Creative,
            MasterDataCodes.AgentTypes.Measurement,
        })
        {
            result[agent] = ClaudeSonnet46;
        }
        result[AgentRuntimeOptions.ModelRoute(
            MasterDataCodes.AgentTypes.BriefDrafting,
            SuppliedBriefOperation)] =
            ClaudeSonnet46;
        result[AgentRuntimeOptions.ModelRoute(
            MasterDataCodes.AgentTypes.InventoryIntelligence,
            SchemaDiscoveryOperation)] =
            AmazonNova2Lite;
        result[AgentRuntimeOptions.ModelRoute(
            MasterDataCodes.AgentTypes.InventoryIntelligence,
            SemanticEnrichmentOperation)] =
            AmazonNova2Lite;
        return result;
    }
}

using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class AgentRuntimeModelRoutingTests
{
    private const string AmazonNovaLite =
        "amazon.nova-lite-v1:0";
    private const string SuppliedBriefOperation =
        "SUPPLIED_BRIEF_UNDERSTANDING";
    private const string SchemaDiscoveryOperation =
        "SCHEMA_DISCOVERY";
    private const string SemanticEnrichmentOperation =
        "SEMANTIC_ENRICHMENT";
    private const string SourceTranscriptionOperation =
        "SOURCE_TRANSCRIPTION";

    [Fact]
    public void BedrockRoutesEveryStageToApprovedNovaLite()
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
                AmazonNovaLite,
                settings.ModelFor(agent)));
        Assert.Equal(
            AmazonNovaLite,
            settings.ModelFor(
                MasterDataCodes.AgentTypes.BriefDrafting,
                SuppliedBriefOperation));
    }

    [Fact]
    public void BedrockRoutesBoundedInventoryExtractionToNovaLite()
    {
        var settings = BedrockSettings();

        Assert.Equal(
            AmazonNovaLite,
            settings.ModelFor(
                MasterDataCodes.AgentTypes.InventoryIntelligence,
                SchemaDiscoveryOperation));
        Assert.Equal(
            AmazonNovaLite,
            settings.ModelFor(
                MasterDataCodes.AgentTypes.InventoryIntelligence,
                SemanticEnrichmentOperation));
        Assert.Equal(
            AmazonNovaLite,
            settings.ModelFor(
                MasterDataCodes.AgentTypes.InventoryIntelligence,
                SourceTranscriptionOperation));
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
            AmazonNovaLite,
            invocation.ProviderPolicy.Model);
        Assert.Equal(1, invocation.ProviderPolicy.MaxAttempts);
    }

    [Fact]
    public void UnconfiguredOperationAndMissingRouteFailClosed()
    {
        var settings = BedrockSettings();
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
            result[agent] = AmazonNovaLite;
        }
        result[AgentRuntimeOptions.ModelRoute(
            MasterDataCodes.AgentTypes.BriefDrafting,
            SuppliedBriefOperation)] =
            AmazonNovaLite;
        result[AgentRuntimeOptions.ModelRoute(
            MasterDataCodes.AgentTypes.InventoryIntelligence,
            SchemaDiscoveryOperation)] =
            AmazonNovaLite;
        result[AgentRuntimeOptions.ModelRoute(
            MasterDataCodes.AgentTypes.InventoryIntelligence,
            SourceTranscriptionOperation)] =
            AmazonNovaLite;
        result[AgentRuntimeOptions.ModelRoute(
            MasterDataCodes.AgentTypes.InventoryIntelligence,
            SemanticEnrichmentOperation)] =
            AmazonNovaLite;
        return result;
    }
}

using Advertified.Commercial.Application.Intelligence;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Api.Tests;

internal sealed class MediaStrategyIntelligenceAgentFixture : IMediaStrategyIntelligenceAgentClient
{
    public Task<MediaStrategyIntelligenceProposal> AnalyseAsync(
        MediaStrategyIntelligenceInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (input.AvailableChannels.Count == 0)
            throw new InvalidOperationException("The deterministic Media Strategy fixture requires governed channels.");

        var selected = input.AvailableChannels[0];
        var recommendations = new[]
        {
            new MediaChannelRecommendationProposal(
                selected,
                "Primary governed channel for the approved campaign scope.",
                "Use the approved campaign mode and audience as planning boundaries; effectiveness is not asserted without measurement evidence.",
                "Support the approved objective without introducing an unapproved channel claim.",
                null,
                MasterDataCodes.EvidenceClassifications.Hypothesis,
                100m,
                ["Channel effectiveness is not established by this recommendation alone."],
                ["Performance and delivery evidence remain to be established during planning and measurement."])
        };
        var excluded = input.AvailableChannels.Skip(1).ToArray();
        var proposal = new MediaStrategyIntelligenceProposal(
            "A governed zero-inventory media strategy based on the approved Brief, audience and campaign mode.",
            recommendations,
            ["Keep strategic channel choice separate from supplier inventory selection."],
            excluded,
            ["Channel performance is not established until measured or supported by approved evidence."],
            [],
            [],
            "The deterministic fixture preserves the approved commercial boundaries and makes no inventory or performance claims.",
            new IntelligenceInvocationUsage(
                MasterDataCodes.AgentTypes.MediaStrategy,
                "deterministic",
                "fixture-v1",
                0,
                "FIXTURE",
                null,
                0,
                0,
                0));
        return Task.FromResult(proposal);
    }
}

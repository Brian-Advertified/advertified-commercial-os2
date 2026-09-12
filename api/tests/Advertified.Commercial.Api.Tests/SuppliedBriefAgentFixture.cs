using Advertified.Commercial.Application.Brief;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Api.Tests;

internal sealed class SuppliedBriefAgentFixture(
    Func<SuppliedBriefAgentInput, SuppliedBriefUnderstandingView>? output = null)
    : ISuppliedBriefAgentClient
{
    public Task<SuppliedBriefUnderstandingView> UnderstandAsync(
        SuppliedBriefAgentInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(
            output?.Invoke(input) ?? Create(input));
    }

    internal static SuppliedBriefUnderstandingView Create(
        SuppliedBriefAgentInput input,
        string campaignMode = MasterDataCodes.CampaignModes.FullCampaign)
    {
        var evidence = new SuppliedBriefEvidenceView(
            SuppliedBriefFieldPaths.CampaignMode,
            MasterDataCodes.EvidenceClassifications.Fact,
            input.SourceContent[..Math.Min(input.SourceContent.Length, 4_000)],
            1m,
            "supplied:brief/current");
        var draft = new SuppliedBriefDraftView(
            "The supplied request requires a campaign response.",
            "Raise awareness",
            ["Adults"],
            ["Gauteng"],
            "October 2026",
            10_000_000,
            false,
            MasterDataCodes.Currencies.Zar,
            MasterDataCodes.VatStatuses.Registered,
            null,
            campaignMode == MasterDataCodes.CampaignModes.OohOnly
                ? [MasterDataCodes.Channels.Ooh]
                : [MasterDataCodes.Channels.Radio, MasterDataCodes.Channels.Digital],
            [],
            [],
            [evidence.Excerpt],
            [],
            [],
            []);
        return new SuppliedBriefUnderstandingView(
            "Synthetic Brand",
            input.SourceTitle,
            campaignMode,
            1m,
            false,
            "Explicit test fixture selection.",
            draft,
            [],
            [evidence],
            new SuppliedBriefAgentUsageView(
                "deterministic",
                "fixture-v1",
                "test-fixture-v1",
                "NOT_REQUESTED",
                0,
                0));
    }
}

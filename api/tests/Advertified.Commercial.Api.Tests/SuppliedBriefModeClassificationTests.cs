using Advertified.Commercial.Application.Brief;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Brief;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class SuppliedBriefModeClassificationTests
{
    [Theory]
    [InlineData("ONLY DIGITAL LARGE FORMAT SITES (NO 3 X 6)")]
    [InlineData("ONLY ICONIC STATIC SITES in Sandton, Ballito and Cape Town")]
    [InlineData("Kindly share your sites for townships static / digital")]
    [InlineData("Please share venues with static branding and/or DOOH opportunities")]
    [InlineData("Identify suitable out-of-home media opportunities such as wall murals")]
    public async Task RealOohBuyingLanguageIsDirectOohOnlyEvidence(string mediaInstruction)
    {
        var result = await UnderstandAsync(mediaInstruction);

        Assert.Equal(MasterDataCodes.CampaignModes.OohOnly, result.CampaignMode);
        Assert.Equal(1m, result.CampaignModeConfidence);
        var evidence = Assert.Single(result.Evidence, item => item.FieldPath == "campaignMode");
        Assert.Equal(MasterDataCodes.EvidenceClassifications.Fact, evidence.Kind);
        Assert.All(result.Draft.MediaRequirements, channel => Assert.Contains(
            channel,
            new[] { MasterDataCodes.Channels.Ooh, MasterDataCodes.Channels.Dooh }));
    }

    [Theory]
    [InlineData("Media: OOH preferred; open to other channels")]
    [InlineData("Please consider billboards, but the campaign may include other media")]
    public async Task NonExclusiveOohWordingRequiresCampaignModeConfirmation(string instruction)
    {
        var result = await UnderstandAsync(instruction);

        Assert.Null(result.CampaignMode);
        Assert.Contains(result.Questions, question =>
            question.FieldPath == "campaignMode" && question.IsBlocking);
    }

    [Fact]
    public async Task UngovernedWifiServiceInOohRequestRequiresScopeClarification()
    {
        var result = await UnderstandAsync(
            "Please share OOH sites and possible WIFI solutions at Home Affairs offices");

        Assert.Null(result.CampaignMode);
        Assert.Contains(result.Questions, question =>
            question.FieldPath == "campaignMode" && question.IsBlocking);
    }

    [Fact]
    public async Task ExplicitMixedMediaRequirementIsFullCampaignEvidence()
    {
        var result = await UnderstandAsync("Media: OOH billboards and radio");

        Assert.Equal(MasterDataCodes.CampaignModes.FullCampaign, result.CampaignMode);
        Assert.Equal(1m, result.CampaignModeConfidence);
        var evidence = Assert.Single(result.Evidence, item => item.FieldPath == "campaignMode");
        Assert.Equal(MasterDataCodes.EvidenceClassifications.Fact, evidence.Kind);
    }

    private static Task<SuppliedBriefUnderstandingView> UnderstandAsync(string instruction)
    {
        var client = new DeterministicSuppliedBriefAgentClient(
            SuppliedBriefAgentPolicy.Load());
        return client.UnderstandAsync(
            new SuppliedBriefAgentInput(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Supplied OOH campaign Brief",
                instruction,
                Array.Empty<BriefClarificationInput>()),
            CancellationToken.None);
    }
}

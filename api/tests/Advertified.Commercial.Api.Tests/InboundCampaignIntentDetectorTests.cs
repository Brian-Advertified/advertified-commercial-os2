using Advertified.Commercial.Infrastructure.EmailAutomation;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class InboundCampaignIntentDetectorTests
{
    [Fact]
    public void MultipleExplicitBriefsCannotBeBlendedIntoOneCampaign()
    {
        const string source = """
            **BRIEF 1**
            Jameson Select - only digital large format sites

            **BRIEF 2**
            Codigo - only iconic static sites
            """;

        Assert.True(InboundCampaignIntentDetector.ContainsMultipleExplicitBriefs(source));
    }

    [Theory]
    [InlineData("Brief 1: One OOH campaign")]
    [InlineData("Please share one OOH proposal")]
    [InlineData("The Brief has two format sections")]
    public void OneCampaignIntentDoesNotTriggerBatchReview(string source)
    {
        Assert.False(InboundCampaignIntentDetector.ContainsMultipleExplicitBriefs(source));
    }
}

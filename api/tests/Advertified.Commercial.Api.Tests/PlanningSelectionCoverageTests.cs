using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Infrastructure.Planning;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class PlanningSelectionCoverageTests
{
    [Fact]
    public void RequiredMediaMixChannelsMustAllBeSelected()
    {
        var error = Assert.Throws<InvalidLifecycleTransitionException>(() =>
            PlanningSelectionCoverage.EnsureChannels(
                ["DIGITAL", "OOH"],
                ["DIGITAL", "OOH", "SOCIAL"]));

        Assert.NotNull(error);
    }

    [Fact]
    public void EveryRequiredMediaMixChannelCanBeSelected()
    {
        PlanningSelectionCoverage.EnsureChannels(
            ["DIGITAL", "OOH", "SOCIAL"],
            ["DIGITAL", "OOH", "SOCIAL"]);
    }
}

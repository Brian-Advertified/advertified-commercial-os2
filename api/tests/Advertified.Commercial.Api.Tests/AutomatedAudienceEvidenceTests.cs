using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Infrastructure.EmailAutomation;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class AutomatedAudienceEvidenceTests
{
    [Theory]
    [InlineData(null, null, null, false, false, true)]
    [InlineData(null, null, null, true, false, false)]
    [InlineData(0, 80, null, false, false, false)]
    [InlineData(80, 0, null, false, false, false)]
    [InlineData(80, 80, null, false, true, false)]
    [InlineData(80, 80, 0, false, false, false)]
    [InlineData(80, 80, 70, false, true, true)]
    public void AutomaticSelectionRequiresEvidenceForRequestedAudienceComponents(
        int? language, int? lifeStage, int? segment, bool missingEvidence,
        bool mandatorySegment, bool expected)
    {
        var fit = new InventoryAudienceFitView(
            language / 100m, lifeStage / 100m, segment / 100m,
            missingEvidence ? ["inventory.audienceProfile"] : [],
            LsmSemMandatory: mandatorySegment);

        Assert.Equal(expected, EmailAutomationInventorySelector.AudienceEvidenceReady(fit));
    }
}

using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Infrastructure.Inventory;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class DoclingInventoryExtractionAdapterTests
{
    [Fact]
    public void CandidateAdmissionSeparatesEvidenceFragmentsFromInventory()
    {
        var rows = new[]
        {
            Row(1, "docling:page=1;table=1;row=2",
                ("area", "GARDENS"), ("47m", "49m")),
            Row(2, "docling:page=2;table=1;row=2",
                ("name", "Metro FM 06:00-07:00"),
                ("rate", "R34 260")),
            Row(3, "pptx:slide=2",
                ("productcode", "WCD001")),
            Row(4, "docling:page=1;table=1;row=4",
                ("name", "TOTAL VALUE"),
                ("rate", "R448 560")),
            Row(5, "docling:page=3;block=7",
                ("name", "NATURALLY"),
                ("channel", "OOH"),
                ("producttype", "OOH_SITE")),
        };

        var candidates = InventoryCandidateAdmissionPolicy.Prepare(
            rows,
            new string('a', 64),
            "Not supplied",
            EmptyCodes(),
            DateTimeOffset.UnixEpoch);

        Assert.Equal(2, candidates.Length);
        Assert.DoesNotContain(
            candidates,
            candidate => candidate.SourceLocator.Contains(
                "page=1", StringComparison.Ordinal));
    }

    private static InventoryExtractedRow Row(
        int number,
        string locator,
        params (string Key, string Value)[] values) =>
        new(
            number,
            locator,
            values.ToDictionary(
                item => item.Key,
                item => item.Value,
                StringComparer.Ordinal));
}

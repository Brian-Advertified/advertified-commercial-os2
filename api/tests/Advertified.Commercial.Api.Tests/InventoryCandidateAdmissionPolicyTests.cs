using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Infrastructure.Inventory;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class InventoryCandidateAdmissionPolicyTests
{
    [Fact]
    public void CandidateAdmissionSeparatesEvidenceFragmentsFromInventory()
    {
        var rows = new[]
        {
            Row(1, "source:page=1;table=1;row=2",
                ("area", "GARDENS"), ("47m", "49m")),
            Row(2, "source:page=2;table=1;row=2",
                ("name", "Metro FM 06:00-07:00"),
                ("rate", "R34 260")),
            Row(3, "pptx:slide=2",
                ("productcode", "WCD001")),
            Row(4, "source:page=1;table=1;row=4",
                ("name", "TOTAL VALUE"),
                ("rate", "R448 560")),
            Row(5, "source:page=3;block=7",
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
        Assert.DoesNotContain(candidates, candidate =>
            candidate.SourceLocator.Contains("page=1", StringComparison.Ordinal));
        Assert.DoesNotContain(candidates, candidate =>
            candidate.SourceLocator.Contains("row=4", StringComparison.Ordinal) &&
            candidate.Values.Name == "TOTAL VALUE");
    }

    [Theory]
    [InlineData("TOTAL VALUE")]
    [InlineData("TOTAL INVESTMENT")]
    [InlineData("TOTAL INVOICE")]
    [InlineData("TOTAL EXCL VAT")]
    [InlineData("TOTAL NO OF SPOTS")]
    [InlineData("SUBTOTAL")]
    [InlineData("SUB TOTALS")]
    [InlineData("SAVINGS")]
    [InlineData("VAT")]
    [InlineData("LESS DISCOUNT")]
    [InlineData("COST PER MONTH")]
    public void CandidateAdmissionRejectsPricedCommercialSummaryLabels(
        string label)
    {
        var candidates = InventoryCandidateAdmissionPolicy.Prepare(
            [Row(1, "source:page=1;table=1;row=1",
                ("name", label), ("rate", "R448 560"))],
            new string('a', 64),
            "Not supplied",
            EmptyCodes(),
            DateTimeOffset.UnixEpoch);

        Assert.Empty(candidates);
    }

    [Theory]
    [InlineData("RATE/No of weeks")]
    [InlineData("SUB TOTALS")]
    [InlineData("TOTAL NO OF SPOTS")]
    [InlineData("SIGNATURE")]
    [InlineData("SIGNATURE LESS DISCOUNT 247 890,00 R")]
    [InlineData("VAT 37 183,50 R")]
    [InlineData("DATE")]
    [InlineData("A")]
    [InlineData("G H")]
    public void CandidateAdmissionRejectsNonProductCodeOnlyRows(string code)
    {
        var candidates = InventoryCandidateAdmissionPolicy.Prepare(
            [Row(1, "source:page=2;table=6;row=3", ("product_code", code))],
            new string('a', 64),
            "Not supplied",
            EmptyCodes(),
            DateTimeOffset.UnixEpoch);

        Assert.Empty(candidates);
    }

    [Fact]
    public void CandidateAdmissionPreservesPricedPackageAndRelationships()
    {
        var candidates = InventoryCandidateAdmissionPolicy.Prepare(
            [Row(1, "source:page=1;table=1;row=1",
                ("name", "TOTAL INVESTMENT"),
                ("rate", "R224 280"),
                ("packagename", "Plan A"),
                ("packagecode", "ALGOA-PLAN-A"),
                ("packagecomponents", "GENERIC-30;REPORT-SPONSORSHIP"),
                ("packagediscountrule", "50% saving from R448 560 total value"))],
            new string('a', 64),
            "Not supplied",
            EmptyCodes(),
            DateTimeOffset.UnixEpoch);

        var candidate = Assert.Single(candidates);
        Assert.Equal(22_428_000, candidate.Values.RateAmountMinor);
        Assert.Equal("Plan A", candidate.Values.Package?.PackageName);
        Assert.Equal(
            ["GENERIC-30", "REPORT-SPONSORSHIP"],
            candidate.Values.Package?.ComponentProductCodes);
        Assert.Equal(
            "50% saving from R448 560 total value",
            candidate.Values.Package?.DiscountRule);
        Assert.Equal(2, candidate.Values.PackageComponents?.Count);
        Assert.All(candidate.Values.PackageComponents!, component =>
            Assert.Equal("source:page=1;table=1;row=1", component.SourceLocator));
        Assert.Single(candidate.Values.Discounts!);
    }

    [Fact]
    public void CandidateAdmissionDoesNotInferPackageIdentityWithoutSourceEvidence()
    {
        var locator = "source:page=1;table=1;row=";
        var candidates = InventoryCandidateAdmissionPolicy.Prepare(
            [
                Row(1, locator + "2", ("name", "Generic spots"), ("rate", "R291 060")),
                Row(2, locator + "3", ("name", "Report sponsorship"), ("rate", "R157 500")),
                Row(3, locator + "4", ("name", "TOTAL VALUE"), ("rate", "R448 560")),
                Row(4, locator + "5", ("name", "TOTAL INVESTMENT"), ("rate", "R224 280")),
                Row(5, locator + "6", ("name", "SAVINGS"), ("rate", "R224 280")),
            ],
            new string('a', 64),
            "Not supplied",
            EmptyCodes(),
            DateTimeOffset.UnixEpoch);

        Assert.Equal(2, candidates.Length);
        Assert.All(candidates, candidate => Assert.Null(candidate.Values.Package));
    }

    private static InventoryCodeSets EmptyCodes()
    {
        var empty = new HashSet<string>(StringComparer.Ordinal);
        return new(
            empty, empty, empty, empty, empty,
            empty, empty, empty, empty);
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

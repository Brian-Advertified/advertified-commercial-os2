using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Inventory;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class InventoryProjectionMergeRegressionTests
{
    private static readonly string SourceHash = new('a', 64);

    [Theory]
    [InlineData("currency", "ZAR", "USD")]
    [InlineData("ratetype", MasterDataCodes.RateTypes.MonthRate, MasterDataCodes.RateTypes.SpotRate)]
    [InlineData("geography", "Area A", "Area B")]
    public void MateriallyDifferentRowsAreNotCollapsed(string field, string first, string second)
    {
        var native = Row("xlsx:sheet=A;row=2", (field, first));
        var provider = Row("docling:table=1;row=2", (field, second));
        var rows = Merge([native], [provider]);
        Assert.Equal(2, rows.Length);
        Assert.Equal(new[] { first, second }, rows.Select(row => row.Values[field]));
    }

    [Fact]
    public void AmbiguousRepeatedRowsAcrossSheetsRemainSeparateAndRequireReview()
    {
        var rows = Merge([Row("xlsx:sheet=A;row=2"), Row("xlsx:sheet=B;row=2")],
            [Row("docling:table=1;row=2", ("address", "Supplement address"))]);
        Assert.Equal(3, rows.Length);
        Assert.All(rows, row => Assert.True(row.Values.ContainsKey(InventoryProjectionRowMatch.AmbiguityField)));
        Assert.All(rows.Take(2), row => Assert.False(row.Values.ContainsKey("address")));
    }

    [Fact]
    public void ExactPhysicalBindingEnrichesOnlyItsOwnRow()
    {
        var first = Row("xlsx:sheet=A;row=2");
        var second = Row("xlsx:sheet=B;row=2");
        var supplement = Row(second.Locator, ("address", "Source address"));
        var rows = Merge([first, second], [supplement]);
        Assert.Equal(2, rows.Length);
        Assert.False(rows[0].Values.ContainsKey("address"));
        Assert.Equal("Source address", rows[1].Values["address"]);
    }

    private static InventoryExtractedRow[] Merge(InventoryExtractedRow[] native, InventoryExtractedRow[] provider) =>
        NativeOfficeInventoryProjection.Merge(native, provider, SourceHash, MasterDataCodes.DocumentClasses.Xlsx);

    private static InventoryExtractedRow Row(string locator, params (string Field, string Value)[] supplied)
    {
        var values = new Dictionary<string, string> { ["name"] = "Shared placement", ["rate"] = "100.00" };
        foreach (var (field, value) in supplied) values[field] = value;
        return new InventoryExtractedRow(1, locator, values);
    }
}

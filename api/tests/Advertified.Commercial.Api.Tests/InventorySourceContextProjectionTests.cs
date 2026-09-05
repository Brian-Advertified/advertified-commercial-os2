using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Inventory;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class InventorySourceContextProjectionTests
{
    [Theory]
    [InlineData("Y PACKAGES ONE PAGER.pdf")]
    [InlineData("Arena-Business-Day-Rate-Sheet-2026-Repro.pdf")]
    [InlineData("Unfamiliar supplier.pdf")]
    public void FileNameCannotSupplyMissingCommercialFacts(string fileName)
    {
        var request = new InventoryExtractionRequest(
            fileName,
            "application/pdf",
            MasterDataCodes.DocumentClasses.Pdf,
            new string('a', 64),
            []);
        var provider = InventoryExtractionContract.Create(
            "test",
            "test/1.0.0",
            InventoryExtractionOptions.CurrentSchemaVersion,
            request.SourceHash,
            "{}",
            [new InventoryExtractedRow(
                1,
                "pdf:page=1;block=1",
                new Dictionary<string, string>
                {
                    ["name"] = "Source placement",
                })]);

        var projected = NativeOfficeInventoryProjection.Apply(request, provider);
        var row = Assert.Single(projected.Rows);

        Assert.False(row.Values.ContainsKey("supplier"));
        Assert.False(row.Values.ContainsKey("channel"));
        Assert.False(row.Values.ContainsKey("productcode"));
        Assert.False(row.Values.ContainsKey("rateunknown"));
    }
}

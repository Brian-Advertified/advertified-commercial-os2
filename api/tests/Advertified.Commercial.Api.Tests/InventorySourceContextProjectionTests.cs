using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Inventory;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class InventorySourceContextProjectionTests
{
    [Theory]
    [InlineData("Y PACKAGES ONE PAGER.pdf", "YFM")]
    [InlineData("Arena-Business-Day-Rate-Sheet-2026-Repro.pdf", "Arena Holdings")]
    [InlineData("JAC Rate Card_2026.pdf", "Jacaranda FM")]
    [InlineData("Reveel - ZA - Publisher Media Kit.pptx", "Reveel")]
    public void SupplierIsDerivedFromTheRetainedPhysicalFileName(
        string fileName,
        string expectedSupplier)
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

        var projected = InventorySourceContextProjection.Apply(
            request,
            provider);
        var row = Assert.Single(projected.Rows);

        Assert.Equal(expectedSupplier, row.Values["supplier"]);
        Assert.Equal(
            "source:file-name",
            row.FieldLocators!["supplier"]);
        Assert.Equal(
            MasterDataCodes.InventoryEvidenceBases.DerivedPolicy,
            row.FieldEvidenceBases!["supplier"]);
    }
}

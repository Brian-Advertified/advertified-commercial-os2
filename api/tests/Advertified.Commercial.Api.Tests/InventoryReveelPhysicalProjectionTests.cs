using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Inventory;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class DoclingInventoryExtractionAdapterTests
{
    [Fact]
    public void ReveelMixedCasePhysicalSiteCodeIsPreservedAndNormalized()
    {
        var sourceHash = new string('9', 64);
        var request = new InventoryExtractionRequest(
            "Reveel - ZA - Publisher Media Kit.pptx",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            MasterDataCodes.DocumentClasses.Pptx,
            sourceHash,
            [1]);
        var row = new InventoryExtractedRow(
            1,
            "pptx:slide=2;shape=4",
            new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["name"] = "Rv034 The Maslow Dagieos Sandton",
            },
            MasterDataCodes.InventoryExtractionMethods.Ocr,
            0.95m,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["name"] = "pptx:slide=2;shape=4",
            });
        var provider = InventoryExtractionContract.Create(
            "docling",
            "test",
            InventoryExtractionOptions.CurrentSchemaVersion,
            sourceHash,
            "{}",
            [row]);
        var contextual = InventorySourceContextProjection.Apply(request, provider);

        var candidate = InventoryCandidateNormalizer.Normalize(
            Assert.Single(contextual.Rows),
            sourceHash,
            DateTimeOffset.UnixEpoch);

        Assert.Equal("Reveel", candidate.SupplierName);
        Assert.Equal("RV034", candidate.Values.ProductCode);
        Assert.Equal(
            "Rv034 The Maslow Dagieos Sandton",
            candidate.Values.Name);
        Assert.Contains(candidate.Evidence, item =>
            item.FieldName == "product_code" &&
            item.RawValue == "Rv034 The Maslow Dagieos Sandton" &&
            item.NormalizedValue == "RV034" &&
            item.SourceLocator == "pptx:slide=2;shape=4");
    }
}

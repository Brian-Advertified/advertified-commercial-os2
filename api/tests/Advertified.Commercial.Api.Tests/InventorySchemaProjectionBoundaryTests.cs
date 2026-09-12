using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Infrastructure.Inventory;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class InventorySchemaProjectionBoundaryTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RecordFieldNeverReadsExcludedOrOutOfBoundarySource(bool excluded)
    {
        var document = Document();
        var boundary = new InventoryRecordBoundary(
            2, excluded ? 3 : 2, 2, excluded ? [3] : [],
            excluded ? new Dictionary<int, string> { [3] = "Context note, not a record value." } : null);
        var proposal = Proposal(document, boundary, "Rate");
        var rows = InventorySchemaProjection.Project(document, proposal, GovernedCodes());

        var row = Assert.Single(rows);
        Assert.Equal("UNSEEN-41", Assert.Single(row.DiscoveredFields!,
            field => field.CanonicalMeaning == "product_code").RawValue);
        Assert.DoesNotContain(row.DiscoveredFields!, field => field.CanonicalMeaning == "rate");
        Assert.DoesNotContain(row.DiscoveredFields!, field => field.SourceLocator == "context-value");
    }

    [Fact]
    public void EmptyCitationCannotEstablishSourceGrounding()
    {
        var document = Document();
        var proposal = Proposal(document, new InventoryRecordBoundary(2, 3, 2, []), "");

        Assert.Throws<InventorySchemaRejectedException>(() =>
            InventorySchemaProjection.Project(document, proposal, GovernedCodes()));
    }

    private static InventoryDocumentStructure Document() => new(
        new string('a', 64), new string('b', 64),
        [new InventorySourceStructure("unseen-structure", "table",
        [
            new("product-label", 1, 1, "Product"),
            new("rate-label", 1, 2, "Rate"),
            new("product-value", 2, 1, "UNSEEN-41"),
            new("context-value", 3, 2, "999999"),
        ])]);

    private static InventorySchemaProposal Proposal(
        InventoryDocumentStructure document, InventoryRecordBoundary boundary, string rateQuote) => new(
        "inventory-schema/1.0", document.SourceHash, document.StructureHash,
        [new InventoryRecordSchema("unseen-structure", boundary,
        [
            Mapping("product_code", "Product", "product-label", 1, 0, "Product"),
            Mapping("rate", "Rate", "rate-label", 2, 1, rateQuote),
        ], [], [])], 1m, []);

    private static InventorySchemaFieldMapping Mapping(
        string meaning, string label, string locator, int column, int offset, string quote) => new(
        meaning, label, locator, "unseen-structure", column, offset, false,
        "Binding from source header.", 1m, [new InventorySchemaCitation(locator, quote)]);

    private static Dictionary<string, IReadOnlySet<string>> GovernedCodes() =>
        new Dictionary<string, IReadOnlySet<string>>();
}

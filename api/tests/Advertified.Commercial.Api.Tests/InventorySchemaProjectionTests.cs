using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Infrastructure.Inventory;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class InventorySchemaProjectionTests
{
    [Fact]
    public void ReusableSchemaProjectsEveryRecordFromExactSourceCells()
    {
        var document = Document(includeUnmappedNote: false);
        var record = Record();
        var proposal = Proposal(document, record);

        var rows = InventorySchemaProjection.Project(
            document, proposal, GovernedCodes());

        Assert.Equal(2, rows.Count);
        Assert.Equal("SITE-A", Field(rows[0], "product_code").RawValue);
        Assert.Equal("125000", Field(rows[0], "rate").RawValue);
        Assert.Equal("SITE-B", Field(rows[1], "product_code").RawValue);
        Assert.Equal("98000", Field(rows[1], "rate").RawValue);
        Assert.Equal("ZAR", Field(rows[1], "currency").RawValue);
        Assert.Equal("OOH", Field(rows[1], "channel").RawValue);
        Assert.Equal(
            InventoryAcceptanceCheckResult.Passed,
            InventoryAcceptanceSourceAccounting.AccountSection(
                document, record, rows).Result);
    }

    [Fact]
    public void UngovernedInterpretedCodeIsRejected()
    {
        var document = Document(includeUnmappedNote: false);
        var record = Record() with
        {
            FieldMappings = Record().FieldMappings.Select(mapping =>
                mapping.CanonicalMeaning == "currency"
                    ? mapping with { InterpretedCode = "USD" }
                    : mapping).ToArray(),
        };

        Assert.Throws<InventorySchemaRejectedException>(() =>
            InventorySchemaProjection.Project(
                document, Proposal(document, record), GovernedCodes()));
    }

    [Fact]
    public void UnmappedSourceContentFailsAccountingInsteadOfDisappearing()
    {
        var document = Document(includeUnmappedNote: true);
        var record = Record();
        var rows = InventorySchemaProjection.Project(
            document, Proposal(document, record), GovernedCodes());

        var accounting = InventoryAcceptanceSourceAccounting.AccountSection(
            document, record, rows);

        Assert.Equal(InventoryAcceptanceCheckResult.Failed, accounting.Result);
        Assert.Contains("not retained", accounting.Reason, StringComparison.OrdinalIgnoreCase);
    }

    private static InventoryDocumentStructure Document(bool includeUnmappedNote)
    {
        var cells = new List<InventorySourceCell>
        {
            Cell("h-product", 1, 1, "Product"),
            Cell("h-rate", 1, 2, "Rate"),
            Cell("h-currency", 1, 3, "Currency"),
            Cell("h-channel", 1, 4, "Channel"),
            Cell("r2-product", 2, 1, "SITE-A"),
            Cell("r2-rate", 2, 2, "125000"),
            Cell("r2-currency", 2, 3, "ZAR"),
            Cell("r2-channel", 2, 4, "OOH"),
            Cell("r3-product", 3, 1, "SITE-B"),
            Cell("r3-rate", 3, 2, "98000"),
            Cell("r3-currency", 3, 3, "ZAR"),
            Cell("r3-channel", 3, 4, "OOH"),
        };
        if (includeUnmappedNote)
            cells.Add(Cell("r2-note", 2, 5, "Illuminated"));
        return new InventoryDocumentStructure(
            new string('a', 64),
            new string('b', 64),
            [new InventorySourceStructure("sheet-1", "table", cells)]);
    }

    private static InventoryRecordSchema Record() => new(
        "sheet-1",
        new InventoryRecordBoundary(2, 3, 1, []),
        [
            Mapping("product_code", "Product", "h-product", 1),
            Mapping("rate", "Rate", "h-rate", 2),
            Mapping("currency", "Currency", "h-currency", 3),
            Mapping("channel", "Channel", "h-channel", 4),
        ],
        [],
        []);

    private static InventorySchemaFieldMapping Mapping(
        string meaning,
        string label,
        string locator,
        int column) => new(
        meaning,
        label,
        locator,
        "sheet-1",
        column,
        0,
        false,
        $"The source header {label} identifies {meaning}.",
        1m,
        [new InventorySchemaCitation(locator, label)]);

    private static InventorySchemaProposal Proposal(
        InventoryDocumentStructure document,
        InventoryRecordSchema record) => new(
        "inventory-schema/1.0",
        document.SourceHash,
        document.StructureHash,
        [record],
        1m,
        []);

    private static Dictionary<string, IReadOnlySet<string>> GovernedCodes() =>
        new(StringComparer.Ordinal)
        {
            ["currency"] = new HashSet<string>(["ZAR"], StringComparer.Ordinal),
            ["channel"] = new HashSet<string>(["OOH"], StringComparer.Ordinal),
        };

    private static InventorySourceCell Cell(
        string locator, int row, int column, string value) =>
        new(locator, row, column, value);

    private static InventoryDiscoveredField Field(
        InventoryExtractedRow row, string meaning) =>
        Assert.Single(row.DiscoveredFields!, item =>
            item.CanonicalMeaning == meaning);
}

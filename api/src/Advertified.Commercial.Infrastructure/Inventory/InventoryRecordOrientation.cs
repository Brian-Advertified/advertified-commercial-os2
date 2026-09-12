using Advertified.Commercial.Application.Inventory;

namespace Advertified.Commercial.Infrastructure.Inventory;

// Extraction protocol axes, never commercial classifications. Source locators and positions
// remain physical; only traversal coordinates are transposed for column-oriented records.
internal static class InventoryRecordOrientation
{
    internal static InventorySourceStructure Apply(
        InventorySourceStructure source, InventoryRecordSchema record) => record.RecordAxis switch
    {
        null or "ROW" => source,
        "COLUMN" => source with
        {
            Cells = source.Cells.Select(cell => cell with { Row = cell.Column, Column = cell.Row }).ToArray(),
        },
        _ => throw new InventorySchemaRejectedException("Schema record axis is invalid."),
    };

    internal static int MaximumBindingColumn(InventoryRecordSchema record) =>
        record.RecordAxis == "COLUMN" ? 1_000_000 : 256;
}

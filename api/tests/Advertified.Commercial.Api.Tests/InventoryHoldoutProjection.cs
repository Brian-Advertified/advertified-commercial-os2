using System.Text.Json;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Infrastructure.Inventory;

namespace Advertified.Commercial.Api.Tests;

// Explicit test-authored bindings. No filename or label interpretation enters production.
internal static class InventoryHoldoutProjection
{
    internal static InventorySchemaProposal Proposal(
        InventoryDocumentStructure document, JsonElement fixture)
    {
        var tables = fixture.GetProperty("tables").EnumerateArray().ToArray();
        var records = document.Structures.Select((source, index) =>
            Record(source, tables[index])).ToArray();
        return new("inventory-schema/1.0", document.SourceHash, document.StructureHash,
            records, 1m, []);
    }

    private static InventoryRecordSchema Record(
        InventorySourceStructure source, JsonElement table)
    {
        var axis = table.GetProperty("axis").GetString()!;
        var first = table.GetProperty("first").GetInt32();
        var last = table.GetProperty("last").GetInt32();
        var span = table.GetProperty("span").GetInt32();
        var header = table.GetProperty("header").GetInt32();
        var meanings = table.GetProperty("meanings").EnumerateArray().ToArray();
        var record = new InventoryRecordSchema(source.Id, new(first, last, span, []),
            [], [], [], axis);
        var logical = InventoryRecordOrientation.Apply(source, record);
        var mappings = new List<InventorySchemaFieldMapping>();
        for (var column = 1; column <= meanings.Length; column++)
        {
            var label = logical.Cells.Single(cell => cell.Row == header && cell.Column == column);
            for (var offset = 0; offset < span; offset++)
            {
                var meaning = offset == 0 ? meanings[column - 1].GetString() : null;
                mappings.Add(new(meaning, label.RawText, label.Locator, source.Id, column,
                    offset, false, "Explicit synthetic test binding.", 1m,
                    [new(label.Locator, label.RawText)]));
            }
        }
        var context = logical.Cells.Where(cell => cell.Row < first && cell.Row != header &&
                !string.IsNullOrWhiteSpace(cell.RawText))
            .Select(cell => new InventorySchemaCitation(cell.Locator, cell.RawText));
        mappings[0] = mappings[0] with { Evidence = mappings[0].Evidence.Concat(context).ToArray() };
        return record with { FieldMappings = mappings };
    }

    internal static InventoryCodeSets Codes()
    {
        IReadOnlySet<string> Empty() => new HashSet<string>();
        return new(Empty(), Empty(), Empty(), new HashSet<string>(["ZAR"]), Empty(),
            Empty(), Empty(), Empty(), Empty());
    }
}

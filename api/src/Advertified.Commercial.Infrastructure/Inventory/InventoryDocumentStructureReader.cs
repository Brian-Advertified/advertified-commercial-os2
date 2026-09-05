using System.Text.Json;
using Advertified.Commercial.Application.Inventory;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class InventoryDocumentStructureReader
{
    internal static InventoryDocumentStructure Read(string sourceHash, string providerJson)
    {
        using var json = JsonDocument.Parse(providerJson, new JsonDocumentOptions { MaxDepth = 64 });
        var structures = new List<InventorySourceStructure>();
        var gaps = new List<string>();
        ReadTables(json.RootElement, structures, gaps);
        ReadBlocks(json.RootElement, "texts", structures);
        ReadBlocks(json.RootElement, "pictures", structures);
        ReadEmbedded(json.RootElement, structures, gaps);
        InspectContent(json.RootElement, structures, gaps);
        var hash = InventoryExtractionContract.Hash(JsonSerializer.Serialize(structures));
        var document = new InventoryDocumentStructure(sourceHash, hash, structures, gaps);
        InventorySchemaValidation.ValidateStructure(document);
        return document;
    }

    private static void ReadTables(JsonElement root, List<InventorySourceStructure> structures, List<string> gaps)
    {
        if (!root.TryGetProperty("tables", out var tables)) return;
        var index = 0;
        foreach (var table in tables.EnumerateArray())
        {
            var id = $"docling:table={++index}";
            if (!table.TryGetProperty("data", out var data) || !data.TryGetProperty("table_cells", out var sourceCells))
            {
                gaps.Add($"Table has no retained cell extraction: {id}.");
                continue;
            }
            var cells = new List<InventorySourceCell>();
            foreach (var source in sourceCells.EnumerateArray())
            {
                var row = source.GetProperty("start_row_offset_idx").GetInt32();
                var column = source.GetProperty("start_col_offset_idx").GetInt32();
                var text = source.TryGetProperty("text", out var value) ? value.GetString() ?? string.Empty : string.Empty;
                cells.Add(new InventorySourceCell($"{id};row={row};column={column}", row, column,
                    text, SourcePosition(source, table)));
                if (cells.Count > InventorySchemaValidation.MaximumCells)
                    throw new InventorySchemaRejectedException("Document cell budget exceeded.");
            }
            if (cells.Count > 0) structures.Add(new InventorySourceStructure(id, "table", cells));
            else gaps.Add($"Table extraction contains no cells: {id}.");
            EnsureStructureBudget(structures);
        }
    }

    private static void ReadBlocks(JsonElement root, string collection, List<InventorySourceStructure> structures)
    {
        if (!root.TryGetProperty(collection, out var blocks)) return;
        var byPage = new Dictionary<int, List<InventorySourceCell>>();
        var index = 0;
        foreach (var block in blocks.EnumerateArray())
        {
            index++;
            var page = Page(block);
            if (!byPage.TryGetValue(page, out var cells)) byPage[page] = cells = [];
            var text = block.TryGetProperty("text", out var value) ? value.GetString() ?? string.Empty : string.Empty;
            var locator = $"docling:page={page};{collection}={index}";
            cells.Add(new InventorySourceCell(locator, cells.Count, 0, text, SourcePosition(block, block)));
            if (index > InventorySchemaValidation.MaximumCells)
                throw new InventorySchemaRejectedException("Document block budget exceeded.");
        }
        foreach (var (page, cells) in byPage.OrderBy(item => item.Key))
        {
            structures.Add(new InventorySourceStructure($"docling:page={page};kind={collection}", collection, cells));
            EnsureStructureBudget(structures);
        }
    }

    private static int Page(JsonElement item)
    {
        if (item.TryGetProperty("prov", out var provenance) && provenance.ValueKind == JsonValueKind.Array &&
            provenance.GetArrayLength() > 0 && provenance[0].TryGetProperty("page_no", out var page))
            return page.GetInt32();
        return 0; // Protocol sentinel: page not supplied, never an invented page reference.
    }

    private static void ReadEmbedded(JsonElement root, List<InventorySourceStructure> structures, List<string> gaps)
    {
        if (!root.TryGetProperty("embeddedOfficeImages", out var images)) return;
        foreach (var image in images.EnumerateArray())
        {
            var locator = image.GetProperty("sourceLocator").GetString();
            var hash = image.GetProperty("sourceHash").GetString();
            if (string.IsNullOrWhiteSpace(locator) || string.IsNullOrWhiteSpace(hash))
                throw new InventorySchemaRejectedException("Embedded source identity is missing.");
            if (!image.TryGetProperty("document", out var document) || document.ValueKind != JsonValueKind.Object)
            {
                gaps.Add($"Embedded image extraction is incomplete: {locator}.");
                continue;
            }
            var child = Read(hash, document.GetRawText());
            gaps.AddRange(child.ExtractionGaps ?? []);
            structures.AddRange(child.Structures.Select(structure => structure with
            {
                Id = locator + ";" + structure.Id,
                Cells = structure.Cells.Select(cell => cell with { Locator = locator + ";" + cell.Locator }).ToArray(),
            }));
            EnsureStructureBudget(structures);
        }
    }

    private static void InspectContent(JsonElement root, List<InventorySourceStructure> structures, List<string> gaps)
    {
        foreach (var structure in structures)
            if (structure.Kind == "pictures" && structure.Cells.Any(cell => string.IsNullOrWhiteSpace(cell.RawText)))
                gaps.Add($"Image content has no retained text interpretation: {structure.Id}.");
        foreach (var collection in new[] { "key_value_items", "form_items" })
            if (root.TryGetProperty(collection, out var items) && items.ValueKind == JsonValueKind.Array &&
                items.GetArrayLength() > 0)
                gaps.Add($"Source collection is not supported by the structural reader: {collection}.");
        if (!root.TryGetProperty("pages", out var pages) || pages.ValueKind != JsonValueKind.Object) return;
        var represented = new HashSet<int>();
        foreach (var collection in new[] { "tables", "texts", "pictures" })
            if (root.TryGetProperty(collection, out var items))
                foreach (var item in items.EnumerateArray())
                    if (item.TryGetProperty("prov", out var provenance))
                        foreach (var position in provenance.EnumerateArray())
                            if (position.TryGetProperty("page_no", out var number)) represented.Add(number.GetInt32());
        foreach (var page in pages.EnumerateObject())
            if (int.TryParse(page.Name, out var number) && !represented.Contains(number))
                gaps.Add($"Source page has no retained content or explicit blank-page evidence: {number}.");
    }

    private static string SourcePosition(JsonElement cell, JsonElement container) => JsonSerializer.Serialize(new
    {
        cell = OrderedPosition(cell),
        containerProvenance = container.TryGetProperty("prov", out var provenance) ? OrderedPosition(provenance) : null,
    });

    // JSONB may reorder object properties. Property order is not part of source
    // identity; array order and exact raw string values remain significant.
    private static object? OrderedPosition(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object => value.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToDictionary(property => property.Name, property => OrderedPosition(property.Value), StringComparer.Ordinal),
        JsonValueKind.Array => value.EnumerateArray().Select(OrderedPosition).ToArray(),
        JsonValueKind.Null => null,
        _ => value.Clone(),
    };

    private static void EnsureStructureBudget(List<InventorySourceStructure> structures)
    {
        if (structures.Count > InventorySchemaValidation.MaximumStructures)
            throw new InventorySchemaRejectedException("Document structure budget exceeded.");
    }
}

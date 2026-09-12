using System.Text;
using System.Text.Json;

namespace Advertified.Commercial.Api.Tests;

// Fixture selection is confined to tests; production only receives file bytes and typed bindings.
internal static class BackendInventoryScenarioSources
{
    internal static (string File, string Kind, byte[] Bytes, JsonElement? Table) Read(string id, string root)
    {
        if (id is "INV-005" or "INV-006" or "INV-008" or "INV-014")
            return InlineSource(id);
        var file = id switch
        {
            "INV-001" => "xlsx_flat.xlsx",
            "INV-002" => "xlsx_merged_header.xlsx",
            "INV-003" => "xlsx_transposed.xlsx",
            "INV-004" => "xlsx_multi_section.xlsx",
            "INV-007" => "csv_blank_cells.csv",
            "INV-009" => "pptx_schedule.pptx",
            "INV-010" => "pdf_text_rate_card.pdf",
            "INV-011" => "pdf_brochure.pdf",
            "INV-012" => "pdf_mixed_narrative_table.pdf",
            "INV-013" => "xlsx_missing_supplier.xlsx",
            "INV-015" => "xlsx_rate_variants.xlsx",
            _ => throw new ArgumentException("The inventory scenario has no source fixture.", nameof(id)),
        };
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "manifest.json")));
        var fixture = manifest.RootElement.GetProperty("cases").EnumerateArray()
            .Single(item => item.GetProperty("file").GetString() == file).Clone();
        var kind = fixture.GetProperty("format").GetString()!;
        return (file, kind, File.ReadAllBytes(Path.Combine(root, file)),
            fixture.TryGetProperty("tables", out _) ? fixture : null);
    }

    private static (string File, string Kind, byte[] Bytes, JsonElement? Table) InlineSource(string id)
    {
        var meanings = new List<string> { "product_code", "name", "rate", "currency" };
        var labels = new List<string> { "Item key", "Placement", "Quoted amount", "Money unit" };
        var first = new List<string> { "ITEM-571", "Synthetic panel", "1000.00", "ZAR" };
        var second = new List<string>(first);
        AddCommercialShape(id, meanings, labels, first, second);
        string[][] rows = [labels.ToArray(), first.ToArray(), second.ToArray()];
        var content = string.Join("\n", rows.Select(row =>
            string.Join(",", row.Select(value => "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""))));
        var table = JsonSerializer.SerializeToElement(new
        {
            tables = new[] { new { rows, first = 2, last = 3, span = 1, axis = "ROW",
                header = 1, meanings } },
        });
        return (id + ".csv", "CSV", Encoding.UTF8.GetBytes(content), table);
    }

    private static void AddCommercialShape(
        string id, List<string> meanings, List<string> labels, List<string> first, List<string> second)
    {
        if (id == "INV-005")
        {
            meanings.AddRange(["package_code", "package_name", "package_component_codes"]);
            labels.AddRange(["Bundle key", "Bundle name", "Included products"]);
            first.AddRange(["PK-531", "Bundle K-531", "COMP-01; COMP-02"]);
            second.AddRange(["PK-531", "Bundle K-531", "COMP-01; COMP-02"]);
        }
        if (id == "INV-006")
        {
            meanings.Add("discount_terms");
            labels.Add("Discount conditions");
            first.Add("10 percent only above 5 units");
            second.Add("10 percent only above 5 units");
        }
        if (id == "INV-008")
        {
            meanings.AddRange(["rate_valid_from", "rate_valid_to"]);
            labels.AddRange(["Starts", "Ends"]);
            first.AddRange(["2026-10-01", "2026-11-30"]);
            second.AddRange(["2026-11-01", "2026-12-31"]);
        }
    }
}

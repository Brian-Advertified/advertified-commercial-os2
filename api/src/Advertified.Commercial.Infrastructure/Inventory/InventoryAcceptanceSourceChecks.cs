using System.Text.Json;
using Advertified.Commercial.Application.Inventory;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class InventoryAcceptanceSourceChecks
{
    internal static IReadOnlyList<InventoryAcceptanceCheckEvidence> Evaluate(
        InventoryExtractionResult extraction, string expectedHash, long sourceVersion, InventoryCodeSets codes)
    {
        var identity = sourceVersion > 0 && extraction.SourceHash == expectedHash &&
            extraction.ProviderOutputHash == InventoryExtractionContract.Hash(extraction.ProviderJson) &&
            extraction.CanonicalOutputHash == InventoryExtractionContract.Hash(extraction.CanonicalJson);
        var checks = new List<InventoryAcceptanceCheckEvidence>
        {
            Check(InventoryAcceptanceCheck.SourceIdentity, identity,
                identity ? "Source version and retained extraction hashes match the active attempt."
                    : "Source version or retained extraction hash does not match the active attempt."),
        };
        ValidateApplication(extraction, codes, checks);
        return checks;
    }

    private static void ValidateApplication(InventoryExtractionResult extraction, InventoryCodeSets codes,
        List<InventoryAcceptanceCheckEvidence> checks)
    {
        var schema = extraction.Document.DiscoveredSchema;
        if (schema is null)
        {
            checks.Add(new(InventoryAcceptanceCheck.InterpretationBinding,
                InventoryAcceptanceCheckResult.NotEvaluated, "document", "No retained interpretation is available."));
            checks.Add(new(InventoryAcceptanceCheck.StructuralApplication,
                InventoryAcceptanceCheckResult.NotEvaluated, "document", "No mappings are available for full-record validation."));
            checks.Add(new(InventoryAcceptanceCheck.SourceContentAccounting,
                InventoryAcceptanceCheckResult.NotEvaluated, "document", "No interpretation is available for source accounting."));
            return;
        }
        try
        {
            var document = InventoryDocumentStructureReader.Read(extraction.SourceHash, extraction.ProviderJson);
            var projected = InventorySchemaBatchProjection.Project(document, schema,
                InventoryCandidateNormalizer.CanonicalMeanings, InventorySchemaExtractionStep.GovernedCodes(codes));
            var binding = !string.IsNullOrWhiteSpace(schema.Provenance.Interpreter) &&
                !string.IsNullOrWhiteSpace(schema.Provenance.ConfigurationVersion) &&
                (schema.Provenance.AiCalls == 0 || (!string.IsNullOrWhiteSpace(schema.Provenance.Model) &&
                    !string.IsNullOrWhiteSpace(schema.Provenance.ProviderRequestId)));
            checks.Add(Check(InventoryAcceptanceCheck.InterpretationBinding, binding,
                binding ? "Retained mappings and citations validate against the exact extracted structures."
                    : "Interpreter or prompt/configuration provenance is missing."));
            foreach (var record in schema.Records)
            {
                var actual = RowsForStructure(extraction.Rows, record.SourceStructure);
                var replay = RowsForStructure(projected, record.SourceStructure);
                var equal = JsonSerializer.Serialize(actual, InventoryRowMapper.StoredJson) ==
                    JsonSerializer.Serialize(replay, InventoryRowMapper.StoredJson);
                checks.Add(new(InventoryAcceptanceCheck.StructuralApplication,
                    equal ? InventoryAcceptanceCheckResult.Passed :
                        InventoryAcceptanceCheckResult.Failed,
                    record.SourceStructure, equal
                        ? "Mappings were reapplied to every record in this section, including unsampled records."
                        : "Retained records differ from deterministic mapping replay in this section."));
                checks.Add(InventoryAcceptanceSourceAccounting.AccountSection(
                    document, record, replay));
            }
        }
        catch (Exception exception) when (exception is InventorySchemaRejectedException or JsonException or InvalidOperationException)
        {
            checks.Add(Check(InventoryAcceptanceCheck.InterpretationBinding, false,
                "Retained interpretation cannot be validated against the source structures."));
            checks.Add(Check(InventoryAcceptanceCheck.StructuralApplication, false,
                "Full-record mapping validation could not be completed safely."));
            checks.Add(new(InventoryAcceptanceCheck.SourceContentAccounting,
                InventoryAcceptanceCheckResult.NotEvaluated, "document", "Source accounting requires valid retained structure bindings."));
        }
    }

    private static InventoryAcceptanceCheckEvidence Check(InventoryAcceptanceCheck check, bool passed, string reason) =>
        new(check, passed ? InventoryAcceptanceCheckResult.Passed : InventoryAcceptanceCheckResult.Failed,
            "document", reason);

    private static InventoryExtractedRow[] RowsForStructure(
        IReadOnlyList<InventoryExtractedRow> rows,
        string structure) => rows.Where(row => (row.DiscoveredFields ?? [])
            .Any(field => field.SourceStructure == structure)).ToArray();
}

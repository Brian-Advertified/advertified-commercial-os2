using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Advertified.Commercial.Application.Inventory;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static partial class InventoryExtractionSourceAccounting
{
    internal const string ProtocolVersion = "inventory-source-accounting/1.0";

    [GeneratedRegex(@"\b(?:rate\s+on\s+request|p\.?o\.?r\.?)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RateOnRequestPattern();

    [GeneratedRegex(@"\b(?:package|discount|saving|vat|currency|valid|rate|price|cost|investment|availability|buying\s+unit)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CommercialTermPattern();

    [GeneratedRegex(@"\b[A-Z]{2,8}[- ]?\d{2,8}\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ProductCodePattern();

    internal static InventoryExtractionResult Attach(
        InventoryExtractionResult extraction,
        IReadOnlyList<PreparedInventoryCandidate> candidates,
        IReadOnlyList<InventoryDeduplicationDecision>? deduplication = null)
    {
        var decisions = deduplication ??
            extraction.Document.DeduplicationDecisions ?? [];
        var report = Build(extraction, candidates, decisions);
        return InventoryExtractionContract.Create(extraction.AdapterCode,
            extraction.AdapterVersion, extraction.SchemaVersion,
            extraction.SourceHash, extraction.ProviderJson, extraction.Rows,
            extraction.Document.DiscoveredSchema,
            extraction.Document.SchemaDiscoveryFailure, report, decisions,
            extraction.Document.SourceElements,
            extraction.Document.ProjectionWarnings);
    }

    internal static InventorySourceAccountingReport Build(
        InventoryExtractionResult extraction,
        IReadOnlyList<PreparedInventoryCandidate> candidates,
        IReadOnlyList<InventoryDeduplicationDecision> deduplication)
    {
        var elements = ReadElements(extraction);
        var rowReferences = RowReferences(extraction.Rows);
        ApplyDeduplicationReferences(rowReferences, deduplication);
        var normalizedReferences = NormalizedReferences(extraction.Rows,
            extraction.SourceHash);
        var schemaReferences = SchemaReferences(extraction.Document.DiscoveredSchema);
        var candidateRows = candidates.GroupBy(candidate => candidate.RowNumber)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var entries = elements.Where(element => element.Signal is not null)
            .Select(element => Trace(element, extraction, rowReferences,
                normalizedReferences, schemaReferences, candidateRows,
                deduplication)).ToArray();
        var accounted = entries.Count(IsAccounted);
        var summary = new InventorySourceAccountingSummary(
            elements.Count, entries.Length, accounted, entries.Length - accounted,
            Count(entries, entry => entry.FirstFailureStage ?? "NONE"),
            Count(entries, entry => entry.TerminalDisposition));
        return new InventorySourceAccountingReport(ProtocolVersion,
            extraction.SourceHash, summary, entries, deduplication,
            ExceptionGroups(entries));
    }

    private static List<SourceElement> ReadElements(
        InventoryExtractionResult extraction)
    {
        var result = (extraction.Document.SourceElements ?? [])
            .Where(element => !string.IsNullOrWhiteSpace(element.RawValue))
            .Select(element => new SourceElement(
                element.Locator,
                element.StructureId,
                element.StructureKind,
                element.Row,
                element.Column,
                element.RawValue,
                element.PositionJson,
                null))
            .ToList();
        AddProjectedElements(extraction.Rows, result);
        var rateLocators = extraction.Rows.SelectMany(row => row.RateVariants ?? [])
            .Select(rate => rate.SourceLocator).ToHashSet(StringComparer.Ordinal);
        var fieldLocators = extraction.Rows
            .SelectMany(row => row.FieldLocators?.Values ?? [])
            .Concat(extraction.Rows.SelectMany(row => row.DiscoveredFields ?? [])
                .Select(field => field.SourceLocator))
            .ToHashSet(StringComparer.Ordinal);
        return result.GroupBy(element => element.Locator, StringComparer.Ordinal)
            .Select(group => group.First() with
            {
                Signal = Signal(group.First().RawValue,
                    rateLocators.Contains(group.Key), fieldLocators.Contains(group.Key)),
            })
            .OrderBy(element => element.Locator, StringComparer.Ordinal).ToList();
    }

    private static void AddProjectedElements(
        IReadOnlyList<InventoryExtractedRow> rows,
        List<SourceElement> result)
    {
        var known = result.Select(element => element.Locator)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            foreach (var value in row.Values)
            {
                var locator = row.FieldLocators?.GetValueOrDefault(value.Key) ?? row.Locator;
                if (known.Add(locator))
                    result.Add(new SourceElement(locator, Scope(locator), "projected-source",
                        Coordinate(locator, "row"), Coordinate(locator, "cell"),
                        value.Value, null, null));
            }
            foreach (var rate in row.RateVariants ?? [])
                if (known.Add(rate.SourceLocator))
                    result.Add(new SourceElement(rate.SourceLocator, Scope(rate.SourceLocator),
                        "projected-source", Coordinate(rate.SourceLocator, "row"),
                        Coordinate(rate.SourceLocator, "cell"), rate.RawValue,
                        rate.PositionJson, "MONETARY_VALUE"));
        }
    }

    private static Dictionary<string, int[]> RowReferences(
        IReadOnlyList<InventoryExtractedRow> rows)
    {
        var result = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            Add(result, row.Locator, row.Number);
            foreach (var locator in row.FieldLocators?.Values ?? []) Add(result, locator, row.Number);
            foreach (var field in row.DiscoveredFields ?? []) Add(result, field.SourceLocator, row.Number);
            foreach (var rate in row.RateVariants ?? [])
            {
                Add(result, rate.SourceLocator, row.Number);
                foreach (var locator in rate.HeaderLocators) Add(result, locator, row.Number);
            }
        }
        return result.ToDictionary(item => item.Key, item => item.Value.Order().ToArray(),
            StringComparer.Ordinal);
    }

    private static void Add(
        Dictionary<string, HashSet<int>> references,
        string locator,
        int row)
    {
        if (!references.TryGetValue(locator, out var rows))
            references[locator] = rows = [];
        rows.Add(row);
    }

    private static void ApplyDeduplicationReferences(
        Dictionary<string, int[]> references,
        IReadOnlyList<InventoryDeduplicationDecision> decisions)
    {
        foreach (var decision in decisions)
        {
            if (!references.TryGetValue(decision.RetainedLocator, out var rows))
                continue;
            references[decision.ConsolidatedLocator] = rows;
            foreach (var locator in decision.ConsolidatedSourceLocators)
                references[locator] = rows;
        }
    }

    private static HashSet<string> NormalizedReferences(
        IReadOnlyList<InventoryExtractedRow> rows,
        string sourceHash)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var normalized = InventoryCandidateNormalizer.Normalize(
                row, sourceHash, DateTimeOffset.UnixEpoch);
            foreach (var evidence in normalized.Evidence)
                result.Add(evidence.SourceLocator);
        }
        return result;
    }

    private static HashSet<string> SchemaReferences(
        DiscoveredInventorySchema? schema)
    {
        if (schema is null) return [];
        return schema.Records.SelectMany(record => record.FieldMappings
                .Concat(record.SupplierMetadataMappings).Concat(record.AssetMappings))
            .SelectMany(mapping => mapping.Evidence.Select(item => item.SourceLocator)
                .Append(mapping.SourceLocation)
                .Concat(mapping.ValueSourceLocation is null ? [] : [mapping.ValueSourceLocation]))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static InventorySourceEntryTrace Trace(
        SourceElement element,
        InventoryExtractionResult extraction,
        IReadOnlyDictionary<string, int[]> rowReferences,
        HashSet<string> normalizedReferences,
        HashSet<string> schemaReferences,
        IReadOnlyDictionary<int, PreparedInventoryCandidate[]> candidates,
        IReadOnlyList<InventoryDeduplicationDecision> deduplication)
    {
        var rows = rowReferences.GetValueOrDefault(element.Locator) ?? [];
        var mappedCandidates = rows.SelectMany(row => candidates.GetValueOrDefault(row) ?? []).ToArray();
        var isSchemaContext = schemaReferences.Contains(element.Locator);
        var projected = rows.Length > 0 || isSchemaContext;
        var normalized = normalizedReferences.Contains(element.Locator) ||
            isSchemaContext;
        var admitted = mappedCandidates.Any(candidate =>
            candidate.Evidence.Any(field => field.SourceLocator == element.Locator));
        var persistable = mappedCandidates.Any(candidate => candidate.Validation.All(issue => !issue.IsBlocking));
        var schemaStage = SchemaStage(extraction, element, schemaReferences);
        var commercialBlock = projected ||
            element.StructureKind.Contains("table", StringComparison.OrdinalIgnoreCase) ||
            element.StructureKind.Contains("sheet", StringComparison.OrdinalIgnoreCase);
        var firstFailure = FirstFailure(commercialBlock, schemaStage,
            projected, normalized, admitted, persistable);
        var terminal = Terminal(element, extraction.Rows, mappedCandidates,
            isSchemaContext, admitted, projected);
        return new InventorySourceEntryTrace(EntryId(extraction.SourceHash, element.Locator),
            extraction.SourceHash, element.Locator, element.StructureId, element.StructureKind,
            element.Row, element.Column, element.RawValue, element.PositionJson,
            element.Signal!, HeaderHierarchy(element.Locator, extraction.Rows),
            Stage(InventoryExtractionTraceCodes.Retained, element.Locator),
            Stage(InventoryExtractionTraceCodes.Retained, element.StructureId),
            Stage(commercialBlock ? InventoryExtractionTraceCodes.Mapped :
                InventoryExtractionTraceCodes.Unsupported, element.StructureId),
            schemaStage,
            Stage(projected ? InventoryExtractionTraceCodes.Mapped : InventoryExtractionTraceCodes.Failed,
                rows.Length > 0 ? string.Join(',', rows) : null,
                projected ? null : "No projected record references this commercial source element."),
            Stage(normalized || isSchemaContext ? InventoryExtractionTraceCodes.Mapped :
                InventoryExtractionTraceCodes.NotEvaluated),
            Stage(admitted || isSchemaContext ? InventoryExtractionTraceCodes.Mapped :
                normalized ? InventoryExtractionTraceCodes.Rejected : InventoryExtractionTraceCodes.NotEvaluated),
            DeduplicationStage(element.Locator, deduplication),
            Stage(persistable ? InventoryExtractionTraceCodes.Mapped :
                admitted ? InventoryExtractionTraceCodes.Ambiguous : InventoryExtractionTraceCodes.NotEvaluated),
            Stage(InventoryExtractionTraceCodes.NotEvaluated), terminal,
            firstFailure, rows);
    }

    private static string? FirstFailure(
        bool commercialBlock,
        InventoryTraceStageResult schema,
        bool projected,
        bool normalized,
        bool admitted,
        bool persistable)
    {
        if (!commercialBlock) return InventoryExtractionTraceCodes.CommercialBlock;
        if (schema.State is InventoryExtractionTraceCodes.Failed or
            InventoryExtractionTraceCodes.Unsupported)
            return InventoryExtractionTraceCodes.SchemaDiscovery;
        if (!projected) return InventoryExtractionTraceCodes.RecordProjection;
        if (!normalized) return InventoryExtractionTraceCodes.Normalization;
        if (!admitted) return InventoryExtractionTraceCodes.Admission;
        return persistable ? null : InventoryExtractionTraceCodes.Persistence;
    }

    private static InventoryTraceStageResult DeduplicationStage(
        string locator,
        IReadOnlyList<InventoryDeduplicationDecision> decisions)
    {
        var decision = decisions.FirstOrDefault(item =>
            item.ConsolidatedLocator == locator ||
            item.ConsolidatedSourceLocators.Contains(locator, StringComparer.Ordinal));
        return decision is null
            ? Stage(InventoryExtractionTraceCodes.Retained,
                "false-merge-safe exact-evidence policy")
            : Stage(InventoryExtractionTraceCodes.Consolidated,
                decision.RetainedLocator, decision.Reason);
    }

    private static InventoryTraceStageResult SchemaStage(
        InventoryExtractionResult extraction,
        SourceElement element,
        HashSet<string> schemaReferences)
    {
        if (extraction.Document.DiscoveredSchema is null)
            return extraction.Document.SchemaDiscoveryFailure is null
                ? Stage(InventoryExtractionTraceCodes.Retained,
                    "deterministic structural projection")
                : Stage(InventoryExtractionTraceCodes.Failed, reason:
                    extraction.Document.SchemaDiscoveryFailure);
        var represented = extraction.Document.DiscoveredSchema.Records.Any(record =>
            record.SourceStructure == element.StructureId);
        return Stage(represented || schemaReferences.Contains(element.Locator)
            ? InventoryExtractionTraceCodes.Mapped
            : InventoryExtractionTraceCodes.Unsupported,
            represented ? element.StructureId : null,
            represented ? null : "No section schema represents this source structure.");
    }

    private static InventoryTraceStageResult Stage(
        string state,
        string? reference = null,
        string? reason = null) => new(state, reference, reason);

    private static string? Signal(string value, bool projectedRate, bool projectedField)
    {
        if (projectedRate) return "MONETARY_VALUE";
        if (projectedField) return "PROJECTED_FIELD";
        if (InventoryMoneyParser.TryParse(value, out _, out var currency) && currency.Length > 0)
            return "MONETARY_VALUE";
        if (RateOnRequestPattern().IsMatch(value)) return "UNPRICED_RATE";
        var normalized = Regex.Replace(
            value.Trim().ToLowerInvariant(), @"[^a-z0-9]+", string.Empty,
            RegexOptions.CultureInvariant);
        if (InventoryCandidateNormalizer.RecognizesHeader(normalized) ||
            CommercialTermPattern().IsMatch(value)) return "COMMERCIAL_CONTEXT";
        if (ProductCodePattern().IsMatch(value)) return "PRODUCT_IDENTITY";
        return null;
    }

    private static string? HeaderHierarchy(
        string locator,
        IReadOnlyList<InventoryExtractedRow> rows) => rows
        .SelectMany(row => row.RateVariants ?? [])
        .FirstOrDefault(rate => rate.SourceLocator == locator)?.HeaderHierarchy;

    private static bool IsAccounted(InventorySourceEntryTrace entry) =>
        entry.TerminalDisposition is InventoryExtractionTraceCodes.ProductField or
            InventoryExtractionTraceCodes.Rate or
            InventoryExtractionTraceCodes.PackageOrComponent or
            InventoryExtractionTraceCodes.SharedCondition or
            InventoryExtractionTraceCodes.NonCommercial or
            InventoryExtractionTraceCodes.Ambiguous or
            InventoryExtractionTraceCodes.Unsupported or
            InventoryExtractionTraceCodes.Failed;

    private static Dictionary<string, int> Count(
        IEnumerable<InventorySourceEntryTrace> entries,
        Func<InventorySourceEntryTrace, string> key) => entries.GroupBy(key)
        .OrderBy(group => group.Key, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

    private static InventoryReviewExceptionGroup[] ExceptionGroups(
        IReadOnlyList<InventorySourceEntryTrace> entries) => entries
        .Where(entry => entry.TerminalDisposition is
            InventoryExtractionTraceCodes.Ambiguous or
            InventoryExtractionTraceCodes.Unsupported or
            InventoryExtractionTraceCodes.Failed)
        .GroupBy(entry => (entry.FirstFailureStage ?? InventoryExtractionTraceCodes.Admission,
            entry.StructureId))
        .Select(group => new InventoryReviewExceptionGroup(
            group.Key.StructureId + ":" + group.Key.Item1,
            group.Key.Item1,
            group.Key.StructureId,
            group.Count(),
            group.Select(entry => entry.SourceLocator).Order(StringComparer.Ordinal).ToArray(),
            Correction(group.Key.Item1)))
        .OrderByDescending(group => group.AffectedEntryCount)
        .ThenBy(group => group.GroupKey, StringComparer.Ordinal).ToArray();

    private static string Correction(string stage) => stage switch
    {
        InventoryExtractionTraceCodes.SchemaDiscovery =>
            "Correct the section schema and re-evaluate every dependent record.",
        InventoryExtractionTraceCodes.RecordProjection =>
            "Correct the section/table mapping and replay the affected scope.",
        InventoryExtractionTraceCodes.Normalization =>
            "Resolve the repeated field meaning or shared commercial context.",
        _ => "Resolve the grouped source-accounting exception and replay its scope.",
    };

    private static string EntryId(string sourceHash, string locator) =>
        Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes(sourceHash + "\n" + locator)));

    private static string Scope(string locator)
    {
        var row = locator.IndexOf(";row=", StringComparison.Ordinal);
        return row < 0 ? locator : locator[..row];
    }

    private static int Coordinate(string locator, string name)
    {
        var match = Regex.Match(locator, ";" + name + @"=(?<value>\d+)",
            RegexOptions.CultureInvariant);
        return match.Success && int.TryParse(match.Groups["value"].Value, out var value)
            ? value : 0;
    }

}

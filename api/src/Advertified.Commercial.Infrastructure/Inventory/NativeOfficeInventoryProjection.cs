using System.Globalization;

using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class NativeOfficeInventoryProjection
{
    internal const string AdapterVersion =
        "advertified-openxml/1.1.0";

    internal static InventoryExtractionResult Apply(
        InventoryExtractionRequest request,
        InventoryExtractionResult provider)
    {
        if (provider.Document.DiscoveredSchema is not null)
            return provider;
        var native = request.DocumentClass switch
        {
            MasterDataCodes.DocumentClasses.Xlsx =>
                NativeSpreadsheetProjection.Read(request),
            MasterDataCodes.DocumentClasses.Pptx =>
                NativePresentationProjection.Read(request),
            _ => [],
        };
        if (native.Count == 0)
            return provider;
        var rows = Merge(
            native,
            provider.Rows,
            request.SourceHash,
            request.DocumentClass);
        return InventoryExtractionContract.Create(
            provider.AdapterCode,
            provider.AdapterVersion,
            provider.SchemaVersion,
            provider.SourceHash,
            provider.ProviderJson,
            rows);
    }

    internal static InventoryExtractedRow[] Merge(
        IReadOnlyList<InventoryExtractedRow> native,
        IReadOnlyList<InventoryExtractedRow> provider,
        string sourceHash,
        string documentClass)
    {
        var rows = new List<InventoryExtractedRow>();
        var identities = new Dictionary<string, List<int>>(
            StringComparer.Ordinal);
        var includeOrdinalScope = documentClass ==
            MasterDataCodes.DocumentClasses.Pptx;

        foreach (var row in native)
        {
            if (IsVisualBlocker(row) &&
                (native.Count > 1 || provider.Count > 0))
                continue;
            var index = rows.Count;
            rows.Add(row);
            var identity = Identity(row, sourceHash, includeOrdinalScope);
            if (!identities.TryGetValue(identity, out var matches)) identities[identity] = matches = [];
            matches.Add(index);
        }
        foreach (var row in provider)
        {
            if (IsVisualBlocker(row) && native.Count > 0)
                continue;
            var identity = Identity(
                row,
                sourceHash,
                includeOrdinalScope);
            var matches = identities.GetValueOrDefault(identity) ?? [];
            var compatible = matches.Where(index => InventoryProjectionRowMatch.Compatible(rows[index], row, sourceHash)).ToArray();
            var physical = compatible.Where(index => InventoryProjectionRowMatch.SamePhysicalEvidence(rows[index], row)).ToArray();
            var selected = physical.Length == 1 ? physical : compatible;
            if (selected.Length == 1)
            {
                rows[selected[0]] = MergeMissing(rows[selected[0]], row);
                continue;
            }
            if (selected.Length > 1)
                foreach (var index in selected) rows[index] = InventoryProjectionRowMatch.MarkAmbiguous(rows[index]);
            if (!identities.ContainsKey(identity)) identities[identity] = matches;
            matches.Add(rows.Count);
            rows.Add(selected.Length > 1 ? InventoryProjectionRowMatch.MarkAmbiguous(row) : row);
        }
        return rows.Select((row, index) =>
            row with { Number = index + 1 }).ToArray();
    }

    private static string Identity(
        InventoryExtractedRow row,
        string sourceHash,
        bool includeOrdinalScope)
    {
        var candidate = InventoryCandidateNormalizer.Normalize(
            row, sourceHash, DateTimeOffset.UnixEpoch).Values;
        var identity = candidate.Name;
        var scope = includeOrdinalScope
            ? PresentationScope(row.Locator)
            : string.Empty;
        if (!string.IsNullOrWhiteSpace(identity))
        {
            var rateKey = candidate.RateAmountMinor?.ToString(
                CultureInfo.InvariantCulture)
                ?? Normalize(SourceRate(row));
            return string.Join(
                '|',
                Normalize(identity),
                scope,
                rateKey,
                Normalize(candidate.Deliverable?.Placement),
                Normalize(candidate.Deliverable?.Programme),
                Normalize(candidate.Deliverable?.Daypart),
                Normalize(candidate.Deliverable?.Format),
                Normalize(candidate.Package?.PackageName));
        }
        if (!string.IsNullOrWhiteSpace(candidate.ProductCode))
        {
            return "code|" + Normalize(candidate.ProductCode) + "|" +
                scope;
        }
        return "raw|" + string.Join(
            '|',
            row.Values.OrderBy(item => item.Key)
                .Select(item =>
                    Normalize(item.Key) + "=" +
                    Normalize(item.Value)));
    }

    private static string SourceRate(InventoryExtractedRow row) =>
        row.Values.GetValueOrDefault("rate")
        ?? row.Values.GetValueOrDefault("price")
        ?? row.Values.GetValueOrDefault("cost")
        ?? row.Values.GetValueOrDefault("baseprice")
        ?? string.Empty;

    private static string PresentationScope(string locator)
    {
        var marker = locator.StartsWith(
                "pptx:", StringComparison.OrdinalIgnoreCase)
            ? "slide="
            : locator.StartsWith(
                "docling:", StringComparison.OrdinalIgnoreCase)
                ? "page="
                : null;
        if (marker is null)
            return string.Empty;
        var start = locator.IndexOf(
            marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return string.Empty;
        start += marker.Length;
        var end = locator.IndexOf(';', start);
        return end < 0
            ? locator[start..]
            : locator[start..end];
    }

    internal static InventoryExtractedRow MergeMissing(
        InventoryExtractedRow preferred,
        InventoryExtractedRow supplement)
    {
        var values = CopyRequired(preferred.Values);
        var locators = CopyOptional(
            preferred.FieldLocators);
        var confidences = CopyOptional(
            preferred.FieldConfidences);
        var bases = CopyOptional(
            preferred.FieldEvidenceBases);
        var transformations = CopyOptional(
            preferred.FieldTransformations);
        foreach (var item in supplement.Values)
        {
            if (!values.TryAdd(item.Key, item.Value))
                continue;
            CopyField(
                item.Key, supplement.FieldLocators, locators);
            CopyField(
                item.Key, supplement.FieldConfidences, confidences);
            CopyField(
                item.Key, supplement.FieldEvidenceBases, bases);
            CopyField(
                item.Key, supplement.FieldTransformations,
                transformations);
        }
        return preferred with
        {
            Values = values,
            FieldLocators = locators.Count == 0 ? null : locators,
            FieldConfidences =
                confidences.Count == 0 ? null : confidences,
            FieldEvidenceBases =
                bases.Count == 0 ? null : bases,
            FieldTransformations =
                transformations.Count == 0
                    ? null
                    : transformations,
        };
    }

    private static SortedDictionary<string, string> CopyRequired(
        IReadOnlyDictionary<string, string> values) =>
        new(
            values.ToDictionary(
                item => item.Key,
                item => item.Value,
                StringComparer.Ordinal),
            StringComparer.Ordinal);

    private static Dictionary<string, T> CopyOptional<T>(
        IReadOnlyDictionary<string, T>? values) =>
        values?.ToDictionary(
            item => item.Key,
            item => item.Value,
            StringComparer.Ordinal)
        ?? new Dictionary<string, T>(StringComparer.Ordinal);

    private static void CopyField<T>(
        string key,
        IReadOnlyDictionary<string, T>? source,
        IDictionary<string, T> target)
    {
        if (source?.TryGetValue(key, out var value) == true)
            target[key] = value;
    }

    private static bool IsVisualBlocker(
        InventoryExtractedRow row) =>
        NativeOfficeImageReader.IsRequired([row]);

    private static string Normalize(string? value) =>
        new((value ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
}

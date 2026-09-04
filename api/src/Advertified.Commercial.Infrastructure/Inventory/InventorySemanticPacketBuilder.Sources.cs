using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static partial class InventorySemanticPacketBuilder
{
    private static IReadOnlyList<InventorySemanticImage>
        ReadImages(
            InventoryExtractionRequest request,
            IReadOnlyList<InventoryExtractedRow> rows,
            InventorySemanticOptions settings) =>
        IsOfficeDocument(request) ||
        NativeOfficeImageReader.IsRequired(rows)
            ? NativeOfficeImageReader.Read(request, settings)
            : [];

    private static bool IsOfficeDocument(
        InventoryExtractionRequest request) =>
        request.DocumentClass is
            MasterDataCodes.DocumentClasses.Xlsx or
            MasterDataCodes.DocumentClasses.Pptx;

    private static List<
        InventorySemanticPacketSources> BuildSources(
            List<InventorySemanticSourceItem> items,
            IReadOnlyList<InventorySemanticImage> images,
            InventorySemanticOptions settings)
    {
        var result = new List<
            InventorySemanticPacketSources>();
        if (items.Count > 1 || images.Count == 0)
        {
            result.AddRange(Pack(
                    items, settings.MaximumChunkCharacters)
                .Select(group =>
                    new InventorySemanticPacketSources(
                        group, [])));
        }
        foreach (var group in PackImages(images, settings))
        {
            result.Add(new InventorySemanticPacketSources(
                ImageContext(
                    items,
                    group,
                    settings.MaximumChunkCharacters),
                group));
        }
        return result;
    }

    private static List<
        InventorySemanticSourceItem> ImageContext(
            List<InventorySemanticSourceItem> items,
            IReadOnlyList<InventorySemanticImage> images,
            int maximumCharacters)
    {
        var prefixes = images
            .Select(image => ContextPrefix(image.Locator))
            .Where(value => value is not null)
            .ToHashSet(StringComparer.Ordinal);
        var result = new List<InventorySemanticSourceItem>
        {
            items[0],
        };
        var characters = ItemSize(items[0]);
        foreach (var item in items.Skip(1).Where(item =>
                     prefixes.Any(prefix =>
                         item.Locator == prefix ||
                         item.Locator.StartsWith(
                             prefix + ";",
                             StringComparison.Ordinal))))
        {
            var size = ItemSize(item);
            if (result.Count >= 100 ||
                characters + size > maximumCharacters)
                continue;
            result.Add(item);
            characters += size;
        }
        return result;
    }

    private static string? ContextPrefix(string locator)
    {
        var marker = locator.StartsWith(
            "pptx:slide=", StringComparison.Ordinal)
            ? ";image="
            : locator.StartsWith(
                "xlsx:sheet=", StringComparison.Ordinal)
                ? ";image="
                : null;
        if (marker is null)
            return null;
        var end = locator.IndexOf(
            marker, StringComparison.Ordinal);
        return end < 0 ? null : locator[..end];
    }

    private static int ItemSize(
        InventorySemanticSourceItem item) =>
        item.Content.Length +
        item.Locator.Length +
        item.Kind.Length + 64;

    private static List<
        IReadOnlyList<InventorySemanticImage>> PackImages(
            IReadOnlyList<InventorySemanticImage> images,
            InventorySemanticOptions settings)
    {
        var groups = new List<
            IReadOnlyList<InventorySemanticImage>>();
        var current = new List<InventorySemanticImage>();
        var bytes = 0L;
        foreach (var image in images)
        {
            if (image.ByteLength >
                settings.MaximumImagePayloadBytesPerChunk)
            {
                throw new InventorySemanticInputRejectedException();
            }
            if (current.Count > 0 &&
                (current.Count >= settings.MaximumImagesPerChunk ||
                 bytes + image.ByteLength >
                    settings.MaximumImagePayloadBytesPerChunk))
            {
                groups.Add(current.ToArray());
                current = [];
                bytes = 0;
            }
            current.Add(image with
            {
                Ordinal = current.Count + 1,
            });
            bytes += image.ByteLength;
        }
        if (current.Count > 0)
            groups.Add(current.ToArray());
        return groups;
    }

    private static IEnumerable<InventorySemanticSourceItem>
        ReadNativeRows(
            IReadOnlyList<InventoryExtractedRow> rows,
            int maximumCharacters)
    {
        foreach (var row in rows.Where(row =>
                     IsNativeRow(row) &&
                     !row.Values.ContainsKey("extractionblocker")))
        {
            var content = string.Join(
                "\n",
                row.Values.Select(item =>
                    "field=" + item.Key +
                    "\nvalue=" + item.Value));
            if (content.Length == 0)
                continue;
            var kind = row.Locator.StartsWith(
                "xlsx:", StringComparison.Ordinal)
                ? "TABLE"
                : "TEXT";
            foreach (var item in SplitItem(
                         row.Locator,
                         kind,
                         content,
                         row.Confidence,
                         maximumCharacters))
            {
                yield return item;
            }
        }
    }

    private static InventorySemanticExistingRow[] RelatedRows(
        IReadOnlyList<InventoryExtractedRow> rows,
        IReadOnlyList<InventorySemanticSourceItem> items,
        IReadOnlyList<InventorySemanticImage> images)
    {
        var pages = items.Select(PagePrefix)
            .Where(value => value is not null)
            .ToHashSet(StringComparer.Ordinal);
        var exact = items.Where(item =>
                item.Locator.StartsWith(
                    "xlsx:", StringComparison.Ordinal) ||
                item.Locator.StartsWith(
                    "pptx:", StringComparison.Ordinal))
            .Select(item => Partless(item.Locator))
            .Concat(images.Select(image => image.Locator))
            .ToHashSet(StringComparer.Ordinal);
        var officeContexts = exact.Select(OfficeContextPrefix)
            .Where(value => value is not null)
            .ToHashSet(StringComparer.Ordinal);
        return rows.Where(row =>
                !row.Values.ContainsKey("extractionblocker") &&
                (pages.Any(page => row.Locator.StartsWith(
                     page!, StringComparison.Ordinal)) ||
                 exact.Contains(row.Locator) ||
                 officeContexts.Any(prefix =>
                     row.Locator == prefix ||
                     row.Locator.StartsWith(
                         prefix + ";",
                         StringComparison.Ordinal))))
            .Select(row => new InventorySemanticExistingRow(
                row.Number,
                row.Locator,
                row.Values))
            .ToArray();
    }

    private static string? OfficeContextPrefix(string locator)
    {
        if (locator.StartsWith(
                "xlsx:sheet=", StringComparison.Ordinal))
        {
            return BeforeFirstMarker(
                locator,
                [";table=", ";row=", ";cell=", ";image="]);
        }
        if (locator.StartsWith(
                "pptx:slide=", StringComparison.Ordinal))
        {
            return BeforeFirstMarker(
                locator,
                [";table=", ";row=", ";cell=", ";image=", ";shape="]);
        }
        return null;
    }

    private static string BeforeFirstMarker(
        string locator,
        IReadOnlyList<string> markers)
    {
        var indexes = markers
            .Select(marker => locator.IndexOf(
                marker, StringComparison.Ordinal))
            .Where(index => index >= 0)
            .ToArray();
        return indexes.Length == 0
            ? locator
            : locator[..indexes.Min()];
    }

    private static string? PagePrefix(
        InventorySemanticSourceItem item)
    {
        const string prefix = "docling:page=";
        if (!item.Locator.StartsWith(
                prefix, StringComparison.Ordinal))
            return null;
        var end = item.Locator.IndexOf(
            ';', prefix.Length);
        return end < 0
            ? item.Locator
            : item.Locator[..end];
    }

    private static bool IsNativeRow(
        InventoryExtractedRow row) =>
        row.Locator.StartsWith(
            "xlsx:", StringComparison.Ordinal) ||
        row.Locator.StartsWith(
            "pptx:", StringComparison.Ordinal);

    private static string Partless(string locator)
    {
        var marker = locator.LastIndexOf(
            ";part=", StringComparison.Ordinal);
        return marker < 0 ? locator : locator[..marker];
    }
}

internal sealed record InventorySemanticPacketSources(
    IReadOnlyList<InventorySemanticSourceItem> Items,
    IReadOnlyList<InventorySemanticImage> Images);

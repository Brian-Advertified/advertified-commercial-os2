using System.IO.Compression;
using System.Security.Cryptography;
using System.Xml.Linq;
using Advertified.Commercial.Application.Inventory;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class NativeOfficeSourcePreprocessor
{
    internal static (IReadOnlyList<InventoryExtractedSourceElement>,
        IReadOnlyList<InventoryExtractedSourceImage>) ReadDocx(byte[] content)
    {
        using var archive = Open(content);
        var document = ReadXml(archive, "word/document.xml");
        var paragraphs = document.Descendants()
            .Where(node => node.Name.LocalName == "p")
            .Select(node => Text(node))
            .Where(value => value.Length > 0)
            .Select((value, index) => Element(
                $"docx:paragraph={index + 1}", "paragraph",
                index + 1, value))
            .ToArray();
        return (paragraphs, Images(archive, "word/media/", "docx"));
    }

    internal static (IReadOnlyList<InventoryExtractedSourceElement>,
        IReadOnlyList<InventoryExtractedSourceImage>) ReadPptx(byte[] content)
    {
        using var archive = Open(content);
        var elements = new List<InventoryExtractedSourceElement>();
        var images = new List<InventoryExtractedSourceImage>();
        foreach (var entry in OrderedParts(archive, "ppt/slides/slide", ".xml"))
        {
            var number = PartNumber(entry.Name, "slide");
            var slide = ReadXml(entry);
            var paragraphs = slide.Descendants()
                .Where(node => node.Name.LocalName == "p")
                .Select(node => Text(node))
                .Where(value => value.Length > 0)
                .ToArray();
            elements.AddRange(paragraphs.Select((value, index) => Element(
                $"pptx:slide={number};paragraph={index + 1}",
                "paragraph", index + 1, value)));
            images.AddRange(ReadSlideImages(archive, slide, number));
        }
        return (elements, images);
    }

    internal static (IReadOnlyList<InventoryExtractedSourceElement>,
        IReadOnlyList<InventoryExtractedSourceImage>) ReadXlsx(byte[] content)
    {
        using var archive = Open(content);
        var shared = ReadSharedStrings(archive);
        var elements = new List<InventoryExtractedSourceElement>();
        foreach (var entry in OrderedParts(archive, "xl/worksheets/sheet", ".xml"))
        {
            var sheet = PartNumber(entry.Name, "sheet");
            foreach (var row in ReadXml(entry).Descendants()
                         .Where(node => node.Name.LocalName == "row"))
            {
                var number = (int?)row.Attribute("r") ?? elements.Count + 1;
                var cells = row.Elements()
                    .Where(node => node.Name.LocalName == "c")
                    .Select(cell => CellText(cell, shared))
                    .Where(value => value.Length > 0)
                    .ToArray();
                if (cells.Length == 0) continue;
                elements.Add(Element(
                    $"xlsx:sheet={sheet};row={number}",
                    "row", number, string.Join(" | ", cells)));
            }
        }
        return (elements, Images(archive, "xl/media/", "xlsx"));
    }

    internal static InventoryExtractedSourceImage Image(
        string locator,
        string mediaType,
        byte[] content) => new(
        locator,
        mediaType,
        Convert.ToBase64String(content),
        content.Length,
        Convert.ToHexStringLower(SHA256.HashData(content)));

    private static IEnumerable<InventoryExtractedSourceImage> ReadSlideImages(
        ZipArchive archive,
        XDocument slide,
        int slideNumber)
    {
        var relationIds = slide.Descendants()
            .Where(node => node.Name.LocalName == "blip")
            .SelectMany(node => node.Attributes())
            .Where(attribute => attribute.Name.LocalName == "embed")
            .Select(attribute => attribute.Value)
            .ToArray();
        var relationsPath =
            $"ppt/slides/_rels/slide{slideNumber}.xml.rels";
        var relationships = archive.GetEntry(relationsPath);
        if (relationships is null) yield break;
        var targets = ReadXml(relationships).Descendants()
            .Where(node => node.Name.LocalName == "Relationship")
            .Where(node => relationIds.Contains((string?)node.Attribute("Id")))
            .Select(node => (string?)node.Attribute("Target"))
            .Where(value => value is not null)
            .Select(value => "ppt/" + value!.TrimStart('/', '.', '/'))
            .ToArray();
        var ordinal = 0;
        foreach (var target in targets)
        {
            var entry = archive.GetEntry(target);
            if (entry is null || MediaType(entry.Name) is not { } mediaType)
                continue;
            ordinal++;
            yield return Image(
                $"pptx:slide={slideNumber};image={ordinal}",
                mediaType,
                ReadBytes(entry));
        }
    }

    private static InventoryExtractedSourceImage[] Images(
        ZipArchive archive,
        string prefix,
        string locatorPrefix) => archive.Entries
        .Where(entry => entry.FullName.StartsWith(prefix, StringComparison.Ordinal))
        .Select(entry => (Entry: entry, MediaType: MediaType(entry.Name)))
        .Where(item => item.MediaType is not null)
        .Select((item, index) => Image(
            $"{locatorPrefix}:image={index + 1}",
            item.MediaType!, ReadBytes(item.Entry)))
        .ToArray();

    private static string[] ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        return entry is null ? [] : ReadXml(entry).Descendants()
            .Where(node => node.Name.LocalName == "si")
            .Select(Text)
            .ToArray();
    }

    private static string CellText(XElement cell, string[] shared)
    {
        var reference = (string?)cell.Attribute("r") ?? "cell";
        var raw = cell.Descendants()
            .FirstOrDefault(node => node.Name.LocalName is "v" or "t")?.Value
            ?.Trim() ?? string.Empty;
        if ((string?)cell.Attribute("t") == "s" &&
            int.TryParse(raw, out var index) &&
            index >= 0 && index < shared.Length)
            raw = shared[index];
        return raw.Length == 0 ? string.Empty : reference + "=" + raw;
    }

    private static InventoryExtractedSourceElement Element(
        string locator, string kind, int row, string value) =>
        new(locator, locator, kind, row, 1, value);

    private static string Text(XElement node) => string.Join(
        " ", node.Descendants().Where(child => child.Name.LocalName == "t")
            .Select(child => child.Value.Trim())
            .Where(value => value.Length > 0));

    private static ZipArchive Open(byte[] content) =>
        new(new MemoryStream(content, writable: false), ZipArchiveMode.Read);

    private static XDocument ReadXml(ZipArchive archive, string name) =>
        ReadXml(archive.GetEntry(name) ??
            throw new InventoryExtractionUnavailableException());

    private static XDocument ReadXml(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        return XDocument.Load(stream, LoadOptions.None);
    }

    private static IEnumerable<ZipArchiveEntry> OrderedParts(
        ZipArchive archive, string prefix, string suffix) => archive.Entries
        .Where(entry => entry.FullName.StartsWith(prefix, StringComparison.Ordinal) &&
            entry.FullName.EndsWith(suffix, StringComparison.Ordinal) &&
            !entry.FullName.Contains("/_rels/", StringComparison.Ordinal))
        .OrderBy(entry => PartNumber(entry.Name,
            prefix.EndsWith("slide", StringComparison.Ordinal) ? "slide" : "sheet"));

    private static int PartNumber(string name, string prefix) =>
        int.TryParse(Path.GetFileNameWithoutExtension(name)[prefix.Length..],
            out var value) ? value : int.MaxValue;

    private static byte[] ReadBytes(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var output = new MemoryStream();
        stream.CopyTo(output);
        return output.ToArray();
    }

    private static string? MediaType(string name) =>
        Path.GetExtension(name).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => null,
        };
}

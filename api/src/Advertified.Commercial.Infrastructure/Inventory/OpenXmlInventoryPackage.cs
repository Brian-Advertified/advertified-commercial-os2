using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using Advertified.Commercial.Application.Inventory;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal sealed class OpenXmlInventoryPackage : IDisposable
{
    private const long MaximumPartBytes = 64L * 1024L * 1024L;
    private const long MaximumExpandedPackageBytes =
        512L * 1024L * 1024L;
    private readonly ZipArchive archive;
    private readonly Dictionary<string, ZipArchiveEntry> entries;

    private OpenXmlInventoryPackage(ZipArchive archive)
    {
        this.archive = archive;
        if (archive.Entries.Count > 10_000 ||
            archive.Entries.Sum(entry => entry.Length) >
                MaximumExpandedPackageBytes)
            throw new InventoryExtractionUnavailableException();
        entries = new Dictionary<string, ZipArchiveEntry>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries.Where(
                     item => !string.IsNullOrEmpty(item.Name)))
        {
            if (!entries.TryAdd(
                    Normalize(entry.FullName), entry))
                throw new InventoryExtractionUnavailableException();
        }
    }

    internal static OpenXmlInventoryPackage Open(byte[] content)
    {
        try
        {
            var stream = new MemoryStream(content, writable: false);
            return new OpenXmlInventoryPackage(
                new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false));
        }
        catch (Exception error) when (
            error is InvalidDataException or OverflowException)
        {
            throw new InventoryExtractionUnavailableException();
        }
    }

    internal XDocument ReadRequired(string partPath) =>
        ReadOptional(partPath) ??
        throw new InventoryExtractionUnavailableException();

    internal XDocument? ReadOptional(string partPath)
    {
        var normalized = Normalize(partPath);
        if (!entries.TryGetValue(normalized, out var entry))
            return null;
        if (entry.Length > MaximumPartBytes)
            throw new InventoryExtractionUnavailableException();
        try
        {
            using var stream = entry.Open();
            using var reader = XmlReader.Create(stream, ReaderSettings());
            return XDocument.Load(reader, LoadOptions.None);
        }
        catch (Exception error) when (
            error is InvalidDataException or XmlException)
        {
            throw new InventoryExtractionUnavailableException();
        }
    }

    internal string RelationshipTarget(
        string sourcePart,
        string relationshipId)
    {
        var relationships = ReadRequired(RelationshipPart(sourcePart));
        XNamespace relationshipNamespace =
            "http://schemas.openxmlformats.org/package/2006/relationships";
        var target = relationships.Root?
            .Elements(relationshipNamespace + "Relationship")
            .SingleOrDefault(item =>
                (string?)item.Attribute("Id") == relationshipId)?
            .Attribute("Target")?.Value;
        return string.IsNullOrWhiteSpace(target)
            ? throw new InventoryExtractionUnavailableException()
            : ResolveTarget(sourcePart, target);
    }

    internal IReadOnlyDictionary<string, string>
        RelationshipTargets(string sourcePart)
    {
        var document = ReadOptional(
            RelationshipPart(sourcePart));
        if (document is null)
            return new Dictionary<string, string>(
                StringComparer.Ordinal);
        XNamespace relationshipNamespace =
            "http://schemas.openxmlformats.org/package/2006/relationships";
        var result = new Dictionary<string, string>(
            StringComparer.Ordinal);
        foreach (var item in document.Root?
                     .Elements(
                         relationshipNamespace + "Relationship")
                 ?? [])
        {
            var id = item.Attribute("Id")?.Value;
            var target = item.Attribute("Target")?.Value;
            var external = string.Equals(
                item.Attribute("TargetMode")?.Value,
                "External",
                StringComparison.OrdinalIgnoreCase);
            if (external)
                continue;
            if (string.IsNullOrWhiteSpace(id) ||
                string.IsNullOrWhiteSpace(target) ||
                !result.TryAdd(
                    id, ResolveTarget(sourcePart, target)))
            {
                throw new InventoryExtractionUnavailableException();
            }
        }
        return result;
    }

    internal bool Contains(string partPath) =>
        entries.ContainsKey(Normalize(partPath));

    internal bool HasPartPrefix(string prefix)
    {
        var normalized = Normalize(prefix);
        return entries.Keys.Any(path =>
            path.StartsWith(normalized, StringComparison.OrdinalIgnoreCase));
    }

    internal IReadOnlyList<string> ListParts(string prefix)
    {
        var normalized = Normalize(prefix);
        return entries.Keys
            .Where(path => path.StartsWith(
                normalized, StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    internal long PartLength(string partPath) =>
        entries.TryGetValue(Normalize(partPath), out var entry)
            ? entry.Length
            : throw new InventoryExtractionUnavailableException();

    internal byte[] ReadBytes(
        string partPath,
        int maximumBytes)
    {
        var normalized = Normalize(partPath);
        if (maximumBytes <= 0 ||
            !entries.TryGetValue(normalized, out var entry) ||
            entry.Length <= 0 || entry.Length > maximumBytes)
        {
            throw new InventoryExtractionUnavailableException();
        }
        var content = new byte[checked((int)entry.Length)];
        using var stream = entry.Open();
        stream.ReadExactly(content);
        return content;
    }

    public void Dispose() => archive.Dispose();

    private static XmlReaderSettings ReaderSettings() => new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        MaxCharactersInDocument = MaximumPartBytes,
        XmlResolver = null,
    };

    private static string RelationshipPart(string sourcePart)
    {
        var normalized = Normalize(sourcePart);
        var slash = normalized.LastIndexOf('/');
        var directory = slash < 0 ? string.Empty : normalized[..(slash + 1)];
        var name = slash < 0 ? normalized : normalized[(slash + 1)..];
        return directory + "_rels/" + name + ".rels";
    }

    private static string ResolveTarget(
        string sourcePart,
        string target)
    {
        var decoded = Uri.UnescapeDataString(
            target.Replace('\\', '/').Trim());
        if (decoded.StartsWith('/'))
            return Normalize(decoded);
        var source = Normalize(sourcePart);
        var slash = source.LastIndexOf('/');
        var directory = slash < 0 ? string.Empty : source[..(slash + 1)];
        return Normalize(directory + decoded);
    }

    private static string Normalize(string value)
    {
        var parts = new List<string>();
        foreach (var part in value.Replace('\\', '/')
                     .Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == ".") continue;
            if (part == "..")
            {
                if (parts.Count == 0)
                    throw new InventoryExtractionUnavailableException();
                parts.RemoveAt(parts.Count - 1);
                continue;
            }
            parts.Add(part);
        }
        return string.Join('/', parts);
    }
}

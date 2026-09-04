using System.Security.Cryptography;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class NativeOfficeImageReader
{
    internal const string RequiredBlocker =
        "EMBEDDED_IMAGE_SEMANTIC_EXTRACTION_REQUIRED";

    internal static bool IsRequired(
        IReadOnlyList<InventoryExtractedRow> rows) =>
        rows.Any(row => row.Values.TryGetValue(
            "extractionblocker", out var value) &&
            value == RequiredBlocker);

    internal static IReadOnlyList<InventorySemanticImage> Read(
        InventoryExtractionRequest request,
        InventorySemanticOptions settings) =>
        ReadContent(request, settings)
            .Select(image => new InventorySemanticImage(
                image.Ordinal,
                image.Locator,
                image.Format,
                image.Sha256,
                image.Content.Length,
                Convert.ToBase64String(image.Content)))
            .ToArray();

    internal static IReadOnlyList<InventoryOfficeImage> ReadContent(
        InventoryExtractionRequest request,
        InventorySemanticOptions settings)
    {
        var prefix = Prefix(request.DocumentClass);
        if (prefix is null)
            return [];

        using var package = OpenXmlInventoryPackage.Open(
            request.Content);
        var parts = package.ListParts(prefix)
            .Where(part =>
                Format(part) is not null &&
                package.PartLength(part) is > 0 &&
                package.PartLength(part) <= settings.MaximumImageBytes)
            .ToArray();
        if (parts.Length == 0)
            return [];
        if (parts.Length > settings.MaximumImagesPerDocument)
            throw new InventorySemanticInputRejectedException();

        var locators = NativeOfficeImageLocatorReader.Read(
            package, request.DocumentClass, parts);
        var result = new List<InventoryOfficeImage>();
        var totalBytes = 0L;
        foreach (var part in parts)
        {
            var format = Format(part) ??
                throw new InventorySemanticInputRejectedException();
            var content = package.ReadBytes(
                part, settings.MaximumImageBytes);
            if (!HasExpectedSignature(content, format))
                throw new InventorySemanticInputRejectedException();
            totalBytes += content.Length;
            if (totalBytes > settings.MaximumImageDocumentBytes)
                throw new InventorySemanticInputRejectedException();
            result.Add(new InventoryOfficeImage(
                result.Count + 1,
                locators[part],
                format,
                Convert.ToHexString(SHA256.HashData(content))
                    .ToLowerInvariant(),
                content));
        }
        return result;
    }

    internal static IReadOnlyList<InventorySemanticSourceItem>
        ReadExclusions(
            InventoryExtractionRequest request,
            InventorySemanticOptions settings)
    {
        var prefix = Prefix(request.DocumentClass);
        if (prefix is null)
            return [];
        using var package = OpenXmlInventoryPackage.Open(
            request.Content);
        var parts = package.ListParts(prefix);
        var locators = NativeOfficeImageLocatorReader.Read(
            package, request.DocumentClass, parts);
        return parts.Where(part =>
                Format(part) is null ||
                package.PartLength(part) <= 0 ||
                package.PartLength(part) > settings.MaximumImageBytes)
            .Select(part => new InventorySemanticSourceItem(
                locators[part],
                "UNSUPPORTED_EMBEDDED_ASSET",
                "format=" + Path.GetExtension(part).ToLowerInvariant() +
                ";bytes=" + package.PartLength(part) +
                ";requires_human_visual_review=true",
                null))
            .ToArray();
    }

    private static string? Prefix(string documentClass) =>
        documentClass switch
        {
            MasterDataCodes.DocumentClasses.Xlsx => "xl/media/",
            MasterDataCodes.DocumentClasses.Pptx => "ppt/media/",
            _ => null,
        };

    private static string? Format(string partPath) =>
        Path.GetExtension(partPath).ToLowerInvariant() switch
        {
            ".png" => "png",
            ".jpg" or ".jpeg" => "jpeg",
            ".gif" => "gif",
            ".webp" => "webp",
            _ => null,
        };

    private static bool HasExpectedSignature(
        byte[] content,
        string format) =>
        format switch
        {
            "png" => content.AsSpan().StartsWith(
                new byte[] { 0x89, 0x50, 0x4e, 0x47 }),
            "jpeg" => content.AsSpan().StartsWith(
                new byte[] { 0xff, 0xd8, 0xff }),
            "gif" => content.AsSpan().StartsWith("GIF8"u8),
            "webp" => content.AsSpan().StartsWith("RIFF"u8) &&
                content.Length >= 12 &&
                content.AsSpan(8).StartsWith("WEBP"u8),
            _ => false,
        };
}

internal sealed record InventoryOfficeImage(
    int Ordinal,
    string Locator,
    string Format,
    string Sha256,
    byte[] Content);

internal sealed record InventorySemanticImage(
    int Ordinal,
    string Locator,
    string Format,
    string Sha256,
    int ByteLength,
    string DataBase64);

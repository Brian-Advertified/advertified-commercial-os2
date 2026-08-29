using System.IO.Compression;
using System.Text;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal sealed record InventoryDocumentClass(string Code, string MediaType);

internal static class InventoryDocumentClassifier
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static InventoryDocumentClass Detect(
        string fileName,
        string declaredMediaType,
        byte[] content,
        int maximumSourceBytes)
    {
        ValidateSize(content, maximumSourceBytes);
        var detected = DetectBytes(content);
        ValidateExtension(fileName, detected.Code);
        ValidateMediaType(declaredMediaType, detected);
        return detected;
    }

    private static void ValidateSize(byte[] content, int maximumSourceBytes)
    {
        if (content.Length == 0 || content.Length > maximumSourceBytes)
        {
            throw new ArgumentException("The inventory file exceeds the configured size policy.");
        }
    }

    private static InventoryDocumentClass DetectBytes(byte[] content)
    {
        var span = content.AsSpan();
        if (span.StartsWith("%PDF-"u8))
        {
            return new(MasterDataCodes.DocumentClasses.Pdf, "application/pdf");
        }
        if (span.StartsWith(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }))
        {
            return new(MasterDataCodes.DocumentClasses.Png, "image/png");
        }
        if (span.StartsWith(new byte[] { 0xff, 0xd8, 0xff }))
        {
            return new(MasterDataCodes.DocumentClasses.Jpeg, "image/jpeg");
        }
        if (span.StartsWith("PK"u8))
        {
            return DetectOpenXml(content);
        }
        return DetectCsv(content);
    }

    private static InventoryDocumentClass DetectOpenXml(byte[] content)
    {
        try
        {
            using var stream = new MemoryStream(content, writable: false);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            if (archive.GetEntry("xl/workbook.xml") is not null)
            {
                return new(MasterDataCodes.DocumentClasses.Xlsx,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            }
            if (archive.GetEntry("word/document.xml") is not null)
            {
                return new(MasterDataCodes.DocumentClasses.Docx,
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document");
            }
        }
        catch (InvalidDataException)
        {
            throw new ArgumentException("The inventory file structure is invalid.");
        }
        throw new ArgumentException("The inventory file type is not supported.");
    }

    private static InventoryDocumentClass DetectCsv(byte[] content)
    {
        string text;
        try
        {
            text = StrictUtf8.GetString(content);
        }
        catch (DecoderFallbackException)
        {
            throw new ArgumentException("The inventory file type is not supported.");
        }
        var first = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        if (first is null || !first.Any(character => character is ',' or ';' or '\t' or '|'))
        {
            throw new ArgumentException("The inventory file type is not supported.");
        }
        return new(MasterDataCodes.DocumentClasses.Csv, "text/csv");
    }

    private static void ValidateExtension(string fileName, string detected)
    {
        var extension = Path.GetExtension(fileName).TrimStart('.').ToUpperInvariant();
        var expected = extension == "JPG" ? MasterDataCodes.DocumentClasses.Jpeg : extension;
        if (expected.Length == 0 || !string.Equals(expected, detected, StringComparison.Ordinal))
        {
            throw new ArgumentException("The file name does not match its content.");
        }
    }

    private static void ValidateMediaType(
        string declaredMediaType,
        InventoryDocumentClass detected)
    {
        var mediaType = declaredMediaType.Split(';', 2)[0].Trim();
        if (mediaType.Length == 0 || mediaType == "application/octet-stream")
        {
            return;
        }
        var csvAlias = detected.Code == MasterDataCodes.DocumentClasses.Csv &&
            mediaType is "application/csv" or "application/vnd.ms-excel";
        if (!csvAlias && !string.Equals(mediaType, detected.MediaType, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The declared file type does not match its content.");
        }
    }
}

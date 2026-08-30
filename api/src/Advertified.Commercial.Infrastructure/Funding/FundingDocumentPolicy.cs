using System.Security.Cryptography;
using Advertified.Commercial.Application.Funding;

namespace Advertified.Commercial.Infrastructure.Funding;

internal static class FundingDocumentPolicy
{
    private const int MaximumDocumentBytes = 10 * 1024 * 1024;
    private const int MaximumReasonLength = 1_000;
    private const int MaximumReferenceLength = 300;
    private static readonly byte[] PdfMagic = "%PDF"u8.ToArray();
    private static readonly byte[] PngMagic = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly byte[] JpegMagic = [255, 216, 255];

    internal static PreparedFundingDocument Prepare(FundingDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Content.Length is 0 or > MaximumDocumentBytes)
            throw new ArgumentException("The funding evidence file size is invalid.");
        var fileName = Path.GetFileName(Required(document.FileName, 255, "File name"));
        var mediaType = Required(document.MediaType, 100, "Media type").ToLowerInvariant();
        if (!Matches(mediaType, document.Content))
            throw new ArgumentException("The funding evidence file type is invalid.");
        return new PreparedFundingDocument(
            fileName, mediaType, document.Content,
            Convert.ToHexStringLower(SHA256.HashData(document.Content)));
    }

    internal static string PurchaseOrderNumber(string value) =>
        Required(value, 100, "Purchase order number");

    internal static string InvoiceNumber(string value) =>
        Required(value, 100, "Invoice number");

    internal static string Reason(string value) =>
        Required(value, MaximumReasonLength, "Reconciliation reason");

    internal static string Reference(string value) =>
        Required(value, MaximumReferenceLength, "Reconciliation reference");

    internal static string Currency(string value)
    {
        var currency = Required(value, 3, "Currency").ToUpperInvariant();
        return currency.Length == 3 ? currency : throw new ArgumentException("Currency is invalid.");
    }

    private static bool Matches(string mediaType, byte[] content) => mediaType switch
    {
        "application/pdf" => content.AsSpan().StartsWith(PdfMagic),
        "image/png" => content.AsSpan().StartsWith(PngMagic),
        "image/jpeg" => content.AsSpan().StartsWith(JpegMagic),
        _ => false,
    };

    private static string Required(string value, int maximumLength, string label)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"{label} is required.");
        var result = value.Trim();
        return result.Length <= maximumLength
            ? result
            : throw new ArgumentException($"{label} is too long.");
    }
}

internal sealed record PreparedFundingDocument(
    string FileName,
    string MediaType,
    byte[] Content,
    string Sha256);

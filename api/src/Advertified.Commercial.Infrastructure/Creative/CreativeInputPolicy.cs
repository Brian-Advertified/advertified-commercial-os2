using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Advertified.Commercial.Application.Creative;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Creative;

internal static partial class CreativeInputPolicy
{
    private const int MaximumCreativeBytes = 100 * 1024 * 1024;
    private static readonly byte[] PdfMagic = "%PDF"u8.ToArray();
    private static readonly byte[] PngMagic = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly byte[] JpegMagic = [255, 216, 255];

    internal static IReadOnlyList<PreparedCreativeRequirement> PrepareRequirements(
        IReadOnlyList<CreativeRequirementInput> inputs,
        IReadOnlyList<CreativeBookingSourceRow> sources)
    {
        if (inputs.Count == 0 || inputs.Count != sources.Count ||
            inputs.Select(item => item.BookingId).Distinct().Count() != inputs.Count)
            throw new CreativeReadinessBlockedException();
        var byBooking = sources.ToDictionary(item => item.BookingId);
        return inputs.Select(input => PrepareRequirement(input, byBooking)).ToArray();
    }

    internal static PreparedCreativeFile PrepareFile(
        CreativeFileUpload upload,
        CreativeRequirementSourceRow requirement)
    {
        ArgumentNullException.ThrowIfNull(upload);
        if (upload.Content.Length is 0 or > MaximumCreativeBytes ||
            upload.Content.Length > requirement.MaximumBytes)
            throw new CreativeFileRejectedException();
        var fileName = Path.GetFileName(Required(upload.FileName, 255, "File name"));
        var mediaType = Required(upload.MediaType, 100, "Media type").ToLowerInvariant();
        if (mediaType != requirement.RequiredMediaType || !Matches(mediaType, upload.Content))
            throw new CreativeFileRejectedException();
        return new PreparedCreativeFile(
            fileName, mediaType, upload.Content,
            Convert.ToHexStringLower(SHA256.HashData(upload.Content)));
    }

    internal static string Copy(string value) => Required(value, 5_000, "Approved copy");
    internal static string Reason(string value) => Required(value, 1_000, "Review reason");
    internal static string Evidence(string value) =>
        Required(value, 500, "Evidence reference");

    internal static string Rights(string value, bool approved)
    {
        var result = Required(value, 100, "Rights status").ToUpperInvariant();
        var known = result is MasterDataCodes.AssetRightsStatuses.Approved or
            MasterDataCodes.AssetRightsStatuses.Unknown or
            MasterDataCodes.AssetRightsStatuses.Restricted;
        if (!known || approved && result != MasterDataCodes.AssetRightsStatuses.Approved)
            throw new CreativeReadinessBlockedException();
        return result;
    }

    private static PreparedCreativeRequirement PrepareRequirement(
        CreativeRequirementInput input,
        Dictionary<Guid, CreativeBookingSourceRow> sources)
    {
        if (!sources.TryGetValue(input.BookingId, out var source) ||
            input.Width is <= 0 or > 20_000 || input.Height is <= 0 or > 20_000 ||
            input.MaximumBytes is <= 0 or > MaximumCreativeBytes)
            throw new CreativeReadinessBlockedException();
        var format = Required(input.FormatCode, 100, "Format code").ToUpperInvariant();
        var mediaType = Required(input.RequiredMediaType, 100, "Media type").ToLowerInvariant();
        if (!FormatCodePattern().IsMatch(format) || !SupportedMediaType(mediaType))
            throw new CreativeReadinessBlockedException();
        return new(
            Guid.NewGuid(), source, format, input.Width, input.Height, mediaType,
            input.MaximumBytes, Required(input.Instructions, 2_000, "Instructions"));
    }

    private static bool SupportedMediaType(string value) =>
        value is "image/png" or "image/jpeg" or "application/pdf";

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

    [GeneratedRegex("^[A-Z0-9][A-Z0-9_.-]{0,99}$", RegexOptions.CultureInvariant)]
    private static partial Regex FormatCodePattern();
}

internal sealed record PreparedCreativeFile(
    string FileName,
    string MediaType,
    byte[] Content,
    string Sha256);

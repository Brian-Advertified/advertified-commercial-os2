using System.Security.Cryptography;
using Advertified.Commercial.Application.Delivery;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Delivery;

internal static class DeliveryProofInputPolicy
{
    private const int MaximumBytes = 25 * 1024 * 1024;
    private static readonly byte[] PdfMagic = "%PDF"u8.ToArray();
    private static readonly byte[] PngMagic = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly byte[] JpegMagic = [255, 216, 255];

    internal static PreparedDeliveryProof Prepare(
        SubmitDeliveryProofCommand command,
        DeliveryProofSourceRow source,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(command.File);
        if (command.CapturedAtUtc.Offset != TimeSpan.Zero || command.CapturedAtUtc > now ||
            DateOnly.FromDateTime(command.CapturedAtUtc.UtcDateTime) < source.FlightStart ||
            DateOnly.FromDateTime(command.CapturedAtUtc.UtcDateTime) > source.FlightEnd)
            throw new DeliveryProofBlockedException();
        var proofType = ProofType(command.ProofType);
        var fileName = Required(
            Path.GetFileName(Required(command.File.FileName, 255, "File name")),
            255, "File name");
        var mediaType = Required(command.File.MediaType, 100, "Media type").ToLowerInvariant();
        if (command.File.Content is null ||
            command.File.Content.Length is 0 or > MaximumBytes ||
            !MediaMatchesType(proofType, mediaType) || !SignatureMatches(mediaType, command.File.Content))
            throw new DeliveryProofFileRejectedException();
        ValidateCoordinates(command.Latitude, command.Longitude);
        return new(
            proofType, fileName, mediaType, command.File.Content,
            Convert.ToHexStringLower(SHA256.HashData(command.File.Content)),
            command.CapturedAtUtc, Required(command.LocationDescription, 500, "Location"),
            command.Latitude, command.Longitude,
            Required(command.SourceReference, 500, "Source reference"),
            Required(command.Reason, 1_000, "Submission reason"));
    }

    internal static string ReviewReason(string value) =>
        Required(value, 1_000, "Review reason");

    private static string ProofType(string value)
    {
        var result = Required(value, 100, "Proof type").ToUpperInvariant();
        return result is MasterDataCodes.DeliveryProofTypes.Photo or
            MasterDataCodes.DeliveryProofTypes.Playlog or
            MasterDataCodes.DeliveryProofTypes.DeliveryReport
            ? result
            : throw new DeliveryProofBlockedException();
    }

    private static bool MediaMatchesType(string proofType, string mediaType) => proofType switch
    {
        MasterDataCodes.DeliveryProofTypes.Photo =>
            mediaType is "image/png" or "image/jpeg",
        MasterDataCodes.DeliveryProofTypes.Playlog or
            MasterDataCodes.DeliveryProofTypes.DeliveryReport => mediaType == "application/pdf",
        _ => false,
    };

    private static bool SignatureMatches(string mediaType, byte[] content) => mediaType switch
    {
        "application/pdf" => content.AsSpan().StartsWith(PdfMagic),
        "image/png" => content.AsSpan().StartsWith(PngMagic),
        "image/jpeg" => content.AsSpan().StartsWith(JpegMagic),
        _ => false,
    };

    private static void ValidateCoordinates(decimal? latitude, decimal? longitude)
    {
        if (latitude.HasValue != longitude.HasValue ||
            latitude is < -90 or > 90 || longitude is < -180 or > 180)
            throw new DeliveryProofBlockedException();
    }

    private static string Required(string value, int maximumLength, string label)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"{label} is required.");
        var result = value.Trim();
        return result.Length <= maximumLength
            ? result
            : throw new ArgumentException($"{label} is too long.");
    }
}

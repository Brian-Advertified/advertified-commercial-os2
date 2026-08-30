using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Advertified.Commercial.Application.Measurement;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Measurement;

internal static class PerformanceEvidenceInputPolicy
{
    private const int MaximumBytes = 25 * 1024 * 1024;
    private static readonly byte[] PdfMagic = "%PDF"u8.ToArray();
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static PreparedPerformanceEvidence Prepare(
        SubmitPerformanceEvidenceCommand command,
        PerformanceEvidenceSourceRow source,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(command.File);
        if (command.CapturedAtUtc.Offset != TimeSpan.Zero || command.CapturedAtUtc > now ||
            command.CapturedAtUtc < source.CompletedAtUtc)
            throw new PerformanceEvidenceBlockedException();
        var limitations = RequiredItems(command.Limitations, 20, 500, "limitation");
        var metrics = PrepareMetrics(command.Metrics, source);
        var fileName = Required(
            Path.GetFileName(Required(command.File.FileName, 255, "File name")),
            255, "File name");
        var mediaType = Required(command.File.MediaType, 100, "Media type").ToLowerInvariant();
        if (command.File.Content is null ||
            command.File.Content.Length is 0 or > MaximumBytes ||
            !HasValidSignature(mediaType, command.File.Content))
            throw new PerformanceEvidenceFileRejectedException();
        return new(
            Required(command.SourceReference, 500, "Source reference"),
            command.CapturedAtUtc, Required(command.Methodology, 2_000, "Methodology"),
            limitations, QualityStatus(command.QualityStatus), source.ReviewerUserId,
            fileName, mediaType, command.File.Content,
            Convert.ToHexStringLower(SHA256.HashData(command.File.Content)), metrics);
    }

    internal static string ReviewReason(string value) =>
        Required(value, 1_000, "Review reason");

    private static PreparedPerformanceMetric[] PrepareMetrics(
        IReadOnlyList<PerformanceMetricInput> metrics,
        PerformanceEvidenceSourceRow source)
    {
        if (metrics is null || metrics.Count is 0 or > 100)
            throw new PerformanceEvidenceBlockedException();
        var prepared = metrics.Select(item => PrepareMetric(item, source)).ToArray();
        if (prepared.GroupBy(item => new
            {
                item.MetricType,
                item.Unit,
                item.PeriodStart,
                item.PeriodEnd,
            }).Any(group => group.Count() > 1))
            throw new PerformanceEvidenceBlockedException();
        return prepared;
    }

    private static PreparedPerformanceMetric PrepareMetric(
        PerformanceMetricInput input,
        PerformanceEvidenceSourceRow source)
    {
        ArgumentNullException.ThrowIfNull(input);
        var metric = MetricType(input.MetricType);
        var unit = Unit(input.Unit);
        if (input.Value < 0 || input.PeriodEnd < input.PeriodStart ||
            input.PeriodStart < source.CampaignStart || input.PeriodEnd > source.CampaignEnd ||
            !UnitMatches(metric, unit) ||
            (unit == MasterDataCodes.MeasurementUnits.Percent && input.Value > 100))
            throw new PerformanceEvidenceBlockedException();
        return new(
            metric, input.Value, unit, input.PeriodStart, input.PeriodEnd,
            Required(input.SourceLocator, 500, "Metric source locator"));
    }

    private static string MetricType(string value)
    {
        var result = Required(value, 100, "Metric type").ToUpperInvariant();
        return result is MasterDataCodes.PerformanceMetricTypes.Impressions or
            MasterDataCodes.PerformanceMetricTypes.Reach or
            MasterDataCodes.PerformanceMetricTypes.Clicks or
            MasterDataCodes.PerformanceMetricTypes.Conversions or
            MasterDataCodes.PerformanceMetricTypes.Footfall or
            MasterDataCodes.PerformanceMetricTypes.ClickThroughRate or
            MasterDataCodes.PerformanceMetricTypes.ConversionRate
            ? result
            : throw new PerformanceEvidenceBlockedException();
    }

    private static string Unit(string value)
    {
        var result = Required(value, 100, "Measurement unit").ToUpperInvariant();
        return result is MasterDataCodes.MeasurementUnits.Count or
            MasterDataCodes.MeasurementUnits.People or
            MasterDataCodes.MeasurementUnits.Percent
            ? result
            : throw new PerformanceEvidenceBlockedException();
    }

    private static bool UnitMatches(string metric, string unit) => metric switch
    {
        MasterDataCodes.PerformanceMetricTypes.Reach or
            MasterDataCodes.PerformanceMetricTypes.Footfall =>
            unit == MasterDataCodes.MeasurementUnits.People,
        MasterDataCodes.PerformanceMetricTypes.ClickThroughRate or
            MasterDataCodes.PerformanceMetricTypes.ConversionRate =>
            unit == MasterDataCodes.MeasurementUnits.Percent,
        _ => unit == MasterDataCodes.MeasurementUnits.Count,
    };

    private static string QualityStatus(string value)
    {
        var result = Required(value, 100, "Quality status").ToUpperInvariant();
        return result is MasterDataCodes.MeasurementQualityStatuses.Verified or
            MasterDataCodes.MeasurementQualityStatuses.Limited or
            MasterDataCodes.MeasurementQualityStatuses.Unusable
            ? result
            : throw new PerformanceEvidenceBlockedException();
    }

    private static bool HasValidSignature(string mediaType, byte[] content) => mediaType switch
    {
        "application/pdf" => content.AsSpan().StartsWith(PdfMagic),
        "application/json" => IsJson(content),
        "text/csv" => IsCsv(content),
        _ => false,
    };

    private static bool IsJson(byte[] content)
    {
        try { using var document = JsonDocument.Parse(content); return document.RootElement.ValueKind
            is JsonValueKind.Array or JsonValueKind.Object; }
        catch (JsonException) { return false; }
    }

    private static bool IsCsv(byte[] content)
    {
        try
        {
            var text = StrictUtf8.GetString(content);
            return text.Contains(',') && (text.Contains('\n') || text.Contains('\r'));
        }
        catch (DecoderFallbackException) { return false; }
    }

    private static string[] RequiredItems(
        IReadOnlyList<string> values, int maximumCount, int maximumLength, string label)
    {
        if (values is null || values.Count is 0 || values.Count > maximumCount)
            throw new PerformanceEvidenceBlockedException();
        return values.Select(value => Required(value, maximumLength, label))
            .Distinct(StringComparer.Ordinal).ToArray();
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

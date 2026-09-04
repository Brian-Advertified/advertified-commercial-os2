using System.Globalization;
using System.Text.RegularExpressions;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static partial class InventoryCandidateNormalizer
{
    private static string Availability(
        Dictionary<string, string> values,
        List<InventoryFieldEvidenceView> evidence,
        string sourceHash,
        DateTimeOffset capturedAtUtc)
    {
        if (values.TryGetValue(
                "availability", out var suppliedAvailability))
        {
            return AvailabilityCode(suppliedAvailability);
        }
        var availability =
            MasterDataCodes.AvailabilityStatuses.PlanningAvailable;
        evidence.Add(Evidence(
            "availability",
            null,
            availability,
            MasterDataCodes.InventoryTransformationTypes.ExplicitUnknown,
            "policy:inventory-availability-default-v1",
            sourceHash,
            capturedAtUtc,
            MasterDataCodes.InventoryExtractionMethods.PolicyDefault,
            1m,
            MasterDataCodes.InventoryEvidenceBases.DerivedPolicy,
            MasterDataCodes.InventoryEvidenceStates.Unverified,
            MasterDataCodes.InventoryEvidenceActions.Review));
        return availability;
    }

    private static string? Currency(
        Dictionary<string, string> values,
        List<InventoryFieldEvidenceView> evidence,
        string locator,
        string sourceHash,
        DateTimeOffset capturedAtUtc)
    {
        var supplied = Code(values, "currency");
        if (supplied is not null)
        {
            return supplied == "R"
                ? MasterDataCodes.Currencies.Zar
                : supplied;
        }
        if (!values.TryGetValue("rate", out var raw) ||
            !InventoryMoneyParser.TryParse(
                raw, out _, out var parsedCurrency) ||
            parsedCurrency.Length == 0)
        {
            return null;
        }
        var rateEvidence = evidence.LastOrDefault(item =>
            item.FieldName == "rate");
        evidence.Add(Evidence(
            "currency",
            raw,
            parsedCurrency,
            MasterDataCodes.InventoryTransformationTypes
                .ParseCurrencyAmount,
            rateEvidence?.SourceLocator ?? locator,
            sourceHash,
            capturedAtUtc,
            MasterDataCodes.InventoryExtractionMethods.PolicyDefault,
            rateEvidence?.ExtractionConfidence,
            MasterDataCodes.InventoryEvidenceBases.DerivedPolicy));
        return parsedCurrency;
    }

    private static long? Rate(
        Dictionary<string, string> values)
    {
        if (values.TryGetValue("rate_minor", out var minor) &&
            long.TryParse(
                minor,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var exact))
        {
            return exact;
        }
        if (!values.TryGetValue("rate", out var major) ||
            InventoryMoneyParser.IsAmbiguousTruncatedRate(major))
        {
            return null;
        }
        return InventoryMoneyParser.TryParse(
                major,
                out var amount,
                out var parsedCurrency)
            ? MajorRateToMinor(
                amount,
                Code(values, "currency") ??
                (parsedCurrency.Length > 0
                    ? parsedCurrency
                    : null))
            : null;
    }

    private static decimal? Decimal(
        Dictionary<string, string> values,
        string field) =>
        values.TryGetValue(field, out var raw) &&
        decimal.TryParse(
            raw,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var result)
            ? result
            : null;

    private static string? Text(
        IReadOnlyDictionary<string, string> values,
        string field) =>
        values.TryGetValue(field, out var value) &&
        value.Trim().Length > 0
            ? value.Trim()
            : null;

    private static string? Code(
        IReadOnlyDictionary<string, string> values,
        string field) =>
        Text(values, field)?
            .ToUpperInvariant()
            .Replace(' ', '_');

    private static string? NormalizeField(
        string field,
        string value,
        Dictionary<string, string> values) =>
        field switch
        {
            "availability" => AvailabilityCode(value),
            "currency" when string.Equals(
                value.Trim(),
                "R",
                StringComparison.OrdinalIgnoreCase) =>
                MasterDataCodes.Currencies.Zar,
            "currency" when InventoryMoneyParser.TryParse(
                value, out _, out var parsedCurrency) &&
                parsedCurrency.Length > 0 => parsedCurrency,
            "rate_type" or "vat_treatment" =>
                Code(values, field),
            "channel" or "product_type" or "currency" =>
                value.Trim().ToUpperInvariant().Replace(' ', '_'),
            "name" => Text(values, "name"),
            "dimensions" => NormalizedDimensions(values),
            "rate" when
                InventoryMoneyParser.IsAmbiguousTruncatedRate(value) =>
                null,
            "rate" when InventoryMoneyParser.TryParse(
                    value,
                    out var amount,
                    out var parsedCurrency) =>
                MajorRateToMinor(
                    amount,
                    Code(values, "currency") ??
                    (parsedCurrency.Length > 0
                        ? parsedCurrency
                        : null))
                    ?.ToString(CultureInfo.InvariantCulture),
            _ => value.Trim(),
        };

    private static long? MajorRateToMinor(
        decimal amount,
        string? currency) =>
        currency is not null &&
        CurrencyMetadata.TryGetMinorUnitDigits(
            currency == "R"
                ? MasterDataCodes.Currencies.Zar
                : currency,
            out var digits)
            ? CurrencyMetadata.MajorToMinor(amount, digits)
            : null;

    private static string Transformation(
        string field,
        string header) =>
        field switch
        {
            "rate" or "currency" when header == "value" =>
                MasterDataCodes.InventoryTransformationTypes
                    .ParseCurrencyAmount,
            "rate" => MasterDataCodes.InventoryTransformationTypes
                .MajorToMinor,
            "latitude" or "longitude" =>
                MasterDataCodes.InventoryTransformationTypes.ParseDecimal,
            "dimensions" => MasterDataCodes.InventoryTransformationTypes
                .DerivedFromSourceContext,
            "channel" or "product_type" or "rate_type" or
                "currency" or "availability" or "vat_treatment" =>
                MasterDataCodes.InventoryTransformationTypes.UppercaseCode,
            _ => MasterDataCodes.InventoryTransformationTypes.Trim,
        };

    private static InventoryFieldEvidenceView Evidence(
        string field,
        string? raw,
        string? normalized,
        string transformation,
        string locator,
        string hash,
        DateTimeOffset capturedAtUtc,
        string extractionMethod,
        decimal? confidence = null,
        string? evidenceBasis = null,
        string? verificationState = null,
        string? requiredAction = null) =>
        new(
            field,
            raw,
            normalized,
            transformation,
            locator,
            hash,
            evidenceBasis ??
                MasterDataCodes.InventoryEvidenceBases.SupplierSupplied,
            verificationState ??
                MasterDataCodes.InventoryEvidenceStates.Unverified,
            requiredAction ??
                MasterDataCodes.InventoryEvidenceActions.Review,
            capturedAtUtc,
            null,
            null,
            extractionMethod,
            confidence);

    private static string AvailabilityCode(string value)
    {
        var code = value.Trim()
            .ToUpperInvariant()
            .Replace(' ', '_');
        if (code ==
                MasterDataCodes.AvailabilityExceptionTypes.NotAvailable ||
            code == "NOTAVAILABLE" ||
            code == MasterDataCodes.AvailabilityExceptionTypes.Blackout)
        {
            return MasterDataCodes.AvailabilityStatuses.Unavailable;
        }
        if (code is "IMMEDIATELY" or "IMMEDIATE" or "YES" ||
            code == MasterDataCodes.AvailabilityStatuses.Available)
        {
            return MasterDataCodes.AvailabilityStatuses.Available;
        }
        return code;
    }

    private static void ApplyContextualMappings(
        InventoryExtractedRow row,
        Dictionary<string, string> canonical,
        Dictionary<string, (string Header, string Value)> sources)
    {
        ApplyElementValuePair(row, canonical, sources);
        ApplyVatTreatment(canonical);
        ApplyRatePeriod(canonical, sources);
    }

    private static void ApplyElementValuePair(
        InventoryExtractedRow row,
        Dictionary<string, string> canonical,
        Dictionary<string, (string Header, string Value)> sources)
    {
        if (!row.Values.TryGetValue("element", out var element) ||
            !row.Values.TryGetValue("value", out var value) ||
            !InventoryMoneyParser.TryParse(value, out _, out var currency) ||
            currency.Length == 0)
        {
            return;
        }
        if (!canonical.ContainsKey("name") &&
            !string.IsNullOrWhiteSpace(element))
        {
            canonical["name"] = element;
            sources["name"] = ("element", element);
        }
        if (!canonical.ContainsKey("rate"))
        {
            canonical["rate"] = value;
            sources["rate"] = ("value", value);
        }
    }

    private static void ApplyVatTreatment(
        Dictionary<string, string> canonical)
    {
        if (!canonical.TryGetValue(
                "vat_treatment", out var raw) ||
            !bool.TryParse(raw, out var inclusive))
        {
            return;
        }
        canonical["vat_treatment"] = inclusive
            ? MasterDataCodes.VatTreatments.Inclusive
            : MasterDataCodes.VatTreatments.Exclusive;
    }

    private static void ApplyRatePeriod(
        Dictionary<string, string> canonical,
        Dictionary<string, (string Header, string Value)> sources)
    {
        if (!canonical.TryGetValue("rate_type", out var raw))
        {
            if (!canonical.TryGetValue("conditions", out var conditions))
                return;
            raw = conditions;
            sources["rate_type"] = ("conditions", conditions);
        }
        var normalized = new string(raw
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
        var rateType = normalized switch
        {
            "monthly" or "month" or "permonth" =>
                MasterDataCodes.RateTypes.MonthRate,
            "weekly" or "week" or "perweek" =>
                MasterDataCodes.RateTypes.WeekRate,
            "daily" or "day" or "perday" =>
                MasterDataCodes.RateTypes.DayRate,
            "spot" or "spotrate" =>
                MasterDataCodes.RateTypes.SpotRate,
            "package" or "packagerate" =>
                MasterDataCodes.RateTypes.PackageRate,
            "cpm" or "per1000" =>
                MasterDataCodes.RateTypes.Cpm,
            _ when Regex.IsMatch(
                raw,
                @"\bper\s+month\b|\bmonthly\b",
                RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant) =>
                MasterDataCodes.RateTypes.MonthRate,
            _ => null,
        };
        if (rateType is null)
        {
            canonical.Remove("rate_type");
            sources.Remove("rate_type");
            return;
        }
        canonical["rate_type"] = rateType;
    }
}

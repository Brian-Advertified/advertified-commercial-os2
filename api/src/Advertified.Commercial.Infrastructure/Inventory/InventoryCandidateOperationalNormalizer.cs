using System.Security.Cryptography;
using System.Text;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class InventoryCandidateOperationalNormalizer
{
    internal static ExtractedInventoryCandidate Normalize(
        ExtractedInventoryCandidate candidate)
    {
        var values = candidate.Values;
        var extension = new Dictionary<string, string>(
            values.Extension ?? new Dictionary<string, string>(),
            StringComparer.Ordinal);
        var productCode = values.ProductCode;
        if (string.IsNullOrWhiteSpace(productCode))
        {
            productCode = InternalProductCode(candidate);
            extension["productcodebasis"] =
                "DERIVED_INTERNAL_IDENTIFIER";
        }

        var geography = values.Geography;
        if (string.IsNullOrWhiteSpace(geography) &&
            IsNationalChannel(values.Channel))
        {
            geography = "South Africa";
            extension["geographybasis"] =
                "DERIVED_NATIONAL_MEDIA_SCOPE";
        }

        var rateType = values.RateType;
        if (string.IsNullOrWhiteSpace(rateType) &&
            values.RateAmountMinor.HasValue)
        {
            rateType = ExplicitRateType(candidate);
            if (rateType is not null)
            {
                extension["ratetypebasis"] =
                    "DERIVED_FROM_EXPLICIT_SOURCE_CONTEXT";
            }
        }
        if (!values.RateAmountMinor.HasValue)
        {
            extension["pricingstatus"] =
                InventoryPricingCodes.PendingSupplier;
        }

        return candidate with
        {
            Values = values with
            {
                ProductCode = productCode,
                Geography = geography,
                RateType = rateType,
                Extension = extension,
            },
        };
    }

    private static string InternalProductCode(
        ExtractedInventoryCandidate candidate)
    {
        var sourceHash = candidate.Evidence
            .Select(item => item.SourceHash)
            .FirstOrDefault(value =>
                !string.IsNullOrWhiteSpace(value))
            ?? Convert.ToHexStringLower(SHA256.HashData(
                Encoding.UTF8.GetBytes(candidate.Locator)));
        var identity = string.Join('|',
            sourceHash,
            candidate.RowNumber,
            candidate.Values.Name,
            candidate.Locator);
        var digest = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        return "ADV-" + sourceHash[..Math.Min(8, sourceHash.Length)]
            .ToUpperInvariant() + "-" + digest[..12];
    }

    private static bool IsNationalChannel(string? channel) =>
        channel is
            MasterDataCodes.Channels.Radio or
            MasterDataCodes.Channels.Tv or
            MasterDataCodes.Channels.Print or
            MasterDataCodes.Channels.Digital or
            MasterDataCodes.Channels.Social or
            MasterDataCodes.Channels.Podcast or
            MasterDataCodes.Channels.Email or
            MasterDataCodes.Channels.Mobile;

    private static string? ExplicitRateType(
        ExtractedInventoryCandidate candidate)
    {
        var context = string.Join(
            "\n",
            candidate.Evidence.Select(item => item.RawValue)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Append(candidate.Values.Name)
                .Append(candidate.Values.Description)
                .Append(candidate.Values.Deliverable?.BuyingUnit)
                .Where(value => !string.IsNullOrWhiteSpace(value)))
            .ToLowerInvariant();
        if (Contains(context, "cpm", "per thousand"))
            return MasterDataCodes.RateTypes.Cpm;
        if (Contains(context, "per day", "daily rate", "day rate"))
            return MasterDataCodes.RateTypes.DayRate;
        if (Contains(context, "per week", "weekly rate", "week rate"))
            return MasterDataCodes.RateTypes.WeekRate;
        if (Contains(context, "per month", "monthly rate", "month rate"))
            return MasterDataCodes.RateTypes.MonthRate;
        if (candidate.Values.Package is not null ||
            Contains(context, "package cost", "package rate"))
        {
            return MasterDataCodes.RateTypes.PackageRate;
        }
        if (candidate.Values.Channel == MasterDataCodes.Channels.Radio &&
            Contains(context, "spot", "time band", "net rate"))
        {
            return MasterDataCodes.RateTypes.SpotRate;
        }
        if (Contains(
                context,
                "once off",
                "per post",
                "per insertion",
                "per placement",
                "fixed fee"))
        {
            return MasterDataCodes.RateTypes.FlatRate;
        }
        return null;
    }

    private static bool Contains(
        string source,
        params string[] values) =>
        values.Any(value => source.Contains(
            value, StringComparison.OrdinalIgnoreCase));
}

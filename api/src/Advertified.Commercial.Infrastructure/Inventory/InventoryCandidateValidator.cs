using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal sealed record InventoryCodeSets(
    IReadOnlySet<string> Channels,
    IReadOnlySet<string> ProductTypes,
    IReadOnlySet<string> RateTypes,
    IReadOnlySet<string> Currencies,
    IReadOnlySet<string> Availability)
{
    internal static async Task<InventoryCodeSets> LoadAsync(
        GovernanceDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var required = new[]
        {
            "channels", "inventoryProductTypes", "rateTypes", "currencies",
            "availabilityStatuses",
        };
        var items = await dbContext.MasterDataItems.AsNoTracking()
            .Where(item => required.Contains(item.CollectionCode) && item.IsActive)
            .Select(item => new { item.CollectionCode, item.Code })
            .ToListAsync(cancellationToken);
        IReadOnlySet<string> Codes(string collection) => items
            .Where(item => item.CollectionCode == collection)
            .Select(item => item.Code).ToHashSet(StringComparer.Ordinal);
        return new(Codes("channels"), Codes("inventoryProductTypes"), Codes("rateTypes"),
            Codes("currencies"), Codes("availabilityStatuses"));
    }
}

internal static class InventoryCandidateValidator
{
    internal static IReadOnlyList<InventoryValidationIssueView> Validate(
        InventoryCandidateValues values,
        InventoryCodeSets codes)
    {
        var issues = new List<InventoryValidationIssueView>();
        Required(issues, "productCode", values.ProductCode);
        Required(issues, "name", values.Name);
        RequiredCode(issues, "channel", values.Channel, codes.Channels);
        RequiredCode(issues, "productType", values.ProductType, codes.ProductTypes);
        Required(issues, "geography", values.Geography);
        RequiredCode(issues, "rateType", values.RateType, codes.RateTypes);
        RequiredCode(issues, "currency", values.Currency, codes.Currencies);
        if (values.RateAmountMinor is null or < 0)
        {
            issues.Add(Block("rateAmountMinor", "RATE_REQUIRED", "A valid non-negative rate is required."));
        }
        RequiredCode(issues, "availability", values.Availability, codes.Availability);
        ValidateCoordinates(values, issues);
        if (values.Availability == Gate6Availability.Unknown)
        {
            issues.Add(new("availability", "AVAILABILITY_UNKNOWN",
                "Availability is not supplied and must be confirmed before booking.", false));
        }
        return issues;
    }

    private static void ValidateCoordinates(
        InventoryCandidateValues values,
        List<InventoryValidationIssueView> issues)
    {
        var paired = values.Latitude.HasValue == values.Longitude.HasValue;
        var range = values.Latitude is null or >= -90 and <= 90 &&
            values.Longitude is null or >= -180 and <= 180;
        if (!paired || !range)
        {
            issues.Add(Block("coordinates", "COORDINATES_INVALID",
                "Latitude and longitude must be supplied together and within valid ranges."));
        }
        if (values.Channel is Gate6Channels.Ooh or Gate6Channels.Dooh &&
            (!values.Latitude.HasValue || !values.Longitude.HasValue))
        {
            issues.Add(Block("coordinates", "OOH_COORDINATES_REQUIRED",
                "Out of home inventory requires verified coordinates."));
        }
    }

    private static void Required(
        List<InventoryValidationIssueView> issues,
        string field,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            issues.Add(Block(field, "FIELD_REQUIRED", $"{field} is required before publication."));
        }
    }

    private static void RequiredCode(
        List<InventoryValidationIssueView> issues,
        string field,
        string? value,
        IReadOnlySet<string> allowed)
    {
        if (string.IsNullOrWhiteSpace(value) || !allowed.Contains(value))
        {
            issues.Add(Block(field, "GOVERNED_CODE_REQUIRED",
                $"Select a supported {field} before publication."));
        }
    }

    private static InventoryValidationIssueView Block(
        string field,
        string code,
        string message) => new(field, code, message, true);
}

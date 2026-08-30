using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class InventoryQueryPolicy
{
    private const int MaximumSearchLength = 200;
    private const int MaximumSupplierLength = 500;
    private const int MaximumGeographyLength = 500;
    private static readonly HashSet<string> ActiveChannels =
        MasterDataRegistryReader.Read().Collections
            .Single(item => item.Code == MasterDataCodes.Channels.Collection).Items
            .Where(item => item.IsActive)
            .Select(item => item.Code)
            .ToHashSet(StringComparer.Ordinal);

    internal static InventorySearchQuery Validate(InventorySearchQuery query)
    {
        if (query.PageSize is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(query));
        }
        var channel = Optional(query.Channel, 100, nameof(query))?.ToUpperInvariant();
        if (channel is not null && !ActiveChannels.Contains(channel))
        {
            throw new ArgumentException("Choose a supported media type.", nameof(query));
        }
        return query with
        {
            Search = Optional(query.Search, MaximumSearchLength, nameof(query)),
            Channel = channel,
            Supplier = Optional(query.Supplier, MaximumSupplierLength, nameof(query)),
            Geography = Optional(query.Geography, MaximumGeographyLength, nameof(query)),
        };
    }

    private static string? Optional(string? value, int maximum, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        return normalized.Length <= maximum
            ? normalized
            : throw new ArgumentOutOfRangeException(parameter);
    }
}

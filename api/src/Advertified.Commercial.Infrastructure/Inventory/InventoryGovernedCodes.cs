using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class InventoryGovernedCodes
{
    internal static IReadOnlyDictionary<string, IReadOnlySet<string>> From(
        InventoryCodeSets codes) => new Dictionary<string, IReadOnlySet<string>>(
        StringComparer.Ordinal)
    {
        ["channel"] = codes.Channels,
        ["product_type"] = codes.ProductTypes,
        ["rate_type"] = codes.RateTypes,
        ["currency"] = codes.Currencies,
        ["availability"] = codes.Availability,
    };
}

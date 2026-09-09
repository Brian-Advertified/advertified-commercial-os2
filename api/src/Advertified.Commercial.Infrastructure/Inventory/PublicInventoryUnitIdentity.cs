using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class PublicInventoryUnitIdentity
{
    internal static PublicMediaUnitView? Project(PublicInventoryOwnerRow row)
    {
        var outletChannel = row.Channel is MasterDataCodes.Channels.Radio or
            MasterDataCodes.Channels.Tv or MasterDataCodes.Channels.Print;
        if (!outletChannel)
            return new(row.ProductId.ToString(), row.ProductName, null);
        if (string.IsNullOrWhiteSpace(row.OutletId) || string.IsNullOrWhiteSpace(row.OutletName))
            return null;
        return new(row.OutletId, row.OutletName, null);
    }

    internal static string CountBasis(string channel) => channel switch
    {
        "radio" => "canonical_radio_stations",
        "television" => "canonical_television_channels",
        "print" => "canonical_publications",
        _ => "published_inventory_products",
    };
}

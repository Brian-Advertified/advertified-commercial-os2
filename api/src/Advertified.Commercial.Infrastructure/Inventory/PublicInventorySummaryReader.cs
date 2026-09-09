using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed class PublicInventorySummaryReader(GovernanceDbContext dbContext)
    : IPublicInventorySummaryReader
{
    public async Task<PublicInventorySummaryView> GetAsync(
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.Database.SqlQuery<PublicInventoryOwnerRow>($"""
            SELECT channel_code AS "Channel", supplier_id AS "SupplierId",
                   supplier_name AS "SupplierName", product_id AS "ProductId",
                   product_name AS "ProductName", outlet_id AS "OutletId",
                   outlet_name AS "OutletName"
            FROM commercial.public_inventory_listing_directory
            """).ToArrayAsync(cancellationToken);
        return Project(rows);
    }

    internal static PublicInventorySummaryView Project(
        IReadOnlyCollection<PublicInventoryOwnerRow> rows)
    {
        var channels = rows
            .GroupBy(row => PublicChannel(row.Channel))
            .Where(group => group.Key is not null)
            .Select(group => Channel(group.Key!, group))
            .OrderBy(item => ChannelOrder(item.Channel))
            .ToArray();
        return new PublicInventorySummaryView(
            channels.Sum(item => item.Count), channels);
    }

    private static PublicInventoryChannelView Channel(string channel, IEnumerable<PublicInventoryOwnerRow> rows)
    {
        var units = rows.Select(PublicInventoryUnitIdentity.Project).OfType<PublicMediaUnitView>()
            .DistinctBy(item => item.Id).OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        return new(channel, units.Length, PublicInventoryUnitIdentity.CountBasis(channel), units);
    }

    private static string? PublicChannel(string channel) => channel switch
    {
        MasterDataCodes.Channels.Radio => "radio",
        MasterDataCodes.Channels.Tv => "television",
        MasterDataCodes.Channels.Print => "print",
        MasterDataCodes.Channels.Ooh or MasterDataCodes.Channels.Dooh => "out_of_home",
        MasterDataCodes.Channels.Digital => "digital",
        MasterDataCodes.Channels.Social => "social_media",
        MasterDataCodes.Channels.Experiential => "experiential",
        MasterDataCodes.Channels.Influencer => "influencer",
        _ => null,
    };

    private static int ChannelOrder(string channel) => channel switch
    {
        "out_of_home" => 10,
        "radio" => 20,
        "television" => 30,
        "print" => 40,
        "digital" => 50,
        "social_media" => 60,
        "experiential" => 70,
        "influencer" => 80,
        _ => 100,
    };
}

internal sealed class PublicInventoryOwnerRow
{
    public string Channel { get; set; } = string.Empty;
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? OutletId { get; set; }
    public string? OutletName { get; set; }
}

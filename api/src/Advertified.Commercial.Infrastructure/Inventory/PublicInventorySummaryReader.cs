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
                   supplier_name AS "SupplierName"
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
            .Select(group => new PublicInventoryChannelView(
                group.Key!,
                group.Select(row => row.SupplierId).Distinct().Count(),
                group.GroupBy(row => row.SupplierId)
                    .Select(owner => new PublicMediaOwnerView(
                        owner.Key, owner.First().SupplierName, null))
                    .OrderBy(owner => owner.Name, StringComparer.OrdinalIgnoreCase)
                    .ToArray()))
            .OrderBy(item => ChannelOrder(item.Channel))
            .ToArray();
        return new PublicInventorySummaryView(
            rows.Select(row => row.SupplierId).Distinct().Count(), channels);
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
}

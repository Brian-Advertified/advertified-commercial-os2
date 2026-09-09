using Advertified.Commercial.Infrastructure.Inventory;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class PublicInventorySummaryReaderTests
{
    [Fact]
    public void PublishedProjectionCountsCanonicalOutletsNotOwnersOrRateRows()
    {
        var sharedOwner = Guid.NewGuid();
        var rows = new[]
        {
            Owner("RADIO", sharedOwner, "Group", "Rate A", "Station FM"),
            Owner("RADIO", sharedOwner, "Group", "Rate B", "Station FM"),
            Owner("RADIO", sharedOwner, "Group", "Rate C", "Other FM"),
            Owner("TV", sharedOwner, "Group", "Programme A", "Channel One"),
            Owner("TV", sharedOwner, "Group", "Programme B", "Channel One"),
            Owner("TV", sharedOwner, "Group", "Unresolved package"),
        };

        var result = PublicInventorySummaryReader.Project(rows);

        Assert.Equal(3, result.TotalCount);
        Assert.Collection(result.Channels,
            radio =>
            {
                Assert.Equal("radio", radio.Channel);
                Assert.Equal(2, radio.Count);
                Assert.Equal("canonical_radio_stations", radio.CountBasis);
                Assert.Equal(["Other FM", "Station FM"], radio.Units.Select(item => item.Name));
            },
            television =>
            {
                Assert.Equal("television", television.Channel);
                Assert.Equal("canonical_television_channels", television.CountBasis);
                Assert.Single(television.Units);
            });
    }

    [Fact]
    public void OutdoorCountsProductsNotOwnersAndDoesNotClaimPhysicalSiteIdentity()
    {
        var owner = Guid.NewGuid();
        var first = Owner("OOH", owner, "Same owner", "Site A");
        var second = Owner("DOOH", owner, "Same owner", "Site B");
        var outdoor = Assert.Single(PublicInventorySummaryReader.Project([first, first, second]).Channels);
        Assert.Equal(2, outdoor.Count);
        Assert.Equal("published_inventory_products", outdoor.CountBasis);
    }

    [Fact]
    public void OutletChannelWithoutCanonicalIdentityIsOmittedRatherThanInferred()
    {
        var row = Owner("TV", Guid.NewGuid(), "eMedia", "Channel-looking product title");
        Assert.Null(PublicInventoryUnitIdentity.Project(row));
    }

    private static PublicInventoryOwnerRow Owner(
        string channel, Guid supplierId, string supplierName, string productName,
        string? outletName = null) => new()
        {
            Channel = channel,
            SupplierId = supplierId,
            SupplierName = supplierName,
            ProductId = Guid.NewGuid(),
            ProductName = productName,
            OutletId = outletName is null ? null : InventoryOutletIdentity.StableId(channel, outletName),
            OutletName = outletName,
        };
}

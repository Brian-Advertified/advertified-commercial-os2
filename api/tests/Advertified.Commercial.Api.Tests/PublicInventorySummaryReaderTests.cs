using Advertified.Commercial.Infrastructure.Inventory;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class PublicInventorySummaryReaderTests
{
    [Fact]
    public void PublishedOwnerProjectionCombinesOohAndDoohWithoutDoubleCounting()
    {
        var sharedOwner = Guid.NewGuid();
        var rows = new[]
        {
            Owner("OOH", sharedOwner, "Shared Outdoor"),
            Owner("DOOH", sharedOwner, "Shared Outdoor"),
            Owner("DOOH", Guid.NewGuid(), "Digital Outdoor"),
            Owner("RADIO", Guid.NewGuid(), "Local Radio"),
        };

        var result = PublicInventorySummaryReader.Project(rows);

        Assert.Equal(3, result.TotalCount);
        Assert.Collection(result.Channels,
            outdoor =>
            {
                Assert.Equal("out_of_home", outdoor.Channel);
                Assert.Equal(2, outdoor.Count);
                Assert.Equal(2, outdoor.Owners.Count);
            },
            radio =>
            {
                Assert.Equal("radio", radio.Channel);
                Assert.Single(radio.Owners);
            });
    }

    private static PublicInventoryOwnerRow Owner(
        string channel,
        Guid supplierId,
        string supplierName) => new()
        {
            Channel = channel,
            SupplierId = supplierId,
            SupplierName = supplierName,
        };
}

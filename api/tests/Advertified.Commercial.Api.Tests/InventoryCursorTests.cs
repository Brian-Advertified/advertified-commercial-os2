using Advertified.Commercial.Infrastructure.Inventory;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class InventoryCursorTests
{
    [Fact]
    public void ProductCursorRetainsSupplierProductAndIdentityOrderingKeys()
    {
        var id = Guid.Parse("ca700000-0000-0000-0000-000000000001");

        var cursor = InventoryCursor.Decode(
            InventoryCursor.Encode("arena holdings", "business day", id));

        Assert.NotNull(cursor);
        Assert.Equal("arena holdings", cursor.Supplier);
        Assert.Equal("business day", cursor.Name);
        Assert.Equal(id, cursor.Id);
    }

    [Theory]
    [InlineData("", "product")]
    [InlineData("supplier", "")]
    public void ProductCursorRejectsMissingOrderingKeys(string supplier, string name)
    {
        var encoded = InventoryCursorCodec.Encode(
            new InventoryCursorValue(supplier, name, Guid.NewGuid()));

        Assert.Throws<ArgumentException>(() => InventoryCursor.Decode(encoded));
    }
}

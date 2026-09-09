using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class MarketplaceAcceptanceTests
{
    private static async Task AssertPublicInventoryUnitsAsync(WebApplicationFactory<Program> factory,
        int expectedCount = 1)
    {
        // An expired local identity cannot authenticate, but the public projection must remain accessible.
        await using var anonymousFactory = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Authentication:DevelopmentIdentity:ExpiresAtUtc", "2000-01-01T00:00:00Z"));
        using var anonymous = anonymousFactory.CreateClient();
        using var response = await anonymous.GetAsync("/api/v1/public/inventory-summary");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        using var payload = JsonDocument.Parse(text);
        Assert.Equal(expectedCount, payload.RootElement.GetProperty("totalCount").GetInt32());
        var channels = payload.RootElement.GetProperty("channels").EnumerateArray().ToArray();
        if (expectedCount > 0)
        {
            var outdoor = Assert.Single(channels);
            Assert.Equal("published_inventory_products", outdoor.GetProperty("countBasis").GetString());
            Assert.Equal(1, outdoor.GetProperty("count").GetInt32());
            var unit = Assert.Single(outdoor.GetProperty("units").EnumerateArray());
            Assert.Equal(ProductId.ToString(), unit.GetProperty("id").GetString());
        }
        else Assert.Empty(channels);
        Assert.DoesNotContain("owners", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("supplierId", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("amountMinor", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("latitude", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sourceLocator", text, StringComparison.OrdinalIgnoreCase);
    }
}

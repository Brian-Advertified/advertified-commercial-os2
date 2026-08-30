using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class MarketplaceAcceptanceTests
{
    [Fact]
    [Trait("Category", "Migration")]
    public async Task ReviewedInventorySupportsTenantSafeBuyerSupplierExchange()
    {
        await using var postgres = CreatePostgres();
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DisposableDatabaseRoles.ProvisionAsync(connectionString);
        await SeedAsync(connectionString);
        var clock = new AdjustableMarketplaceClock(InitialTime);
        await using var supplierFactory = CreateFactory(
            connectionString, SupplierUserId, clock);
        await using var buyerFactory = CreateFactory(connectionString, BuyerUserId, clock);
        await using var otherFactory = CreateFactory(connectionString, OtherUserId, clock);
        using var supplier = supplierFactory.CreateClient();
        using var buyer = buyerFactory.CreateClient();
        using var other = otherFactory.CreateClient();

        var listing = await CreateAndPublishListingAsync(supplier, buyer);
        await CompleteAcceptedExchangeAsync(
            buyer, supplier, other, listing.ListingVersionId, clock);
        await AssertExpiredResponseCannotBeAcceptedAsync(
            buyer, supplier, listing.ListingVersionId, clock);
        await ArchiveListingAsync(supplier, buyer, listing.ListingId);
        await AssertRetainedEvidenceAsync(
            connectionString, listing.ListingVersionId, expectedCommands: 10);
    }
}

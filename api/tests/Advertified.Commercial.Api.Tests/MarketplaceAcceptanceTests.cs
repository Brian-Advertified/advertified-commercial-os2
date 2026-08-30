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
        var plan = await BuildBuyerPlanAsync(buyer, listing.ListingVersionId);
        await CompleteAcceptedExchangeAsync(
            buyer, supplier, other, listing.ListingVersionId, clock);
        await AssertExpiredResponseCannotBeAcceptedAsync(
            buyer, supplier, listing.ListingVersionId, clock);
        await AssertFilteredRequestPagingAsync(
            buyer, listing.ListingVersionId, clock);
        await AssertInvalidMarketplaceFiltersAsync(buyer);
        await ArchiveListingAsync(supplier, buyer, listing.ListingId);
        await AssertArchivedListingInvalidatesPlanAsync(buyer, plan);
        await AssertRetainedEvidenceAsync(
            connectionString, listing.ListingVersionId, expectedCommands: 12);
    }
}

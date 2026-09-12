using Npgsql;
using System.Globalization;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class MarketplaceAcceptanceTests
{
    [Fact]
    [Trait("Category", "Migration")]
    public async Task ReviewedInventoryProjectionUsesOnlyCurrentAvailabilityAndPreservesArchive()
    {
        await using var postgres = CreatePostgres();
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DisposableDatabaseRoles.ProvisionAsync(connectionString);
        await SeedAsync(connectionString);
        await AddCurrentRateAsync(connectionString);

        await SetProductStatusAsync(connectionString, "INACTIVE");
        await ApplyInventoryProjectionAsync(connectionString);
        Assert.Equal(0, await ListingCountAsync(connectionString));

        await SetProductStatusAsync(connectionString, "ACTIVE");
        await AddAvailabilityAsync(connectionString, "UNAVAILABLE", -3, 30);
        await ApplyInventoryProjectionAsync(connectionString);
        Assert.Equal(0, await ListingCountAsync(connectionString));

        await AddAvailabilityAsync(connectionString, "AVAILABLE", -2, -1);
        await ApplyInventoryProjectionAsync(connectionString);
        Assert.Equal(0, await ListingCountAsync(connectionString));

        var currentAvailabilityId = await AddAvailabilityAsync(
            connectionString, "AVAILABLE", -1, 30);
        await ApplyInventoryProjectionAsync(connectionString);
        await ApplyInventoryProjectionAsync(connectionString);
        var published = await ReadProjectionStateAsync(connectionString);
        Assert.Equal("PUBLISHED", published.Status);
        Assert.Equal(1, published.VersionCount);
        Assert.Equal(currentAvailabilityId, published.AvailabilityId);

        await ArchiveListingAsync(connectionString);
        await AddCurrentRateAsync(connectionString);
        await ApplyInventoryProjectionAsync(connectionString);
        var archived = await ReadProjectionStateAsync(connectionString);
        Assert.Equal("ARCHIVED", archived.Status);
        Assert.Equal(1, archived.VersionCount);
    }

    [Fact]
    [Trait("Category", "Migration")]
    public async Task SupplierPublishRejectsExpiredAvailabilityEvidence()
    {
        await using var postgres = CreatePostgres();
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DisposableDatabaseRoles.ProvisionAsync(connectionString);
        await SeedAsync(connectionString);
        await AddExpiredAvailabilityAsync(connectionString);
        var clock = new AdjustableMarketplaceClock(InitialTime.AddHours(2));
        await using var factory = CreateFactory(connectionString, SupplierUserId, clock);
        using var supplier = factory.CreateClient();

        using var draft = await CommandAsync(
            supplier, SupplierTenantId, "marketplace-listings", "expired-listing-create",
            null, new { productId = ProductId, terms = "Subject to current availability." });
        var listingId = draft.RootElement.GetProperty("id").GetGuid();
        using var response = await RawCommandAsync(
            supplier, SupplierTenantId, $"marketplace-listings/{listingId}:publish",
            "expired-listing-publish", 1, new { });

        await AssertProblemAsync(
            response, System.Net.HttpStatusCode.Conflict, "MARKETPLACE_LISTING_UNAVAILABLE");
    }

    private static async Task ApplyInventoryProjectionAsync(string connectionString)
    {
        var path = Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "publish-current-inventory-to-marketplace.sql");
        var sql = "SET ROLE advertified_app;\n" + await File.ReadAllTextAsync(path);
        sql = sql.Replace("\\set ON_ERROR_STOP on", string.Empty, StringComparison.Ordinal)
            .Replace("10000000-0000-0000-0000-000000000002",
                SupplierTenantId.ToString(), StringComparison.Ordinal)
            .Replace("10000000-0000-0000-0000-000000000001",
                SupplierUserId.ToString(), StringComparison.Ordinal);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static Task SetProductStatusAsync(string connectionString, string status) =>
        ExecuteAsync(connectionString,
            "UPDATE commercial.inventory_products SET status_code = $1 WHERE id = $2",
            status, ProductId);

    private static async Task<Guid> AddAvailabilityAsync(
        string connectionString, string status, int observedDays, int validDays)
    {
        var id = Guid.NewGuid();
        await ExecuteAsync(connectionString, """
            INSERT INTO commercial.inventory_availability (
                id, tenant_id, product_version_id, availability_code,
                observed_at_utc, valid_until_utc, source_locator)
            VALUES ($1, $2, $3, $4, clock_timestamp() + make_interval(days => $5),
                clock_timestamp() + make_interval(days => $6), 'projection-regression')
            """, id, SupplierTenantId, ProductVersionId, status, observedDays, validDays);
        return id;
    }

    private static Task ArchiveListingAsync(string connectionString) =>
        ExecuteAsync(connectionString, """
            UPDATE commercial.marketplace_listings
            SET status_code = 'ARCHIVED', archived_reason = 'Supplier withdrew this listing.'
            WHERE supplier_tenant_id = $1 AND product_id = $2
            """, SupplierTenantId, ProductId);

    private static Task AddCurrentRateAsync(string connectionString) =>
        ExecuteAsync(connectionString, """
            INSERT INTO commercial.inventory_rates (
                id, tenant_id, product_version_id, rate_type_code, currency_code,
                amount_minor, effective_from, effective_to, source_locator,
                vat_treatment_code, commercial_terms_json)
            VALUES ($1, $2, $3, 'CPM', 'ZAR', 1300000,
                CURRENT_DATE, CURRENT_DATE + 30, 'new-rate', 'INCLUSIVE', '{}'::jsonb)
            """, Guid.NewGuid(), SupplierTenantId, ProductVersionId);

    private static Task AddExpiredAvailabilityAsync(string connectionString) =>
        ExecuteAsync(connectionString, """
            INSERT INTO commercial.inventory_availability (
                id, tenant_id, product_version_id, availability_code,
                observed_at_utc, valid_until_utc, source_locator)
            VALUES ($1, $2, $3, 'AVAILABLE', $4, $5, 'expired-confirmation')
            """, Guid.NewGuid(), SupplierTenantId, ProductVersionId,
            InitialTime.AddHours(1), InitialTime.AddMinutes(90));

    private static async Task<int> ListingCountAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM commercial.marketplace_listings WHERE product_id = $1", connection);
        command.Parameters.AddWithValue(ProductId);
        return Convert.ToInt32(
            await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private static async Task<ProjectionState> ReadProjectionStateAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT listing.status_code, count(version.id), current_version.availability_id
            FROM commercial.marketplace_listings listing
            JOIN commercial.marketplace_listing_versions current_version
              ON current_version.id = listing.current_version_id
            JOIN commercial.marketplace_listing_versions version
              ON version.listing_id = listing.id
            WHERE listing.product_id = $1
            GROUP BY listing.status_code, current_version.availability_id
            """, connection);
        command.Parameters.AddWithValue(ProductId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return new(reader.GetString(0), reader.GetInt32(1), reader.GetGuid(2));
    }

    private static async Task ExecuteAsync(
        string connectionString, string sql, params object[] values)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        for (var index = 0; index < values.Length; index++)
            command.Parameters.AddWithValue(values[index]);
        await command.ExecuteNonQueryAsync();
    }

    private sealed record ProjectionState(
        string Status, int VersionCount, Guid AvailabilityId);
}

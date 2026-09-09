using System.Text.Json;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class CanonicalPlanningAcceptanceTests
{
    private static readonly Guid ResearchCreatorId = Guid.Parse("72000000-0000-0000-0000-000000000091");
    private static readonly Guid ResearchReviewerId = Guid.Parse("72000000-0000-0000-0000-000000000092");
    private static readonly Guid ResearchListingId = Guid.Parse("72000000-0000-0000-0000-000000000093");
    private static readonly Guid ResearchListingVersionId = Guid.Parse("72000000-0000-0000-0000-000000000094");
    private static readonly string[] ResearchPortfolioLocators =
        ["research:placement:1", "research:placement:2"];

    [Fact]
    [Trait("Category", "Migration")]
    public async Task ApprovedResearchCreatesNewInventoryVersionsAndExactPortfolioEvidence()
    {
        await using var postgres = CreatePostgres();
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DisposableDatabaseRoles.ProvisionAsync(connectionString);
        await SeedAsync(connectionString);
        await SeedResearchReviewersAsync(connectionString);
        await SeedPublishedResearchListingAsync(connectionString, "PLANNING-1");
        await using var creatorFactory = CreateFactory(connectionString, ResearchCreatorId);
        await using var reviewerFactory = CreateFactory(connectionString, ResearchReviewerId);
        using var creator = creatorFactory.CreateClient();
        using var reviewer = reviewerFactory.CreateClient();

        using var registered = await CommandAsync(
            creator, Path("inventory-research"), "research-register", 0,
            new
            {
                sourceName = "Authorised Research",
                datasetName = "Johannesburg audience release",
                measurementPeriod = "2026 Q1",
                methodology = "Weighted panel methodology",
                universe = "Johannesburg adults 18+",
                rightsReference = "fixture:licensed-for-planning",
                taxonomyName = (string?)null,
                taxonomyVersion = (string?)null,
                limitations = "Synthetic acceptance evidence only.",
                observations = new[]
                {
                    Observation("research:placement:1", "PLANNING-1", 120m, 300m),
                    Observation("research:placement:2", "PLANNING-2", 100m, 240m),
                },
                portfolios = new[]
                {
                    new
                    {
                        sourceLocator = "research:portfolio:1-2",
                        observationSourceLocators = ResearchPortfolioLocators,
                        deduplicatedReach = 180m,
                        unit = "PEOPLE",
                    },
                },
            });
        Assert.Equal("IN_REVIEW", registered.RootElement.GetProperty("status").GetString());
        var matches = registered.RootElement.GetProperty("matches").EnumerateArray().ToArray();
        Assert.Equal(2, matches.Length);
        Assert.All(matches, item => Assert.NotEqual(Guid.Empty, item.GetProperty("productVersionId").GetGuid()));

        JsonDocument? reviewed = null;
        for (var index = 0; index < matches.Length; index++)
        {
            var match = matches[index];
            reviewed?.Dispose();
            reviewed = await CommandAsync(
                reviewer,
                Path($"inventory-research/matches/{match.GetProperty("id").GetGuid()}:review"),
                $"research-review-{index + 1}", match.GetProperty("version").GetInt64(),
                new { decision = "APPROVE", reason = "Licensed provenance and exact inventory identity verified." });
        }
        using (reviewed)
        {
            Assert.NotNull(reviewed);
            Assert.Equal("COMPLETED", reviewed.RootElement.GetProperty("status").GetString());
            var portfolio = Assert.Single(reviewed.RootElement.GetProperty("portfolios").EnumerateArray());
            Assert.Equal("COMPLETED", portfolio.GetProperty("status").GetString());
            Assert.Equal(2, portfolio.GetProperty("productVersionIds").GetArrayLength());
        }

        await AssertResearchVersioningAsync(connectionString);
    }

    private static object Observation(string locator, string productCode, decimal reach, decimal impressions) => new
    {
        sourceLocator = locator,
        channel = "OOH",
        supplierId = SupplierId,
        supplierProductCode = productCode,
        outletId = (string?)null,
        geography = "Johannesburg",
        audienceProfile = new
        {
            spokenLanguages = new[] { new { label = "English", sharePercent = 80m } },
            understoodLanguages = Array.Empty<object>(),
            lifeStages = new[] { new { label = "Business decision makers", sharePercent = 60m } },
            lsmSemSegments = Array.Empty<object>(),
            taxonomyName = (string?)null,
            taxonomyVersion = (string?)null,
            universe = "Johannesburg adults 18+",
            measurementSource = "Authorised Research",
            measurementPeriod = "2026 Q1",
            methodology = "Weighted panel methodology",
            limitations = "Synthetic acceptance evidence only.",
            measurements = new[]
            {
                new { metricType = "REACH", value = (decimal?)reach, unit = "PEOPLE", universe = "Johannesburg adults 18+", measurementSource = "Authorised Research", measurementPeriod = "2026 Q1", methodology = "Weighted panel methodology", limitations = "Synthetic acceptance evidence only." },
                new { metricType = "IMPRESSIONS", value = (decimal?)impressions, unit = "COUNT", universe = "Johannesburg adults 18+", measurementSource = "Authorised Research", measurementPeriod = "2026 Q1", methodology = "Weighted panel methodology", limitations = "Synthetic acceptance evidence only." },
            },
        },
    };

    private static async Task SeedResearchReviewersAsync(string connectionString)
    {
        var options = new DbContextOptionsBuilder<GovernanceDbContext>().UseNpgsql(connectionString).Options;
        await using var db = new GovernanceDbContext(options);
        db.Users.AddRange(
            CreateUser(ResearchCreatorId, "research.creator@planning.example", "Research Creator"),
            CreateUser(ResearchReviewerId, "research.reviewer@planning.example", "Research Reviewer"));
        db.Memberships.AddRange(
            CreateMembership(TenantId, ResearchCreatorId, "inventory_ops", 91),
            CreateMembership(TenantId, ResearchReviewerId, "inventory_ops", 92));
        await db.SaveChangesAsync();
    }

    private static async Task SeedPublishedResearchListingAsync(
        string connectionString, string productCode)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var listing = new NpgsqlCommand("""
            INSERT INTO commercial.marketplace_listings (
                id, supplier_tenant_id, product_id, status_code, terms,
                created_by, version, created_at_utc, updated_at_utc)
            SELECT $1, product.tenant_id, product.id, 'DRAFT',
                'Synthetic published research listing.', $2, 1, clock_timestamp(), clock_timestamp()
            FROM commercial.inventory_products product
            WHERE product.tenant_id = $3 AND product.supplier_product_code = $4
            """, connection, transaction))
        {
            listing.Parameters.AddWithValue(ResearchListingId);
            listing.Parameters.AddWithValue(ResearchReviewerId);
            listing.Parameters.AddWithValue(TenantId);
            listing.Parameters.AddWithValue(productCode);
            Assert.Equal(1, await listing.ExecuteNonQueryAsync());
        }
        await using (var snapshot = new NpgsqlCommand("""
            INSERT INTO commercial.marketplace_listing_versions (
                id, supplier_tenant_id, listing_id, version_number,
                product_version_id, rate_id, availability_id, supplier_name,
                product_name, channel_code, product_type_code, geography,
                rate_type_code, amount_minor, currency_code, availability_code,
                terms, published_by, published_at_utc, supplier_id,
                rate_source_locator, availability_source_locator, audience_profile_json,
                outlet_id, outlet_name, outlet_identity_basis, outlet_source_locator)
            SELECT $1, product.tenant_id, $2, 1, version.id, rate.id, availability.id,
                supplier.name, version.name, version.channel_code, version.product_type_code,
                version.geography, rate.rate_type_code, rate.amount_minor, rate.currency_code,
                availability.availability_code, 'Synthetic published research listing.',
                $3, clock_timestamp(), product.supplier_id, rate.source_locator,
                availability.source_locator, version.audience_profile_json,
                version.outlet_id, version.outlet_name, version.outlet_identity_basis,
                version.outlet_source_locator
            FROM commercial.inventory_products product
            JOIN commercial.inventory_product_versions version
              ON version.tenant_id = product.tenant_id AND version.id = product.current_version_id
            JOIN commercial.inventory_suppliers supplier
              ON supplier.tenant_id = product.tenant_id AND supplier.id = product.supplier_id
            JOIN LATERAL (SELECT item.* FROM commercial.inventory_rates item
                WHERE item.tenant_id = version.tenant_id AND item.product_version_id = version.id
                ORDER BY item.id LIMIT 1) rate ON TRUE
            JOIN LATERAL (SELECT item.* FROM commercial.inventory_availability item
                WHERE item.tenant_id = version.tenant_id AND item.product_version_id = version.id
                ORDER BY item.id LIMIT 1) availability ON TRUE
            WHERE product.tenant_id = $4 AND product.supplier_product_code = $5
            """, connection, transaction))
        {
            snapshot.Parameters.AddWithValue(ResearchListingVersionId);
            snapshot.Parameters.AddWithValue(ResearchListingId);
            snapshot.Parameters.AddWithValue(ResearchReviewerId);
            snapshot.Parameters.AddWithValue(TenantId);
            snapshot.Parameters.AddWithValue(productCode);
            Assert.Equal(1, await snapshot.ExecuteNonQueryAsync());
        }
        await using (var publish = new NpgsqlCommand("""
            UPDATE commercial.marketplace_listings
            SET current_version_id = $1, status_code = 'PUBLISHED',
                version = 2, updated_at_utc = clock_timestamp()
            WHERE supplier_tenant_id = $2 AND id = $3
            """, connection, transaction))
        {
            publish.Parameters.AddWithValue(ResearchListingVersionId);
            publish.Parameters.AddWithValue(TenantId);
            publish.Parameters.AddWithValue(ResearchListingId);
            Assert.Equal(1, await publish.ExecuteNonQueryAsync());
        }
        await transaction.CommitAsync();
    }

    private static async Task AssertResearchVersioningAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT product.supplier_product_code, product.current_version_id,
                current.version_number, current.audience_profile_json->>'measurementSource' AS source,
                previous.audience_profile_json->>'measurementSource' AS previous_source,
                (SELECT count(*) FROM commercial.inventory_rates rate
                    WHERE rate.tenant_id = product.tenant_id AND rate.product_version_id = current.id) AS rate_count,
                (SELECT count(*) FROM commercial.inventory_availability availability
                    WHERE availability.tenant_id = product.tenant_id AND availability.product_version_id = current.id) AS availability_count
            FROM commercial.inventory_products product
            JOIN commercial.inventory_product_versions current
              ON current.tenant_id = product.tenant_id AND current.id = product.current_version_id
            JOIN commercial.inventory_product_versions previous
              ON previous.tenant_id = product.tenant_id AND previous.product_id = product.id
             AND previous.version_number = 1
            WHERE product.tenant_id = $1 AND product.supplier_product_code IN ('PLANNING-1', 'PLANNING-2')
            ORDER BY product.supplier_product_code
            """, connection);
        command.Parameters.AddWithValue(TenantId);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = 0;
        while (await reader.ReadAsync())
        {
            rows++;
            Assert.Equal(2, reader.GetInt32(2));
            Assert.Equal("Authorised Research", reader.GetString(3));
            Assert.Equal(1, reader.GetInt64(5));
            Assert.Equal(1, reader.GetInt64(6));
            if (reader.GetString(0) == "PLANNING-1")
                Assert.Equal("Fixture audience study", reader.GetString(4));
            else
                Assert.True(reader.IsDBNull(4));
        }
        Assert.Equal(2, rows);
        await reader.DisposeAsync();

        await using var marketplace = new NpgsqlCommand("""
            SELECT listing.version, snapshot.version_number,
                snapshot.product_version_id = product.current_version_id AS current_product_version,
                snapshot.audience_profile_json->>'measurementSource' AS source
            FROM commercial.marketplace_listings listing
            JOIN commercial.marketplace_listing_versions snapshot
              ON snapshot.supplier_tenant_id = listing.supplier_tenant_id
             AND snapshot.id = listing.current_version_id
            JOIN commercial.inventory_products product
              ON product.tenant_id = listing.supplier_tenant_id AND product.id = listing.product_id
            WHERE listing.supplier_tenant_id = $1 AND listing.id = $2
            """, connection);
        marketplace.Parameters.AddWithValue(TenantId);
        marketplace.Parameters.AddWithValue(ResearchListingId);
        await using var marketplaceReader = await marketplace.ExecuteReaderAsync();
        Assert.True(await marketplaceReader.ReadAsync());
        Assert.Equal(3, marketplaceReader.GetInt64(0));
        Assert.Equal(2, marketplaceReader.GetInt32(1));
        Assert.True(marketplaceReader.GetBoolean(2));
        Assert.Equal("Authorised Research", marketplaceReader.GetString(3));
    }
}

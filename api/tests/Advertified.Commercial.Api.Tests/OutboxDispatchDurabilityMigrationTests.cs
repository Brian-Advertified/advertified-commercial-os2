using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class OutboxDispatchDurabilityMigrationTests
{
    private const string DatabaseName = "advertified_outbox_migration";
    private const string DatabaseUser = "advertified_outbox_migration";
    private const string DatabasePassword = "advertified-outbox-migration-local-only";
    private static readonly Guid TenantId =
        Guid.Parse("cb100000-0000-4000-8000-000000000001");
    private static readonly Guid UnpublishedEventId =
        Guid.Parse("cb100000-0000-4000-8000-000000000002");
    private static readonly Guid PublishedEventId =
        Guid.Parse("cb100000-0000-4000-8000-000000000003");

    [Fact]
    [Trait("Category", "Migration")]
    public async Task FreshSchemaSupportsDurableDispatchAndRepeatedInstallation()
    {
        await using var postgres = DisposablePostgres.Create(
            DatabaseName, DatabaseUser, DatabasePassword);
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DisposablePostgres.EnableRequiredExtensionsAsync(connectionString);
        await DisposableDatabaseRoles.ProvisionAsync(connectionString);

        await MigrateAsync(connectionString);
        Assert.True(await ColumnExistsAsync(connectionString, "claim_token"));

        await BootstrapAndSeedLegacyRowsAsync(connectionString);
        await MigrateAsync(connectionString);
        await AssertLegacyRowsAsync(connectionString);
        await ClaimLegacyEventAsync(connectionString);
        await MigrateAsync(connectionString);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT claim_token IS NOT NULL FROM commercial.outbox_messages WHERE id = $1",
            connection);
        command.Parameters.AddWithValue(UnpublishedEventId);
        Assert.Equal(true, await command.ExecuteScalarAsync());
    }

    private static async Task MigrateAsync(
        string connectionString)
    {
        var options = new DbContextOptionsBuilder<GovernanceDbContext>()
            .UseNpgsql(connectionString).Options;
        await using var database = new GovernanceDbContext(options);
        await database.Database.MigrateAsync();
    }

    private static async Task BootstrapAndSeedLegacyRowsAsync(string connectionString)
    {
        var options = new DbContextOptionsBuilder<GovernanceDbContext>()
            .UseNpgsql(connectionString).Options;
        await using var database = new GovernanceDbContext(options);
        await new MasterDataBootstrapper(database, new FixedTimeProvider()).ApplyAsync();
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.tenants (
                id, type_code, legal_name, trading_name, slug, status_code,
                timezone, currency_code, vat_status_code, settings_json,
                version, created_at_utc, updated_at_utc)
            VALUES ({TenantId}, 'AGENCY', 'Legacy Outbox Agency',
                'Legacy Outbox Agency', 'legacy-outbox-agency', 'ACTIVE',
                'Africa/Johannesburg', 'ZAR', 'REGISTERED', jsonb_build_object(),
                1, '2026-08-31T09:00:00Z', '2026-08-31T09:00:00Z');
            INSERT INTO commercial.outbox_messages (
                id, tenant_id, causation_id, correlation_id, event_type_code,
                aggregate_type_code, aggregate_id, aggregate_version,
                payload_json, occurred_at_utc, published_at_utc, attempts)
            VALUES
                ({UnpublishedEventId}, {TenantId}, gen_random_uuid(),
                    gen_random_uuid(), 'LegacyEvent', 'tenant', {TenantId}, 1,
                    jsonb_build_object('kind', 'unpublished'),
                    '2026-08-31T10:00:00Z', NULL, 0),
                ({PublishedEventId}, {TenantId}, gen_random_uuid(),
                    gen_random_uuid(), 'LegacyEvent', 'tenant', {TenantId}, 1,
                    jsonb_build_object('kind', 'published'),
                    '2026-08-31T10:01:00Z', '2026-08-31T10:02:00Z', 2);
            """);
    }

    private static async Task AssertLegacyRowsAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            $"""
            SELECT id, published_at_utc, attempts, claim_token,
                transport_reference, dead_lettered_at_utc
            FROM commercial.outbox_messages
            WHERE id IN ('{UnpublishedEventId}', '{PublishedEventId}')
            ORDER BY id
            """,
            connection);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<(Guid Id, DateTimeOffset? Published, int Attempts)>();
        while (await reader.ReadAsync())
        {
            Assert.True(reader.IsDBNull(3));
            Assert.True(reader.IsDBNull(4));
            Assert.True(reader.IsDBNull(5));
            rows.Add((
                reader.GetGuid(0),
                reader.IsDBNull(1) ? null : reader.GetFieldValue<DateTimeOffset>(1),
                reader.GetInt32(2)));
        }

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, row => row.Id == UnpublishedEventId &&
            row.Published is null && row.Attempts == 0);
        Assert.Contains(rows, row => row.Id == PublishedEventId &&
            row.Published == new DateTimeOffset(2026, 8, 31, 10, 2, 0, TimeSpan.Zero) &&
            row.Attempts == 2);
    }

    private static async Task ClaimLegacyEventAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var setTenant = new NpgsqlCommand(
            "SELECT set_config('advertified.tenant_id', $1, true)",
            connection,
            transaction))
        {
            setTenant.Parameters.AddWithValue(TenantId.ToString());
            await setTenant.ExecuteNonQueryAsync();
        }

        await using var command = new NpgsqlCommand(
            """
            SELECT event_id
            FROM commercial.claim_next_outbox_event(
                gen_random_uuid(),
                60)
            """,
            connection,
            transaction);
        Assert.Equal(UnpublishedEventId, await command.ExecuteScalarAsync());
        await transaction.CommitAsync();
    }

    private static async Task<bool> ColumnExistsAsync(
        string connectionString,
        string columnName)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_schema = 'commercial'
                  AND table_name = 'outbox_messages'
                  AND column_name = $1)
            """,
            connection);
        command.Parameters.AddWithValue(columnName);
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);
    }
}

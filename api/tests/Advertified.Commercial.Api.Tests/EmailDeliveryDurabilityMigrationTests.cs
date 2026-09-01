using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class EmailDeliveryDurabilityMigrationTests
{
    private const string DatabaseName = "advertified_email_delivery_migration";
    private const string DatabaseUser = "advertified_email_delivery_migration";
    private const string DatabasePassword = "advertified-email-delivery-migration-local-only";
    private const string PreviousMigration = nameof(SupplierDeliveryProofRequests);
    private static readonly Guid TenantId = Guid.Parse("ba1fed00-0000-4000-8000-000000000001");
    private static readonly Guid UserId = Guid.Parse("ba1fed00-0000-4000-8000-000000000002");
    private static readonly Guid MailboxId = Guid.Parse("ba1fed00-0000-4000-8000-000000000003");
    private static readonly Guid EmailId = Guid.Parse("ba1fed00-0000-4000-8000-000000000004");
    private static readonly Guid RunId = Guid.Parse("ba1fed00-0000-4000-8000-000000000005");

    [Theory]
    [InlineData(InvalidDeliveryMutation.IncompleteIntent, "23514")]
    [InlineData(InvalidDeliveryMutation.AcceptanceWithoutRequest, "23514")]
    [InlineData(InvalidDeliveryMutation.RewriteIdempotencyKey, "P0001")]
    [InlineData(InvalidDeliveryMutation.RollbackWithEvidence, "P0001")]
    [Trait("Category", "Migration")]
    public async Task UpgradePreservesLegacyEvidenceAndRejectsUnsafeChanges(
        InvalidDeliveryMutation mutation,
        string expectedSqlState)
    {
        await using var postgres = DisposablePostgres.Create(
            DatabaseName, DatabaseUser, DatabasePassword);
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DisposablePostgres.EnableRequiredExtensionsAsync(connectionString);
        await PrepareMigrationRoleAsync(connectionString);
        await MigrateAsync(connectionString, PreviousMigration, bootstrapRegistry: true);
        await SeedLegacyRunAsync(connectionString);

        await MigrateAsync(connectionString, targetMigration: null, bootstrapRegistry: false);
        await AssertLegacyEvidencePreservedAsync(connectionString);
        if (mutation is InvalidDeliveryMutation.RewriteIdempotencyKey or
            InvalidDeliveryMutation.RollbackWithEvidence)
        {
            await EstablishValidDeliveryEvidenceAsync(connectionString);
        }

        Func<Task> rejectedOperation = mutation == InvalidDeliveryMutation.RollbackWithEvidence
            ? () => MigrateAsync(connectionString, PreviousMigration, bootstrapRegistry: false)
            : () => ExecuteInvalidMutationAsync(connectionString, mutation);
        var exception = await Assert.ThrowsAsync<PostgresException>(rejectedOperation);
        Assert.Equal(expectedSqlState, exception.SqlState);
    }

    private static async Task MigrateAsync(
        string connectionString,
        string? targetMigration,
        bool bootstrapRegistry)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using (var setRole = new NpgsqlCommand(
            "SET ROLE advertified_migrator", connection))
        {
            await setRole.ExecuteNonQueryAsync();
        }
        var options = new DbContextOptionsBuilder<GovernanceDbContext>()
            .UseNpgsql(connection)
            .Options;
        await using var dbContext = new GovernanceDbContext(options);
        if (targetMigration is null)
        {
            await dbContext.Database.MigrateAsync();
        }
        else
        {
            var targetId = dbContext.Database.GetMigrations()
                .Single(id => id.EndsWith($"_{targetMigration}", StringComparison.Ordinal));
            var targetName = dbContext.GetService<IMigrationsIdGenerator>()
                .GetName(targetId);
            await dbContext.GetService<IMigrator>().MigrateAsync(targetName);
        }
        if (bootstrapRegistry)
        {
            await new MasterDataBootstrapper(dbContext, new FixedTimeProvider())
                .ApplyAsync();
        }
    }

    private static async Task PrepareMigrationRoleAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            $"""
            CREATE ROLE advertified_migrator
                NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOBYPASSRLS;
            CREATE ROLE advertified_app
                NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOBYPASSRLS;
            GRANT advertified_migrator TO {DatabaseUser};
            GRANT CREATE ON DATABASE {DatabaseName} TO advertified_migrator;
            GRANT CREATE ON SCHEMA public TO advertified_migrator;
            """,
            connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task SeedLegacyRunAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            $"""
            INSERT INTO commercial.tenants (
                id, type_code, legal_name, trading_name, slug, status_code,
                timezone, currency_code, vat_status_code, settings_json,
                version, created_at_utc, updated_at_utc)
            VALUES ('{TenantId}', 'AGENCY', 'Legacy Agency', 'Legacy Agency',
                'legacy-agency', 'ACTIVE', 'Africa/Johannesburg', 'ZAR',
                'REGISTERED', jsonb_build_object(), 1, '2026-08-30T10:00:00Z',
                '2026-08-30T10:00:00Z');
            INSERT INTO commercial.users (
                id, email, display_name, status_code, mfa_enabled, version,
                created_at_utc, updated_at_utc)
            VALUES ('{UserId}', 'legacy@example.test', 'Legacy Owner', 'ACTIVE',
                false, 1, '2026-08-30T10:00:00Z', '2026-08-30T10:00:00Z');
            INSERT INTO commercial.inbound_mailboxes (
                id, tenant_id, address, provider_code, owner_user_id,
                auto_send_enabled, created_at_utc, updated_at_utc)
            VALUES ('{MailboxId}', '{TenantId}', 'briefs@example.test',
                'DETERMINISTIC', '{UserId}', true, '2026-08-30T10:00:00Z',
                '2026-08-30T10:00:00Z');
            INSERT INTO commercial.inbound_campaign_emails (
                id, tenant_id, mailbox_id, provider_event_id, provider_email_id,
                provider_message_id, sender_email, reply_to_email, subject,
                body_text, source_hash, raw_metadata_json, received_at_utc,
                created_at_utc)
            VALUES ('{EmailId}', '{TenantId}', '{MailboxId}', 'legacy-event',
                'legacy-email', 'legacy-message', 'client@example.test',
                'client@example.test', 'Legacy brief', 'Legacy body',
                repeat('a', 64), jsonb_build_object(), '2026-08-30T10:00:00Z',
                '2026-08-30T10:00:00Z');
            INSERT INTO commercial.email_proposal_automation_runs (
                id, tenant_id, inbound_email_id, policy_version,
                campaign_mode_code, status_code, checkpoint_code, input_hash,
                delivery_idempotency_key, delivery_provider_id,
                created_at_utc, updated_at_utc)
            VALUES ('{RunId}', '{TenantId}', '{EmailId}',
                'OOH_INBOUND_PROPOSAL_V1', 'OOH_ONLY', 'SENT', 'SENT',
                repeat('b', 64), 'legacy-delivery-key', 'legacy-provider-receipt',
                '2026-08-30T10:00:00Z', '2026-08-30T10:00:00Z');
            """,
            connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task AssertLegacyEvidencePreservedAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            $"""
            SELECT delivery_idempotency_key, delivery_provider_id,
                delivery_provider_code, delivery_requested_at_utc,
                delivery_accepted_at_utc
            FROM commercial.email_proposal_automation_runs
            WHERE id = '{RunId}'
            """,
            connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("legacy-delivery-key", reader.GetString(0));
        Assert.Equal("legacy-provider-receipt", reader.GetString(1));
        Assert.True(reader.IsDBNull(2));
        Assert.True(reader.IsDBNull(3));
        Assert.True(reader.IsDBNull(4));
    }

    private static Task EstablishValidDeliveryEvidenceAsync(string connectionString) =>
        ExecuteAsync(connectionString, $"""
            UPDATE commercial.email_proposal_automation_runs
            SET delivery_provider_collection_code = 'emailProviders',
                delivery_provider_code = 'DETERMINISTIC',
                delivery_requested_at_utc = '2026-08-31T10:00:00Z',
                delivery_accepted_at_utc = '2026-08-31T10:01:00Z'
            WHERE id = '{RunId}'
            """);

    private static Task ExecuteInvalidMutationAsync(
        string connectionString,
        InvalidDeliveryMutation mutation) => ExecuteAsync(
            connectionString,
            mutation switch
            {
                InvalidDeliveryMutation.IncompleteIntent => $"""
                    UPDATE commercial.email_proposal_automation_runs
                    SET checkpoint_code = 'DELIVERY_REQUESTED',
                        delivery_requested_at_utc = '2026-08-31T10:00:00Z'
                    WHERE id = '{RunId}'
                    """,
                InvalidDeliveryMutation.AcceptanceWithoutRequest => $"""
                    UPDATE commercial.email_proposal_automation_runs
                    SET checkpoint_code = 'DELIVERY_ACCEPTED',
                        delivery_accepted_at_utc = '2026-08-31T10:01:00Z'
                    WHERE id = '{RunId}'
                    """,
                InvalidDeliveryMutation.RewriteIdempotencyKey => $"""
                    UPDATE commercial.email_proposal_automation_runs
                    SET delivery_idempotency_key = 'rewritten-delivery-key'
                    WHERE id = '{RunId}'
                    """,
                _ => throw new ArgumentOutOfRangeException(nameof(mutation)),
            });

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    public enum InvalidDeliveryMutation
    {
        IncompleteIntent,
        AcceptanceWithoutRequest,
        RewriteIdempotencyKey,
        RollbackWithEvidence,
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);
    }
}

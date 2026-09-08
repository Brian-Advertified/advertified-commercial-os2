using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class ProposalBrandingMigrationTests
{
    [Fact]
    [Trait("Category", "Migration")]
    public async Task PopulatedUpgradeBackfillsNamesRestoresRlsAndRejectsMissingApprovalReason()
    {
        await using var postgres = DisposablePostgres.Create(
            "advertified_brief_test_branding_upgrade", "branding_upgrade",
            "branding-upgrade-local-only");
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DisposablePostgres.EnableRequiredExtensionsAsync(connectionString);
        await DisposableDatabaseRoles.ProvisionAsync(connectionString);
        await PrepareMigrationOwnerAsync(connectionString);
        await MigrateAsOwnerAsync(connectionString, "202609080008_AudienceStrategyApproval");
        await CanonicalPlanningAcceptanceTests.SeedAsync(connectionString, initializeSchema: false);
        await InsertLegacyProposalAsync(connectionString);

        await MigrateAsOwnerAsync(connectionString, "202609080009_ProposalBranding");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var names = new NpgsqlCommand("""
            SELECT agency_brand_name, client_brand_name, unbranded_approved_by
            FROM commercial.proposal_versions
            """, connection);
        await using (var reader = await names.ExecuteReaderAsync())
        {
            Assert.True(await reader.ReadAsync());
            Assert.Equal("canonical-planning", reader.GetString(0));
            Assert.Equal("Planning Client", reader.GetString(1));
            Assert.True(reader.IsDBNull(2));
            Assert.False(await reader.ReadAsync());
        }
        await AssertApprovalReasonRequiredAsync(connection);
        await AssertRlsRestoredAsync(connection);
    }

    private static async Task PrepareMigrationOwnerAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            DO $block$ BEGIN
                EXECUTE format('GRANT CREATE ON DATABASE %I TO advertified_migrator', current_database());
            END $block$;
            GRANT USAGE, CREATE ON SCHEMA public TO advertified_migrator;
            """, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task MigrateAsOwnerAsync(string connectionString, string target)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using (var role = new NpgsqlCommand("SET ROLE advertified_migrator", connection))
            await role.ExecuteNonQueryAsync();
        await using var db = new GovernanceDbContext(new DbContextOptionsBuilder<GovernanceDbContext>()
            .UseNpgsql(connection).Options);
        // Legacy repository IDs have twelve digits. Resolve through EF's own name
        // parser so targeted migration uses the same lookup as its migrations assembly.
        var targetName = db.GetService<IMigrationsIdGenerator>().GetName(target);
        await db.GetService<IMigrator>().MigrateAsync(targetName);
        await new MasterDataBootstrapper(db, TimeProvider.System).ApplyAsync();
    }

    private static async Task InsertLegacyProposalAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            INSERT INTO commercial.proposal_versions (
                id, tenant_id, brief_id, brief_version_id, version_no, title,
                executive_summary, terms, expiry_at_utc, status_code, input_hash,
                created_by, version, created_at_utc)
            SELECT gen_random_uuid(), tenant_id, brief_id, id, 1, 'Legacy proposal',
                'Synthetic migration fixture', 'Synthetic terms', now() + interval '1 day',
                'DRAFT', repeat('d', 64), created_by, 1, now()
            FROM commercial.brief_versions;
            """, connection);
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }

    private static async Task AssertApprovalReasonRequiredAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand("""
            UPDATE commercial.proposal_versions
            SET unbranded_approved_by = created_by, unbranded_approved_at_utc = now(),
                unbranded_approval_reason = NULL
            """, connection);
        var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal("ck_proposal_unbranded_approval_shape", error.ConstraintName);
    }

    private static async Task AssertRlsRestoredAsync(NpgsqlConnection connection)
    {
        await using var protection = new NpgsqlCommand("""
            SELECT count(*)::integer FROM pg_class relation
            JOIN pg_namespace schema ON schema.oid = relation.relnamespace
            WHERE schema.nspname = 'commercial' AND relation.relrowsecurity
              AND relation.relforcerowsecurity AND relation.relname IN (
                'proposal_versions', 'campaign_briefs', 'client_accounts', 'tenants')
            """, connection);
        Assert.Equal(4, (int)(await protection.ExecuteScalarAsync())!);
        await using (var role = new NpgsqlCommand("SET ROLE advertified_app", connection))
            await role.ExecuteNonQueryAsync();
        await using var visible = new NpgsqlCommand(
            "SELECT count(*)::integer FROM commercial.proposal_versions", connection);
        Assert.Equal(0, (int)(await visible.ExecuteScalarAsync())!);
    }
}

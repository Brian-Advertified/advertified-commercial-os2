using Advertified.Commercial.DatabaseMigrator;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class MasterDataMigrationTests
{
    private const string IntelligenceRepairId = "202609130015_IntelligenceArtifactProviderColumnDriftRepair";
    private static readonly Guid DriftFirstTenant = Guid.Parse("91000000-0000-0000-0000-000000000001");
    private static readonly Guid DriftSecondTenant = Guid.Parse("91000000-0000-0000-0000-000000000002");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Category", "Migration")]
    public async Task ForwardRepairRemovesLegacyIntelligenceArtifactProviderColumns(bool invocationTableExists)
    {
        await using var postgres = DisposablePostgres.Create(DatabaseName, DatabaseUser, DatabasePassword);
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DisposablePostgres.EnableRequiredExtensionsAsync(connectionString);
        await PrepareMigrationRoleAsync(connectionString);
        var operation = new DatabaseMigrationOperation(new FixedTimeProvider());
        Assert.Contains(IntelligenceRepairId, (await operation.ApplyAsync(connectionString)).AppliedMigrations);
        await SeedIntelligenceSchemaDriftAsync(connectionString, invocationTableExists);
        await using var verify = new NpgsqlConnection(connectionString);
        await verify.OpenAsync();
        var fingerprint = await IntelligenceScalarAsync<string>(verify, ArtifactFingerprintSql);
        var applied = await operation.ApplyAsync(connectionString);
        Assert.Equal([IntelligenceRepairId], applied.AppliedMigrations);
        Assert.Equal(fingerprint, await IntelligenceScalarAsync<string>(verify, ArtifactFingerprintSql));
        await AssertIntelligenceUsagePreservedAsync(verify);
        await AssertIntelligenceIsolationAsync(verify);
        Assert.Empty((await operation.ApplyAsync(connectionString)).AppliedMigrations);
        await AssertIntelligenceUsagePreservedAsync(verify);
    }

    private static async Task SeedIntelligenceSchemaDriftAsync(string connectionString, bool invocationTableExists)
    {
        await using var database = new GovernanceDbContext(new DbContextOptionsBuilder<GovernanceDbContext>()
            .UseNpgsql(connectionString).Options);
        foreach (var tenantId in new[] { DriftFirstTenant, DriftSecondTenant })
        {
            var slug = "intelligence-drift-" + tenantId.ToString("N");
            database.Tenants.Add(new Tenant(new TenantId(tenantId), new TenantTypeCode("AGENCY"),
                slug, slug, new Slug(slug), new LifecycleStatusCode("ACTIVE"), "Africa/Johannesburg",
                new CurrencyCode("ZAR"), new VatStatusCode("REGISTERED"), null, "{}", DateTimeOffset.UtcNow));
        }
        await database.SaveChangesAsync();
        await database.Database.ExecuteSqlRawAsync(LegacyIntelligenceSchemaSql);
        if (!invocationTableExists)
            await database.Database.ExecuteSqlRawAsync("DROP TABLE commercial.intelligence_artifact_invocations");
        await database.Database.ExecuteSqlRawAsync(LegacyIntelligenceRowsSql);
        if (invocationTableExists)
            await database.Database.ExecuteSqlRawAsync("""
                INSERT INTO commercial.intelligence_artifact_invocations (
                    tenant_id, artifact_id, sequence_no, operation_code, provider_code, model_code,
                    incremental_cost_minor, cache_status, provider_request_id)
                SELECT tenant_id, id, 1, service_code, agent_provider_code, agent_model_code,
                    agent_incremental_cost_minor, 'FIXTURE', agent_provider_request_id
                FROM commercial.intelligence_artifacts WHERE agent_provider_code = 'deterministic';
                """);
    }

    private static async Task AssertIntelligenceUsagePreservedAsync(NpgsqlConnection connection)
    {
        Assert.Equal(2, await IntelligenceScalarAsync<int>(connection,
            "SELECT count(*)::integer FROM commercial.intelligence_artifacts"));
        Assert.Equal(2, await IntelligenceScalarAsync<int>(connection,
            "SELECT count(*)::integer FROM commercial.intelligence_artifact_invocations"));
        Assert.True(await IntelligenceScalarAsync<bool>(connection, """
            SELECT EXISTS (SELECT 1 FROM commercial.intelligence_artifact_invocations
                WHERE provider_code = 'bedrock' AND model_code = 'historical-model'
                  AND incremental_cost_minor = 17 AND provider_request_id = 'retained-provider-request'
                  AND cache_status = 'LEGACY')
            """));
        Assert.Equal(0, await IntelligenceScalarAsync<int>(connection, """
            SELECT count(*)::integer FROM information_schema.columns
            WHERE table_schema = 'commercial' AND table_name = 'intelligence_artifacts'
              AND column_name = ANY(ARRAY['agent_provider_code', 'agent_model_code',
                  'agent_incremental_cost_minor', 'agent_provider_request_id'])
            """));
        Assert.Equal(2, await IntelligenceScalarAsync<int>(connection, """
            SELECT count(*)::integer FROM pg_constraint
            WHERE conrelid = 'commercial.intelligence_artifacts'::regclass
              AND conname IN ('ck_intelligence_artifact_numbers', 'ck_intelligence_artifact_text')
            """));
    }

    private static async Task AssertIntelligenceIsolationAsync(NpgsqlConnection connection)
    {
        Assert.True(await IntelligenceScalarAsync<bool>(connection, """
            SELECT bool_and(relrowsecurity AND relforcerowsecurity) FROM pg_class
            WHERE oid IN ('commercial.intelligence_artifacts'::regclass,
                'commercial.intelligence_artifact_invocations'::regclass)
            """));
        Assert.Equal(0, await IntelligenceScalarAsync<int>(connection,
            "SELECT count(*)::integer FROM pg_policy WHERE polname LIKE 'intelligence_provider_repair_%'"));
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var role = new NpgsqlCommand("SET LOCAL ROLE advertified_app", connection))
            await role.ExecuteNonQueryAsync();
        Assert.Equal(0, await IntelligenceScalarAsync<int>(connection,
            "SELECT count(*)::integer FROM commercial.intelligence_artifact_invocations"));
        await using (var scope = new NpgsqlCommand("SELECT set_config('advertified.tenant_id', $1, true)", connection))
        {
            scope.Parameters.AddWithValue(DriftFirstTenant.ToString());
            await scope.ExecuteNonQueryAsync();
        }
        Assert.Equal(1, await IntelligenceScalarAsync<int>(connection,
            "SELECT count(*)::integer FROM commercial.intelligence_artifact_invocations"));
        await transaction.RollbackAsync();
    }

    private static async Task<T> IntelligenceScalarAsync<T>(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Intelligence migration verification returned no value."));
    }

    private const string ArtifactFingerprintSql = """
        SELECT md5(string_agg(id::text || artifact_json::text || input_hash::text || status_code,
            ',' ORDER BY id)) FROM commercial.intelligence_artifacts
        """;

    // This runs only against the disposable fixture. Never alter the application migration ledger.
    private const string LegacyIntelligenceSchemaSql = """
        ALTER TABLE commercial.intelligence_artifacts
            ADD COLUMN agent_provider_code varchar(100) NOT NULL DEFAULT 'deterministic',
            ADD COLUMN agent_model_code varchar(300) NOT NULL DEFAULT 'fixture-v1',
            ADD COLUMN agent_incremental_cost_minor bigint NOT NULL DEFAULT 0,
            ADD COLUMN agent_provider_request_id varchar(500),
            DROP CONSTRAINT ck_intelligence_artifact_numbers,
            DROP CONSTRAINT ck_intelligence_artifact_text,
            ADD CONSTRAINT ck_intelligence_artifact_numbers CHECK (
                subject_version > 0 AND version_no > 0 AND version > 0 AND agent_incremental_cost_minor >= 0),
            ADD CONSTRAINT ck_intelligence_artifact_text CHECK (
                btrim(subject_type) <> '' AND btrim(service_code) <> '' AND
                btrim(artifact_schema_version) <> '' AND btrim(status_code) <> '' AND
                btrim(agent_provider_code) <> '' AND btrim(agent_model_code) <> '');
        DELETE FROM "__EFMigrationsHistory"
        WHERE "MigrationId" = '202609130015_IntelligenceArtifactProviderColumnDriftRepair';
        """;

    private const string LegacyIntelligenceRowsSql = """
        INSERT INTO commercial.intelligence_artifacts (
            id, tenant_id, subject_type, subject_id, subject_version, service_code,
            artifact_schema_version, version_no, artifact_json, input_hash, status_code,
            created_by, created_at_utc, version, agent_provider_code, agent_model_code,
            agent_incremental_cost_minor, agent_provider_request_id)
        SELECT gen_random_uuid(), tenant.id, 'BriefVersion', gen_random_uuid(), 1,
            'audience_intelligence', 'audience-strategy.v1', 1, jsonb_build_object('retained', 'evidence'),
            repeat('a', 64), 'DRAFT', gen_random_uuid(), CURRENT_TIMESTAMP, 1,
            CASE WHEN tenant.id = '91000000-0000-0000-0000-000000000001' THEN 'deterministic' ELSE 'bedrock' END,
            CASE WHEN tenant.id = '91000000-0000-0000-0000-000000000001' THEN 'fixture-v1' ELSE 'historical-model' END,
            CASE WHEN tenant.id = '91000000-0000-0000-0000-000000000001' THEN 0 ELSE 17 END,
            CASE WHEN tenant.id = '91000000-0000-0000-0000-000000000001' THEN NULL ELSE 'retained-provider-request' END
        FROM commercial.tenants tenant;
        """;
}

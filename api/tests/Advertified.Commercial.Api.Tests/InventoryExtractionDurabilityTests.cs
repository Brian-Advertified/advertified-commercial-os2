using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Worker;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class InventoryExtractionDurabilityTests
{
    private static readonly Guid TenantId = Guid.Parse("de100000-0000-0000-0000-000000000001");
    private static readonly Guid UserId = Guid.Parse("de200000-0000-0000-0000-000000000001");
    private static readonly Guid ImportId = Guid.Parse("de300000-0000-0000-0000-000000000001");
    private static readonly Guid FirstAttemptId =
        Guid.Parse("de400000-0000-0000-0000-000000000001");
    private static readonly Guid SecondAttemptId =
        Guid.Parse("de400000-0000-0000-0000-000000000002");
    private static readonly Guid QueuedImportId =
        Guid.Parse("de300000-0000-0000-0000-000000000002");
    private static readonly Guid QueuedAttemptId =
        Guid.Parse("de400000-0000-0000-0000-000000000003");
    private static readonly DateTimeOffset Now =
        new(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);
    private static readonly string SourceHash = new('a', 64);

    [Fact]
    [Trait("Category", "Migration")]
    public async Task RestartResumesTaskAndTerminalHistoryPreventsDuplicateAcceptance()
    {
        await using var postgres = DisposablePostgres.Create(
            "advertified_extraction_durability", "advertified_extraction_durability",
            "advertified-extraction-durability-local-only");
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DisposablePostgres.EnableRequiredExtensionsAsync(connectionString);
        await DisposableDatabaseRoles.ProvisionAsync(connectionString);
        await SeedAsync(connectionString);
        var scheduler = new WorkerSchedulerStore(connectionString);

        var firstClaim = await scheduler.ClaimInventoryExtractionAsync(
            Guid.NewGuid(), 30, 1, CancellationToken.None);
        Assert.NotNull(firstClaim);
        Assert.Equal(FirstAttemptId, firstClaim.AttemptId);
        Assert.Equal("docling-task-original", firstClaim.ExternalTaskId);

        await ExpireLeaseAsync(connectionString, FirstAttemptId);
        var restartClaim = await scheduler.ClaimInventoryExtractionAsync(
            Guid.NewGuid(), 30, 1, CancellationToken.None);
        Assert.NotNull(restartClaim);
        Assert.Equal(firstClaim.AttemptId, restartClaim.AttemptId);
        Assert.Equal(firstClaim.ExternalTaskId, restartClaim.ExternalTaskId);

        await MarkTimedOutAsync(connectionString, restartClaim.ClaimToken);
        Assert.Null(await scheduler.ClaimInventoryExtractionAsync(
            Guid.NewGuid(), 30, 1, CancellationToken.None));

        await InsertExplicitRetryAsync(connectionString);
        var retryClaim = await scheduler.ClaimInventoryExtractionAsync(
            Guid.NewGuid(), 30, 1, CancellationToken.None);
        Assert.NotNull(retryClaim);
        Assert.Equal(SecondAttemptId, retryClaim.AttemptId);
        await AssertLateOldArtifactHasNoEffectsAsync(connectionString);
        await CompleteRetryAsync(connectionString, retryClaim.ClaimToken);

        await AssertHistoryAndArtifactAsync(connectionString);
        await AssertDuplicateArtifactRejectedAsync(connectionString);
        await AssertLateOldResultRejectedAsync(connectionString);
    }

    [Fact]
    [Trait("Category", "Migration")]
    public async Task PendingSubmissionRespectsConfiguredProviderConcurrency()
    {
        await using var postgres = DisposablePostgres.Create(
            "advertified_extraction_bounds", "advertified_extraction_bounds",
            "advertified-extraction-bounds-local-only");
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DisposablePostgres.EnableRequiredExtensionsAsync(connectionString);
        await DisposableDatabaseRoles.ProvisionAsync(connectionString);
        await SeedAsync(connectionString);
        await InsertQueuedImportAsync(connectionString);
        var scheduler = new WorkerSchedulerStore(connectionString);

        var active = await scheduler.ClaimInventoryExtractionAsync(
            Guid.NewGuid(), 30, 1, CancellationToken.None);
        Assert.NotNull(active);
        Assert.Equal(FirstAttemptId, active.AttemptId);
        Assert.Null(await scheduler.ClaimInventoryExtractionAsync(
            Guid.NewGuid(), 30, 1, CancellationToken.None));

        var secondLane = await scheduler.ClaimInventoryExtractionAsync(
            Guid.NewGuid(), 30, 2, CancellationToken.None);
        Assert.NotNull(secondLane);
        Assert.Equal(QueuedAttemptId, secondLane.AttemptId);
    }

    [Fact]
    [Trait("Category", "Migration")]
    public async Task CandidatePagingIndexExcludesUnboundedEvidencePayloads()
    {
        await using var postgres = DisposablePostgres.Create(
            "advertified_candidate_index", "advertified_candidate_index",
            "advertified-candidate-index-local-only");
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DisposablePostgres.EnableRequiredExtensionsAsync(connectionString);
        await DisposableDatabaseRoles.ProvisionAsync(connectionString);
        var options = new DbContextOptionsBuilder<GovernanceDbContext>()
            .UseNpgsql(connectionString).Options;
        await using var db = new GovernanceDbContext(options);
        await db.Database.MigrateAsync();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT indexdef FROM pg_indexes
            WHERE schemaname = 'commercial'
              AND indexname = 'ix_inventory_candidates_import_page'
            """, connection);

        var definition = Assert.IsType<string>(await command.ExecuteScalarAsync());

        Assert.Contains("INCLUDE (status_code, reviewed_by, version, updated_at_utc)",
            definition, StringComparison.Ordinal);
        Assert.DoesNotContain("canonical_values_json", definition, StringComparison.Ordinal);
        Assert.DoesNotContain("validation_json", definition, StringComparison.Ordinal);
        Assert.DoesNotContain("source_locator", definition, StringComparison.Ordinal);
    }

    private static async Task SeedAsync(string connectionString)
    {
        var options = new DbContextOptionsBuilder<GovernanceDbContext>()
            .UseNpgsql(connectionString).Options;
        await using var db = new GovernanceDbContext(options);
        await db.Database.MigrateAsync();
        await new MasterDataBootstrapper(db, TimeProvider.System).ApplyAsync();
        await SetProductionOwnersAsync(connectionString);
        db.Tenants.Add(new Tenant(
            new TenantId(TenantId), new TenantTypeCode("AGENCY"), "Durability Agency",
            "Durability Agency", new Slug("durability-agency"),
            new LifecycleStatusCode("ACTIVE"), "Africa/Johannesburg",
            new CurrencyCode("ZAR"), new VatStatusCode("REGISTERED"), null, "{}", Now));
        db.Users.Add(new User(
            new UserId(UserId), new EmailAddress("durability@example.test"),
            "Durability Operator", null, new LifecycleStatusCode("ACTIVE"), true, Now));
        await db.SaveChangesAsync();
        await ExecuteAsync(connectionString, SeedSql,
            ("tenant", TenantId), ("user", UserId), ("import", ImportId),
            ("attempt", FirstAttemptId), ("hash", SourceHash), ("now", Now));
    }

    private static async Task ExpireLeaseAsync(string connectionString, Guid attemptId) =>
        await ExecuteAsync(connectionString, """
            UPDATE commercial.inventory_extraction_attempts
            SET worker_lease_expires_at_utc = statement_timestamp() - interval '1 second'
            WHERE id = @attempt
            """, ("attempt", attemptId));

    private static Task SetProductionOwnersAsync(string connectionString) =>
        ExecuteAsync(connectionString, """
            ALTER TABLE commercial.inventory_extraction_attempts
                OWNER TO advertified_migrator;
            ALTER FUNCTION commercial.claim_next_inventory_extraction_attempt(uuid, integer, integer)
                OWNER TO advertified_migrator;
            ALTER FUNCTION commercial.heartbeat_inventory_extraction_attempt(uuid, integer)
                OWNER TO advertified_migrator;
            GRANT USAGE ON SCHEMA commercial
                TO advertified_migrator, advertified_worker;
            GRANT EXECUTE ON FUNCTION
                commercial.claim_next_inventory_extraction_attempt(uuid, integer, integer),
                commercial.heartbeat_inventory_extraction_attempt(uuid, integer)
                TO advertified_worker;
            """);

    private static async Task MarkTimedOutAsync(string connectionString, Guid claimToken) =>
        await ExecuteAsync(connectionString, """
            UPDATE commercial.inventory_extraction_attempts
            SET status_code = 'TIMED_OUT', completed_at_utc = statement_timestamp(),
                provider_response_code = 'TASK_TIMEOUT',
                provider_error_code = 'MAXIMUM_DURATION_EXCEEDED',
                failure_class_collection_code = 'inventoryExtractionFailureClasses',
                failure_class_code = 'TIMEOUT', worker_id = NULL,
                worker_lease_token = NULL, worker_lease_expires_at_utc = NULL
            WHERE worker_lease_token = @claim
            """, ("claim", claimToken));

    private static async Task InsertExplicitRetryAsync(string connectionString) =>
        await ExecuteAsync(connectionString, RetrySql,
            ("tenant", TenantId), ("import", ImportId), ("attempt", SecondAttemptId),
            ("hash", SourceHash), ("user", UserId), ("now", Now.AddMinutes(1)));

    private static Task InsertQueuedImportAsync(string connectionString) =>
        ExecuteAsync(connectionString, QueuedSql,
            ("tenant", TenantId), ("user", UserId), ("import", QueuedImportId),
            ("attempt", QueuedAttemptId), ("hash", new string('b', 64)),
            ("now", Now.AddMinutes(1)));

    private static async Task CompleteRetryAsync(string connectionString, Guid claimToken)
    {
        var artifactId = Guid.Parse("de500000-0000-0000-0000-000000000001");
        await ExecuteAsync(connectionString, """
            UPDATE commercial.inventory_extraction_attempts
            SET status_code = 'SUBMITTING', submitted_at_utc = @now
            WHERE worker_lease_token = @claim;
            UPDATE commercial.inventory_extraction_attempts
            SET status_code = 'RUNNING', external_task_id = 'docling-task-retry'
            WHERE worker_lease_token = @claim;
            """, ("claim", claimToken), ("now", Now.AddMinutes(1)));
        await InsertArtifactAsync(connectionString, artifactId, SecondAttemptId);
        await ExecuteAsync(connectionString, """
            UPDATE commercial.inventory_extraction_attempts
            SET status_code = 'COMPLETED', completed_at_utc = @now,
                extracted_artifact_id = @artifact, worker_id = NULL,
                worker_lease_token = NULL, worker_lease_expires_at_utc = NULL
            WHERE worker_lease_token = @claim
            """, ("claim", claimToken), ("artifact", artifactId),
            ("now", Now.AddMinutes(2)));
    }

    private static Task InsertArtifactAsync(
        string connectionString,
        Guid artifactId,
        Guid attemptId) =>
        ExecuteAsync(connectionString, """
            INSERT INTO commercial.inventory_extractions (
                id, tenant_id, import_id, source_hash, adapter_code, adapter_version,
                schema_version, provider_json, provider_output_hash, canonical_json,
                canonical_output_hash, completed_at_utc, attempt_id, source_file_version)
            VALUES (@artifact, @tenant, @import, @hash, 'docling', '1.30.0', '1',
                '{}'::jsonb, repeat('b', 64), '{}'::jsonb, repeat('c', 64), @now,
                @attempt, 1)
            """, ("artifact", artifactId), ("tenant", TenantId), ("import", ImportId),
            ("hash", SourceHash), ("now", Now.AddMinutes(2)), ("attempt", attemptId));

    private static async Task AssertLateOldArtifactHasNoEffectsAsync(string connectionString)
    {
        await Assert.ThrowsAsync<PostgresException>(() => InsertArtifactAsync(
            connectionString,
            Guid.Parse("de500000-0000-0000-0000-000000000009"),
            FirstAttemptId));
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT
                (SELECT count(*) FROM commercial.inventory_extractions
                    WHERE import_id = @import),
                (SELECT count(*) FROM commercial.inventory_candidates
                    WHERE import_id = @import),
                (SELECT status_code FROM commercial.inventory_imports
                    WHERE id = @import),
                (SELECT count(*) FROM commercial.inventory_products),
                (SELECT count(extracted_artifact_id)
                    FROM commercial.inventory_extraction_attempts
                    WHERE import_id = @import)
            """, connection);
        command.Parameters.AddWithValue("import", ImportId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(0L, reader.GetInt64(0));
        Assert.Equal(0L, reader.GetInt64(1));
        Assert.Equal("EXTRACTING", reader.GetString(2));
        Assert.Equal(0L, reader.GetInt64(3));
        Assert.Equal(0L, reader.GetInt64(4));
    }

    private static async Task AssertHistoryAndArtifactAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT string_agg(status_code, ',' ORDER BY attempt_number),
                count(extracted_artifact_id)
            FROM commercial.inventory_extraction_attempts WHERE import_id = @import
            """, connection);
        command.Parameters.AddWithValue("import", ImportId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("TIMED_OUT,COMPLETED", reader.GetString(0));
        Assert.Equal(1L, reader.GetInt64(1));
    }

    private static async Task AssertDuplicateArtifactRejectedAsync(string connectionString)
    {
        await Assert.ThrowsAsync<PostgresException>(() => InsertArtifactAsync(
            connectionString,
            Guid.Parse("de500000-0000-0000-0000-000000000002"),
            SecondAttemptId));
    }

    private static async Task AssertLateOldResultRejectedAsync(string connectionString)
    {
        await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(connectionString, """
            UPDATE commercial.inventory_extraction_attempts
            SET status_code = 'COMPLETED', extracted_artifact_id =
                'de500000-0000-0000-0000-000000000001'
            WHERE id = @attempt
            """, ("attempt", FirstAttemptId)));
    }

    private static async Task ExecuteAsync(
        string connectionString, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var parameter in parameters)
            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        await command.ExecuteNonQueryAsync();
    }

    private const string SeedSql = """
        INSERT INTO commercial.inventory_suppliers (
            id, tenant_id, name, version, created_at_utc, updated_at_utc)
        VALUES ('de600000-0000-0000-0000-000000000001', @tenant,
            'Durability Supplier', 1, @now, @now);
        INSERT INTO commercial.inventory_imports (
            id, tenant_id, supplier_id, source_file_name, declared_media_type,
            document_class_collection_code, document_class_code, status_code,
            scan_status_code, quarantine_object_key, protected_object_key,
            source_hash, source_size, created_by, version, created_at_utc, updated_at_utc)
        VALUES (@import, @tenant, 'de600000-0000-0000-0000-000000000001',
            'durable.pdf', 'application/pdf', 'documentClasses', 'PDF', 'EXTRACTING',
            'CLEAN', 'quarantine/durable.pdf', 'protected/durable.pdf', @hash, 10,
            @user, 2, @now, @now);
        INSERT INTO commercial.inventory_extraction_attempts (
            id, tenant_id, import_id, source_file_version, source_hash,
            stable_submission_key, provider_name, provider_version, status_code,
            external_task_id, submitted_at_utc, polling_checkpoint, attempt_number,
            correlation_id, command_id, requested_by, version, created_at_utc, updated_at_utc)
        VALUES (@attempt, @tenant, @import, 1, @hash, 'submission-original',
            'docling', '1.30.0', 'RUNNING', 'docling-task-original', @now, '{}'::jsonb,
            1, gen_random_uuid(), gen_random_uuid(), @user, 1, @now, @now);
        """;

    private const string RetrySql = """
        INSERT INTO commercial.inventory_extraction_attempts (
            id, tenant_id, import_id, source_file_version, source_hash,
            stable_submission_key, provider_name, provider_version, status_code,
            polling_checkpoint, attempt_number, correlation_id, command_id,
            requested_by, reconciliation_notes, version, created_at_utc, updated_at_utc)
        VALUES (@attempt, @tenant, @import, 1, @hash, 'submission-explicit-retry',
            'docling', '1.30.0', 'PENDING', '{}'::jsonb, 2, gen_random_uuid(),
            gen_random_uuid(), @user, 'operator approved retry', 1, @now, @now);
        """;

    private const string QueuedSql = """
        INSERT INTO commercial.inventory_imports (
            id, tenant_id, supplier_id, source_file_name, declared_media_type,
            document_class_collection_code, document_class_code, status_code,
            scan_status_code, quarantine_object_key, protected_object_key,
            source_hash, source_size, created_by, version, created_at_utc, updated_at_utc)
        VALUES (@import, @tenant, 'de600000-0000-0000-0000-000000000001',
            'queued.pdf', 'application/pdf', 'documentClasses', 'PDF', 'EXTRACTING',
            'CLEAN', 'quarantine/queued.pdf', 'protected/queued.pdf', @hash, 10,
            @user, 2, @now, @now);
        INSERT INTO commercial.inventory_extraction_attempts (
            id, tenant_id, import_id, source_file_version, source_hash,
            stable_submission_key, provider_name, provider_version, status_code,
            polling_checkpoint, attempt_number, correlation_id, command_id,
            requested_by, version, created_at_utc, updated_at_utc)
        VALUES (@attempt, @tenant, @import, 1, @hash, 'submission-queued',
            'docling', '1.30.0', 'PENDING', '{}'::jsonb, 1, gen_random_uuid(),
            gen_random_uuid(), @user, 1, @now, @now);
        """;
}

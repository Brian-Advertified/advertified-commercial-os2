using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Advertified.Commercial.Application.Security;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Identity;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

[Collection(RecoveryTestGroup.Name)]
public sealed partial class DatabaseRecoveryAcceptanceTests
{
    private const string Database = "advertified_recovery";
    private const string Username = "advertified_recovery";
    private const string Password = "advertified-recovery-local-only";
    private const string ArchivePath = "/tmp/advertified-recovery.dump";
    private static readonly Guid TenantId =
        Guid.Parse("e1000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherTenantId =
        Guid.Parse("e1000000-0000-0000-0000-000000000002");
    private static readonly Guid UserId =
        Guid.Parse("e2000000-0000-0000-0000-000000000001");
    private static readonly Guid ClientId =
        Guid.Parse("e3000000-0000-0000-0000-000000000001");
    private static readonly Guid ImportId =
        Guid.Parse("e4000000-0000-0000-0000-000000000001");
    private static readonly Guid OutboxId =
        Guid.Parse("e5000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Now =
        new(2026, 8, 31, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    [Trait("Category", "Recovery")]
    public async Task CustomBackupRestoresCanonicalTenantSafeStateIntoIsolation()
    {
        await using var source = CreatePostgres();
        await using var target = CreatePostgres();
        await using var sourceObjects = DisposableMinio.Create();
        await using var targetObjects = DisposableMinio.Create();
        await Task.WhenAll(
            source.StartAsync(), target.StartAsync(),
            sourceObjects.StartAsync(), targetObjects.StartAsync());
        await PrepareDatabasesAsync(source, target);
        await PrepareObjectStoresAsync(sourceObjects, targetObjects);
        await SeedSourceAsync(source.GetConnectionString());
        var objectBackup = await CreateObjectBackupAsync(sourceObjects);

        var dump = await source.ExecAsync(
            ["pg_dump", "--format=custom", "--no-owner", "--file", ArchivePath,
                "--username", Username, Database]);
        Assert.Equal(0, dump.ExitCode);
        var archive = await source.ReadFileAsync(ArchivePath);
        Assert.True(archive.Length > 10_000, "The database backup archive was unexpectedly empty.");

        await target.CopyAsync(archive, ArchivePath);
        var restore = await target.ExecAsync(
            ["pg_restore", "--exit-on-error", "--no-owner", "--dbname", Database,
                "--username", Username, ArchivePath]);
        Assert.Equal(0, restore.ExitCode);

        var reference = await ReadRestoredObjectReferenceAsync(target.GetConnectionString());
        await AssertInvalidBackupsLeaveTargetEmptyAsync(
            targetObjects, objectBackup, reference);
        await sourceObjects.StopAsync();
        await RestoreObjectAsync(targetObjects, objectBackup, reference);
        await AssertRestoredStateAsync(target.GetConnectionString());
        await AssertRestoredObjectAsync(targetObjects, objectBackup, reference);
    }

    private static PostgreSqlContainer CreatePostgres() => DisposablePostgres.Create(
        Database, Username, Password);

    private static async Task PrepareDatabasesAsync(
        PostgreSqlContainer source,
        PostgreSqlContainer target)
    {
        await Task.WhenAll(
            DisposablePostgres.EnableRequiredExtensionsAsync(source.GetConnectionString()),
            DisposablePostgres.EnableRequiredExtensionsAsync(target.GetConnectionString()));
        await Task.WhenAll(
            DisposableDatabaseRoles.ProvisionAsync(source.GetConnectionString()),
            DisposableDatabaseRoles.ProvisionAsync(target.GetConnectionString()));
    }

    private static async Task SeedSourceAsync(string connectionString)
    {
        var options = new DbContextOptionsBuilder<GovernanceDbContext>()
            .UseNpgsql(connectionString).Options;
        await using var db = new GovernanceDbContext(options);
        await db.Database.MigrateAsync();
        await new MasterDataBootstrapper(db, TimeProvider.System).ApplyAsync();
        db.Tenants.AddRange(
            Tenant(TenantId, "recovery-source"),
            Tenant(OtherTenantId, "recovery-unrelated"));
        db.Users.Add(new User(
            new UserId(UserId), new EmailAddress("recovery@example.test"),
            "Recovery Operator", null, new LifecycleStatusCode("ACTIVE"), true, Now));
        db.Memberships.Add(new Membership(
            new MembershipId(Guid.Parse("e6000000-0000-0000-0000-000000000001")),
            new TenantId(TenantId), new UserId(UserId), new RoleCode("platform_admin"),
            new LifecycleStatusCode("ACTIVE"), null, Now));
        db.ClientAccounts.Add(new ClientAccount(
            new ClientAccountId(ClientId), new TenantId(TenantId), "recovery-client",
            "Recovery Client Legal", "Recovery Client", null, null, "{}",
            new LifecycleStatusCode("ACTIVE"), Now));
        await db.SaveChangesAsync();
        await SeedOperationalRecordsAsync(connectionString);
    }

    private static Tenant Tenant(Guid id, string slug) => new(
        new TenantId(id), new TenantTypeCode("AGENCY"), $"{slug} legal", slug,
        new Slug(slug), new LifecycleStatusCode("ACTIVE"), "Africa/Johannesburg",
        new CurrencyCode("ZAR"), new VatStatusCode("REGISTERED"), null, "{}", Now);

    private static async Task SeedOperationalRecordsAsync(string connectionString)
    {
        var supplierId = Guid.Parse("e7000000-0000-0000-0000-000000000001");
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var batch = new NpgsqlBatch(connection);
        Add(batch, """
            INSERT INTO commercial.inventory_suppliers (
                id, tenant_id, name, version, created_at_utc, updated_at_utc)
            VALUES ($1, $2, 'Recovery Supplier', 1, $3, $3)
            """, supplierId, TenantId, Now);
        Add(batch, """
            INSERT INTO commercial.inventory_imports (
                id, tenant_id, supplier_id, source_file_name, declared_media_type,
                status_code, scan_status_code, quarantine_object_key,
                protected_object_key, source_hash, source_size, created_by,
                version, created_at_utc, updated_at_utc)
            VALUES ($1, $2, $3, 'recovery.csv', 'text/csv', 'UPLOADED', 'CLEAN',
                $4, $5, $6, $7, $8, 1, $9, $9)
            """, ImportId, TenantId, supplierId,
            QuarantineObjectKey, ProtectedObjectKey,
            ObjectHash, ObjectContent.LongLength, UserId, Now);
        Add(batch, """
            INSERT INTO commercial.outbox_messages (
                id, tenant_id, causation_id, correlation_id, event_type_code,
                aggregate_type_code, aggregate_id, aggregate_version, payload_json,
                occurred_at_utc, attempts)
            VALUES ($1, $2, $3, $4, 'RecoveryFixtureRecorded', 'inventory_import',
                $5, 1, '{"fixture":"recovery"}', $6, 0)
            """, OutboxId, TenantId,
            Guid.Parse("e8000000-0000-0000-0000-000000000001"),
            Guid.Parse("e8000000-0000-0000-0000-000000000002"), ImportId, Now);
        await batch.ExecuteNonQueryAsync();
    }

    private static void Add(NpgsqlBatch batch, string sql, params object[] values)
    {
        var command = new NpgsqlBatchCommand(sql);
        foreach (var value in values) command.Parameters.AddWithValue(value);
        batch.BatchCommands.Add(command);
    }

    private static async Task AssertRestoredStateAsync(string connectionString)
    {
        await AssertAuthenticatedAccessAsync(connectionString);
        await AssertRestoredMembershipAsync(connectionString);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        Assert.Equal(2, await CountAsync(connection, "SELECT count(*)::integer FROM commercial.tenants"));
        Assert.Equal(1, await CountAsync(connection, "SELECT count(*)::integer FROM commercial.memberships"));
        Assert.Equal(1, await CountAsync(connection, "SELECT count(*)::integer FROM commercial.client_accounts"));
        Assert.Equal(1, await CountAsync(connection, "SELECT count(*)::integer FROM commercial.outbox_messages WHERE published_at_utc IS NULL"));
        Assert.Equal(1, await CountAsync(connection, """
            SELECT count(*)::integer FROM "__EFMigrationsHistory"
            WHERE "MigrationId" = '202608300024_MeasurementReports'
            """));
        Assert.Equal(1, await CountAsync(connection, $"""
            SELECT count(DISTINCT registry_version)::integer
            FROM governance.master_data_collections
            WHERE registry_version = '{MasterDataCodes.RegistryVersion}'
            """));
        Assert.Equal(80, await CountAsync(connection, """
            SELECT count(*)::integer FROM pg_class item
            JOIN pg_namespace scope ON scope.oid = item.relnamespace
            WHERE scope.nspname = 'commercial' AND item.relrowsecurity
              AND item.relforcerowsecurity
            """));
        await AssertRestoredTraceAsync(connection);
        await AssertApplicationRoleIsolationAsync(connection);
    }

    private static async Task AssertAuthenticatedAccessAsync(string connectionString)
    {
        await using var factory = CreateRestoredApi(connectionString);
        using var client = factory.CreateClient();
        using var profile = await client.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        var workspaces = await client.GetFromJsonAsync<JsonElement[]>("/api/v1/workspaces");
        Assert.NotNull(workspaces);
        Assert.Single(workspaces);
        Assert.Equal(TenantId, workspaces[0].GetProperty("tenantId").GetGuid());
        using var denied = await client.GetAsync($"/api/v1/tenants/{OtherTenantId}");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateRestoredApi(string connectionString) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("ConnectionStrings:CommercialDatabase", connectionString);
            builder.UseSetting("Authentication:Mode", "Deterministic");
            builder.UseSetting("Authentication:DevelopmentIdentity:UserId", UserId.ToString());
            builder.UseSetting("Authentication:DevelopmentIdentity:ActorId", UserId.ToString());
            builder.UseSetting("Authentication:DevelopmentIdentity:IdentityType", "human");
        });

    private static async Task AssertRestoredMembershipAsync(string connectionString)
    {
        await using var db = new GovernanceDbContext(
            new DbContextOptionsBuilder<GovernanceDbContext>()
                .UseNpgsql(connectionString).Options);
        var source = new DatabaseTenantMembershipSource(db);
        var membership = await source.FindAsync(
            new ActorId(UserId), new TenantId(TenantId), default);
        Assert.NotNull(membership);
        Assert.Contains(MasterDataReferences.Permissions.CampaignView, membership.Permissions);
        Assert.Null(await source.FindAsync(
            new ActorId(UserId), new TenantId(OtherTenantId), default));
    }

    private static async Task AssertRestoredTraceAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand("""
            SELECT import.protected_object_key, import.source_hash, message.payload_json::text
            FROM commercial.inventory_imports import
            JOIN commercial.outbox_messages message
              ON message.tenant_id = import.tenant_id
             AND message.aggregate_id = import.id
            WHERE import.id = $1 AND message.id = $2
            """, connection);
        command.Parameters.AddWithValue(ImportId);
        command.Parameters.AddWithValue(OutboxId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(ProtectedObjectKey, reader.GetString(0));
        Assert.Equal(ObjectHash, reader.GetString(1));
        Assert.Equal("{\"fixture\": \"recovery\"}", reader.GetString(2));
    }

    private static async Task AssertApplicationRoleIsolationAsync(NpgsqlConnection connection)
    {
        await using var transaction = await connection.BeginTransactionAsync();
        await ExecuteAsync(connection, "SET LOCAL ROLE advertified_app");
        await SetSessionAsync(connection, TenantId);
        Assert.Equal(1, await CountAsync(connection,
            "SELECT count(*)::integer FROM commercial.client_accounts"));
        await SetSessionAsync(connection, OtherTenantId);
        Assert.Equal(0, await CountAsync(connection,
            "SELECT count(*)::integer FROM commercial.client_accounts"));
        await transaction.RollbackAsync();
    }

    private static async Task SetSessionAsync(NpgsqlConnection connection, Guid tenantId)
    {
        await using var command = new NpgsqlCommand(
            "SELECT set_config('advertified.user_id', $1, true), " +
            "set_config('advertified.tenant_id', $2, true)", connection);
        command.Parameters.AddWithValue(UserId.ToString());
        command.Parameters.AddWithValue(tenantId.ToString());
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> CountAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return (int)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Recovery count was unavailable."));
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}

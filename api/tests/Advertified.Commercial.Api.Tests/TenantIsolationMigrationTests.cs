using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.Identity;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class TenantIsolationMigrationTests
{
    private const string PostgreSqlImage = "pgvector/pgvector:0.8.6-pg16-bookworm";
    private static readonly TenantId FirstTenant =
        new(Guid.Parse("a1000000-0000-0000-0000-000000000001"));
    private static readonly TenantId SecondTenant =
        new(Guid.Parse("a2000000-0000-0000-0000-000000000002"));
    private static readonly UserId User =
        new(Guid.Parse("b1000000-0000-0000-0000-000000000001"));
    private static readonly MembershipId MembershipRecordId =
        new(Guid.Parse("b2000000-0000-0000-0000-000000000002"));
    private static readonly ClientAccountId FirstClient =
        new(Guid.Parse("c1000000-0000-0000-0000-000000000001"));
    private static readonly ClientAccountId SecondClient =
        new(Guid.Parse("c2000000-0000-0000-0000-000000000002"));

    [Fact]
    [Trait("Category", "Migration")]
    public async Task ApplicationRoleDefaultsClosedAndRejectsCrossTenantAssociation()
    {
        await using var postgres = new PostgreSqlBuilder(PostgreSqlImage)
            .WithDatabase("advertified_gate2_rls")
            .WithUsername("advertified_gate2")
            .WithPassword("advertified-gate2-local-only")
            .Build();
        await postgres.StartAsync();

        await DisposableDatabaseRoles.ProvisionAsync(postgres.GetConnectionString());
        await SeedAsync(postgres.GetConnectionString());
        await AssertDatabasePermissionsAsync(postgres.GetConnectionString());

        await using var connection = new NpgsqlConnection(postgres.GetConnectionString());
        await connection.OpenAsync();
        await ExecuteAsync(connection, "SET ROLE advertified_app");

        Assert.False(await ApplicationRoleBypassesRlsAsync(connection));
        Assert.Equal(9, await ProtectedTableCountAsync(connection));
        Assert.Equal(0, await CountClientsAsync(connection));

        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await SetTenantAsync(connection, FirstTenant);
            Assert.Equal(1, await CountClientsAsync(connection));

            var exception = await Assert.ThrowsAsync<PostgresException>(() =>
                InsertCrossTenantContactAsync(connection));
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, exception.SqlState);
            await transaction.RollbackAsync();
        }

        Assert.Equal(0, await CountClientsAsync(connection));
    }

    private static async Task SeedAsync(string connectionString)
    {
        var options = new DbContextOptionsBuilder<GovernanceDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var dbContext = new GovernanceDbContext(options);
        await dbContext.Database.MigrateAsync();
        await new MasterDataBootstrapper(dbContext, TimeProvider.System).ApplyAsync();

        var now = DateTimeOffset.UtcNow;
        dbContext.Tenants.AddRange(
            CreateTenant(FirstTenant, "first-workspace", "First Workspace", now),
            CreateTenant(SecondTenant, "second-workspace", "Second Workspace", now));
        dbContext.Users.Add(new User(
            User,
            new EmailAddress("owner@example.test"),
            "Local Owner",
            null,
            new LifecycleStatusCode("ACTIVE"),
            true,
            now));
        dbContext.Memberships.Add(new Membership(
            MembershipRecordId,
            FirstTenant,
            User,
            new RoleCode("agency_admin"),
            MasterDataConstants.ActiveStatus,
            null,
            now));
        dbContext.ClientAccounts.AddRange(
            CreateClient(FirstClient, FirstTenant, "first-client", now),
            CreateClient(SecondClient, SecondTenant, "second-client", now));
        await dbContext.SaveChangesAsync();
    }

    private static async Task AssertDatabasePermissionsAsync(string connectionString)
    {
        await using var dbContext = new GovernanceDbContext(
            new DbContextOptionsBuilder<GovernanceDbContext>()
                .UseNpgsql(connectionString)
                .Options);
        var source = new DatabaseTenantMembershipSource(dbContext);
        var actor = new ActorId(User.Value);

        var allowed = await source.FindAsync(actor, FirstTenant, default);
        var crossTenant = await source.FindAsync(actor, SecondTenant, default);

        Assert.NotNull(allowed);
        Assert.Contains(Gate2Permissions.ContactManage, allowed.Permissions);
        Assert.Null(crossTenant);
    }

    private static Tenant CreateTenant(
        TenantId id,
        string slug,
        string name,
        DateTimeOffset now)
    {
        return new Tenant(
            id,
            new TenantTypeCode("AGENCY"),
            name,
            name,
            new Slug(slug),
            new LifecycleStatusCode("ACTIVE"),
            "Africa/Johannesburg",
            new CurrencyCode("ZAR"),
            new VatStatusCode("REGISTERED"),
            null,
            "{}",
            now);
    }

    private static ClientAccount CreateClient(
        ClientAccountId id,
        TenantId tenantId,
        string externalReference,
        DateTimeOffset now)
    {
        return new ClientAccount(
            id,
            tenantId,
            externalReference,
            "Client Legal Name",
            "Client Trading Name",
            null,
            null,
            "{}",
            new LifecycleStatusCode("ACTIVE"),
            now);
    }

    private static async Task SetTenantAsync(NpgsqlConnection connection, TenantId tenantId)
    {
        await using var command = new NpgsqlCommand(
            "SELECT set_config('advertified.tenant_id', $1, true)",
            connection);
        command.Parameters.AddWithValue(tenantId.Value.ToString());
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> CountClientsAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand(
            "SELECT count(*)::integer FROM commercial.client_accounts",
            connection);
        return (int)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Client count was unavailable."));
    }

    private static async Task<bool> ApplicationRoleBypassesRlsAsync(
        NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand(
            "SELECT rolbypassrls FROM pg_roles WHERE rolname = current_user",
            connection);
        return (bool)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Role security was unavailable."));
    }

    private static async Task<int> ProtectedTableCountAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT count(*)::integer
            FROM pg_class item
            JOIN pg_namespace scope ON scope.oid = item.relnamespace
            WHERE scope.nspname = 'commercial'
              AND item.relrowsecurity
              AND item.relforcerowsecurity
            """,
            connection);
        return (int)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Policy count was unavailable."));
    }

    private static async Task InsertCrossTenantContactAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO commercial.contacts (
                id, tenant_id, client_account_id, name, email, purpose_code,
                consent_basis, status_code, version, created_at_utc, updated_at_utc
            ) VALUES (
                gen_random_uuid(), $1, $2, 'Wrong tenant contact',
                'wrong@example.test', 'COMMERCIAL', 'Local acceptance fixture',
                'ACTIVE', 1, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
            )
            """,
            connection);
        command.Parameters.AddWithValue(FirstTenant.Value);
        command.Parameters.AddWithValue(SecondClient.Value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}

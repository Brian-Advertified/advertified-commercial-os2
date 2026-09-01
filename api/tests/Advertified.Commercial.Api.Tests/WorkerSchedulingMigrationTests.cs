using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Worker;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class WorkerSchedulingMigrationTests
{
    [Fact]
    [Trait("Category", "Migration")]
    public async Task WorkerSchedulerIsLeastPrivilegeAndFencesUnknownCompletion()
    {
        await using var postgres = DisposablePostgres.Create(
            "advertified_worker_migration",
            "advertified_worker_migration",
            "advertified-worker-migration-local-only");
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DisposablePostgres.EnableRequiredExtensionsAsync(connectionString);
        await DisposableDatabaseRoles.ProvisionAsync(connectionString);

        var options = new DbContextOptionsBuilder<GovernanceDbContext>()
            .UseNpgsql(connectionString).Options;
        await using (var database = new GovernanceDbContext(options))
        {
            await database.Database.MigrateAsync();
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using (var role = new NpgsqlCommand("SET ROLE advertified_worker", connection))
        {
            await role.ExecuteNonQueryAsync();
        }

        await using (var fenced = new NpgsqlCommand(
            """
            SELECT commercial.complete_email_work(
                gen_random_uuid(), TRUE, NULL, 60, 5)
            """,
            connection))
        {
            Assert.Equal(EmailWorkerCompletion.Fenced, await fenced.ExecuteScalarAsync());
        }

        await using var directRead = new NpgsqlCommand(
            "SELECT count(*) FROM commercial.email_worker_claims",
            connection);
        var denied = await Assert.ThrowsAsync<PostgresException>(
            () => directRead.ExecuteScalarAsync());
        Assert.Equal("42501", denied.SqlState);
    }
}

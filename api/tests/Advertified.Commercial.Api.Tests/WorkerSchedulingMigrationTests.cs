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
        await VerifyAgentWakeTransportAsync(connectionString, connection);
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

        foreach (var query in new[]
        {
            "SELECT count(*) FROM commercial.email_worker_claims",
            "SELECT count(*) FROM commercial.agent_runs",
        })
        {
            await using var directRead = new NpgsqlCommand(query, connection);
            var denied = await Assert.ThrowsAsync<PostgresException>(
                () => directRead.ExecuteScalarAsync());
            Assert.Equal("42501", denied.SqlState);
        }
    }

    private static async Task VerifyAgentWakeTransportAsync(
        string connectionString, NpgsqlConnection connection)
    {
        // This fixture migrates as its test administrator. Match the production migrator's
        // ownership for the SECURITY DEFINER deadline probe without granting worker reads.
        await using var ownership = new NpgsqlCommand("""
            GRANT USAGE ON SCHEMA commercial TO advertified_migrator;
            ALTER TABLE commercial.agent_runs OWNER TO advertified_migrator;
            """, connection);
        await ownership.ExecuteNonQueryAsync();
        var scheduler = new WorkerSchedulerStore(connectionString);
        Assert.Null(await scheduler.NextAgentRunDueAsync(CancellationToken.None));
        await using (var listener = await scheduler.OpenAgentRunListenerAsync(CancellationToken.None))
        {
            await using var rollback = await connection.BeginTransactionAsync();
            await using var signal = new NpgsqlCommand(
                "SELECT pg_notify('advertified_agent_run', '')", connection, rollback);
            await signal.ExecuteNonQueryAsync();
            await rollback.RollbackAsync();
            Assert.False(await listener.WaitAsync(TimeSpan.FromMilliseconds(100), CancellationToken.None));
            signal.Transaction = null;
            await signal.ExecuteNonQueryAsync();
            Assert.True(await listener.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None));
        }
    }
}

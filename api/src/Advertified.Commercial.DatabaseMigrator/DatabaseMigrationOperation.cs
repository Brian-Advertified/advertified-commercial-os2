using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Advertified.Commercial.DatabaseMigrator;

public sealed record DatabaseMigrationResult(
    IReadOnlyList<string> AppliedMigrations,
    MasterDataBootstrapResult MasterData);

public sealed class DatabaseMigrationOperation(TimeProvider timeProvider)
{
    private const string RequiredRole = "advertified_migrator";
    private const string SetRoleSql = "SET ROLE advertified_migrator";

    public async Task<DatabaseMigrationResult> ApplyAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await SetAndVerifyRoleAsync(connection, cancellationToken);

        var options = new DbContextOptionsBuilder<GovernanceDbContext>()
            .UseNpgsql(connection)
            .Options;
        await using var dbContext = new GovernanceDbContext(options);
        var pending = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken))
            .ToArray();

        await dbContext.Database.MigrateAsync(cancellationToken);
        var masterData = await new MasterDataBootstrapper(dbContext, timeProvider)
            .ApplyAsync(cancellationToken);

        return new DatabaseMigrationResult(pending, masterData);
    }

    private static async Task SetAndVerifyRoleAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using (var setRole = new NpgsqlCommand(SetRoleSql, connection))
        {
            await setRole.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var verify = new NpgsqlCommand(
            """
            SELECT rolname, rolsuper, rolcreaterole, rolcreatedb, rolbypassrls
            FROM pg_roles
            WHERE rolname = current_user
            """,
            connection);
        await using var reader = await verify.ExecuteReaderAsync(cancellationToken);
        var valid = await reader.ReadAsync(cancellationToken)
            && string.Equals(reader.GetString(0), RequiredRole, StringComparison.Ordinal)
            && !reader.GetBoolean(1)
            && !reader.GetBoolean(2)
            && !reader.GetBoolean(3)
            && !reader.GetBoolean(4);

        if (!valid)
        {
            throw new InvalidOperationException(
                "The effective database migration role is not least privilege.");
        }
    }
}

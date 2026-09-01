using Npgsql;

namespace Advertified.Commercial.Api.Tests;

internal static class DisposableDatabaseRoles
{
    public static async Task ProvisionAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            CREATE ROLE advertified_migrator
                NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOBYPASSRLS;
            CREATE ROLE advertified_app
                NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOBYPASSRLS;
            CREATE ROLE advertified_worker
                NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOBYPASSRLS;
            """,
            connection);
        await command.ExecuteNonQueryAsync();
    }
}

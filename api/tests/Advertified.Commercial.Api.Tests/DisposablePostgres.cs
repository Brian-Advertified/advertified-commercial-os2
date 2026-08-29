using Npgsql;
using Testcontainers.PostgreSql;

namespace Advertified.Commercial.Api.Tests;

internal static class DisposablePostgres
{
    private const string Image = "advertified/postgres-dev:16-postgis3-pgvector0.8.6";

    internal static PostgreSqlContainer Create(
        string database,
        string username,
        string password) => new PostgreSqlBuilder(Image)
        .WithDatabase(database)
        .WithUsername(username)
        .WithPassword(password)
        .Build();

    internal static async Task EnableRequiredExtensionsAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE EXTENSION IF NOT EXISTS pgcrypto;
            CREATE EXTENSION IF NOT EXISTS postgis;
            CREATE EXTENSION IF NOT EXISTS vector;
            """;
        await command.ExecuteNonQueryAsync();
    }
}

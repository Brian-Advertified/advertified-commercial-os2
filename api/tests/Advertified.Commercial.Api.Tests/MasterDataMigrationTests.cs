using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class MasterDataMigrationTests
{
    private const string PostgreSqlImage = "pgvector/pgvector:0.8.6-pg16-bookworm";

    [Fact]
    [Trait("Category", "Migration")]
    public async Task MigrationBootstrapsRegistryIdempotentlyAndProtectsStableCodes()
    {
        await using var postgres = new PostgreSqlBuilder(PostgreSqlImage)
            .WithDatabase("advertified_gate1")
            .WithUsername("advertified_gate1")
            .WithPassword("advertified-gate1-local-only")
            .Build();
        await postgres.StartAsync();

        var options = new DbContextOptionsBuilder<GovernanceDbContext>()
            .UseNpgsql(postgres.GetConnectionString())
            .Options;
        await using var dbContext = new GovernanceDbContext(options);

        await dbContext.Database.MigrateAsync();

        var bootstrapper = new MasterDataBootstrapper(
            dbContext,
            new FixedTimeProvider());
        var first = await bootstrapper.ApplyAsync();
        var second = await bootstrapper.ApplyAsync();

        Assert.Equal(first, second);
        Assert.Equal(first.CollectionCount, await dbContext.MasterDataSets.CountAsync());
        Assert.Equal(first.ItemCount, await dbContext.MasterDataItems.CountAsync());
        Assert.True(first.CollectionCount > 0);
        Assert.True(first.ItemCount > first.CollectionCount);
        Assert.True(await dbContext.MasterDataItemHistory.CountAsync() >= first.ItemCount);

        var item = await dbContext.MasterDataItems
            .OrderBy(value => value.CollectionCode)
            .ThenBy(value => value.Code)
            .FirstAsync();
        const string changedCode = "changed-code";
        FormattableString updateCode = $"""
            UPDATE governance.master_data_items
            SET code = {changedCode}
            WHERE collection_code = {item.CollectionCode} AND code = {item.Code}
            """;
        FormattableString deleteItem = $"""
            DELETE FROM governance.master_data_items
            WHERE collection_code = {item.CollectionCode} AND code = {item.Code}
            """;

        await Assert.ThrowsAsync<PostgresException>(() =>
            dbContext.Database.ExecuteSqlInterpolatedAsync(updateCode));
        await Assert.ThrowsAsync<PostgresException>(() =>
            dbContext.Database.ExecuteSqlInterpolatedAsync(deleteItem));

        var migrator = dbContext.GetService<IMigrator>();
        await migrator.MigrateAsync(Migration.InitialDatabase);

        Assert.False(await SchemaExistsAsync(
            postgres.GetConnectionString(),
            "governance"));
    }

    private static async Task<bool> SchemaExistsAsync(
        string connectionString,
        string schemaName)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT to_regnamespace($1) IS NOT NULL",
            connection);
        command.Parameters.AddWithValue(schemaName);

        return (bool)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Schema check returned no result."));
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);
        }
    }
}

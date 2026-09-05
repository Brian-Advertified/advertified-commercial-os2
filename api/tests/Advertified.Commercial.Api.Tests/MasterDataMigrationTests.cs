using Advertified.Commercial.DatabaseMigrator;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Migrations;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class MasterDataMigrationTests
{
    private const string DatabaseName = "advertified_master_data";
    private const string DatabaseUser = "advertified_master_data";
    private const string DatabasePassword = "advertified-master-data-local-only";

    [Fact]
    [Trait("Category", "Migration")]
    public async Task MigrationBootstrapsRegistryIdempotentlyAndProtectsStableCodes()
    {
        await using var postgres = DisposablePostgres.Create(
            DatabaseName, DatabaseUser, DatabasePassword);
        await postgres.StartAsync();
        await DisposablePostgres.EnableRequiredExtensionsAsync(postgres.GetConnectionString());

        await AssertApiStartupDoesNotMigrateAsync(postgres.GetConnectionString());
        await PrepareMigrationRoleAsync(postgres.GetConnectionString());

        var options = new DbContextOptionsBuilder<GovernanceDbContext>()
            .UseNpgsql(postgres.GetConnectionString())
            .Options;
        await using var dbContext = new GovernanceDbContext(options);

        var operation = new DatabaseMigrationOperation(new FixedTimeProvider());
        var applied = await operation.ApplyAsync(postgres.GetConnectionString());
        Assert.Equal("202609050001_InitialBaseline", Assert.Single(applied.AppliedMigrations));
        var first = applied.MasterData;
        var repeated = await operation.ApplyAsync(postgres.GetConnectionString());
        Assert.Empty(repeated.AppliedMigrations);
        var second = repeated.MasterData;
        Assert.Empty(await dbContext.Tenants.ToListAsync());
        Assert.Empty(await dbContext.Users.ToListAsync());
        Assert.Equal(0, await dbContext.Database.SqlQueryRaw<int>(
            "SELECT count(*)::integer AS \"Value\" FROM commercial.inventory_imports").SingleAsync());

        Assert.Equal(first, second);
        Assert.Equal(first.CollectionCount, await dbContext.MasterDataSets.CountAsync());
        Assert.Equal(first.ItemCount, await dbContext.MasterDataItems.CountAsync());
        Assert.True(first.CollectionCount > 0);
        Assert.True(first.ItemCount > first.CollectionCount);
        Assert.True(await dbContext.MasterDataItemHistory.CountAsync() >= first.ItemCount);

        var collection = await dbContext.MasterDataSets
            .OrderBy(value => value.Code)
            .FirstAsync();
        FormattableString updateCollectionMetadata = $"""
            UPDATE governance.master_data_collections
            SET registry_version = registry_version || '-regression-test'
            WHERE code = {collection.Code}
            """;
        const string changedCollectionCode = "changed-collection-code";
        FormattableString updateCollectionCode = $"""
            UPDATE governance.master_data_collections
            SET code = {changedCollectionCode}
            WHERE code = {collection.Code}
            """;

        Assert.Equal(
            1,
            await dbContext.Database.ExecuteSqlInterpolatedAsync(updateCollectionMetadata));
        await Assert.ThrowsAsync<PostgresException>(() =>
            dbContext.Database.ExecuteSqlInterpolatedAsync(updateCollectionCode));

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

    private static async Task AssertApiStartupDoesNotMigrateAsync(string connectionString)
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseDeterministicInventoryProtection();
                builder.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Authentication:Mode"] = "Disabled",
                        ["ConnectionStrings:CommercialDatabase"] = connectionString,
                    }));
            });
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/health/live");

        response.EnsureSuccessStatusCode();
        Assert.False(await SchemaExistsAsync(connectionString, "governance"));
    }

    private static async Task PrepareMigrationRoleAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            $"""
            CREATE ROLE advertified_migrator
                NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOBYPASSRLS;
            CREATE ROLE advertified_app
                NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOBYPASSRLS;
            CREATE ROLE advertified_worker
                NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOBYPASSRLS;
            GRANT advertified_migrator TO {DatabaseUser};
            GRANT CREATE ON DATABASE {DatabaseName} TO advertified_migrator;
            GRANT CREATE ON SCHEMA public TO advertified_migrator;
            """,
            connection);
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public void ModelSnapshotMatchesCurrentPersistenceModel()
    {
        var options = new DbContextOptionsBuilder<GovernanceDbContext>()
            .UseNpgsql("Host=localhost;Database=model-only;Username=model-only")
            .Options;
        using var dbContext = new GovernanceDbContext(options);
        var modelDiffer = dbContext.GetService<IMigrationsModelDiffer>();
        var currentModel = dbContext.GetService<IDesignTimeModel>().Model;
        var snapshot = new GovernanceDbContextModelSnapshot().Model;
        var snapshotModel = dbContext.GetService<IModelRuntimeInitializer>()
            .Initialize(snapshot, designTime: true);

        var differences = modelDiffer.GetDifferences(
            snapshotModel.GetRelationalModel(),
            currentModel.GetRelationalModel());

        Assert.False(
            differences.Count > 0,
            string.Join(", ", differences.Select(DescribeOperation)));
    }

    private static string DescribeOperation(MigrationOperation operation)
    {
        return operation switch
        {
            AlterColumnOperation column =>
                $"Alter:{column.Table}.{column.Name}:" +
                $"new[{column.ColumnType},{column.MaxLength},{column.IsNullable}," +
                $"{column.IsUnicode},{column.DefaultValueSql}]" +
                $"old[{column.OldColumn.ColumnType},{column.OldColumn.MaxLength}," +
                $"{column.OldColumn.IsNullable},{column.OldColumn.IsUnicode}," +
                $"{column.OldColumn.DefaultValueSql}]",
            CreateIndexOperation index =>
                $"Index:{index.Table}.{index.Name}({string.Join('|', index.Columns)})",
            _ => operation.GetType().Name,
        };
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

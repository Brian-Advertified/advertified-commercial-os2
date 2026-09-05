using Npgsql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class InventoryAcceptanceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Category", "Migration")]
    public async Task DocumentReevaluationPreservesHistoricalExclusions(bool deleted)
    {
        await using var postgres = CreatePostgres();
        await postgres.StartAsync();
        var connection = postgres.GetConnectionString();
        await DisposableDatabaseRoles.ProvisionAsync(connection);
        await SeedAsync(connection);
        await using var importerFactory = CreateFactory(connection, ImporterId, new SchemaFixtureAdapter());
        await using var reviewerFactory = CreateFactory(connection, ReviewerId);
        using var importer = importerFactory.CreateClient();
        using var reviewer = reviewerFactory.CreateClient();
        var record = await CreateAndExecuteAsync(importer, CsvFixture());
        // Seed a historical human exclusion; the test exercises its persistence across real commands.
        await using var db = new NpgsqlConnection(connection);
        await db.OpenAsync();
        await using var exclude = new NpgsqlCommand(deleted
            ? "UPDATE commercial.inventory_candidates SET soft_deleted_at_utc = now() WHERE import_id = @id"
            : "UPDATE commercial.inventory_candidates SET status_code = 'REJECTED', reviewed_by = @reviewer WHERE import_id = @id", db);
        exclude.Parameters.AddWithValue("id", record.GetProperty("id").GetGuid());
        exclude.Parameters.AddWithValue("reviewer", ReviewerId);
        await exclude.ExecuteNonQueryAsync();
        record = await ReevaluateAsync(reviewer, record, "carry-history");
        Assert.Equal("REJECTED", record.GetProperty("candidates")[0].GetProperty("status").GetString());
        record = await ReevaluateAsync(reviewer, record, "carry-history-again");
        Assert.Equal("REJECTED", record.GetProperty("candidates")[0].GetProperty("status").GetString());
        Assert.Equal(0, record.GetProperty("candidateCounts").GetProperty("approved").GetInt32());
    }
}

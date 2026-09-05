using System.Text.Json;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Infrastructure.Inventory;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class InventoryAcceptanceTests
{
    [Fact]
    [Trait("Category", "Migration")]
    public async Task UploadedSourcesAreSizeCheckedScannedAndProtected()
    {
        await using var postgres = CreatePostgres();
        await postgres.StartAsync();
        var connection = postgres.GetConnectionString();
        await DisposableDatabaseRoles.ProvisionAsync(connection);
        await SeedAsync(connection);
        await using var factory = CreateFactory(connection, ImporterId);
        using var importer = factory.CreateClient();
        await AssertProtectionBoundariesAsync(importer,
            factory.Services.GetRequiredService<IInventoryObjectStore>());
    }

    private static async Task<JsonElement> CreateAndExecuteAsync(
        HttpClient importer, FileFixture fixture)
    {
        using var upload = await UploadAsync(
            importer, "inventory-main-upload", "Synthetic Supplier", fixture);
        using var created = await ReadJsonAsync(upload);
        var importId = created.RootElement.GetProperty("id").GetGuid();
        using var execute = await CommandAsync(importer,
            $"/api/v1/tenants/{TenantId}/inventory-imports/{importId}:execute",
            "inventory-main-execute", 1, new { });
        using var executed = await ReadJsonAsync(execute);
        return executed.RootElement.Clone();
    }

    private static async Task AssertExtractionCheckpointAsync(
        string connectionString,
        Guid importId,
        bool hasInterpretedRows)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var read = new NpgsqlCommand(
            """
            SELECT adapter_code, adapter_version, schema_version,
                length(source_hash), length(provider_output_hash),
                length(canonical_output_hash), canonical_json::text
            FROM commercial.inventory_extractions
            WHERE import_id = $1
            """, connection);
        read.Parameters.AddWithValue(importId);
        await using var row = await read.ExecuteReaderAsync();
        Assert.True(await row.ReadAsync());
        Assert.Equal("fixture", row.GetString(0));
        Assert.Equal("1", row.GetString(1));
        Assert.Equal(InventoryExtractionOptions.CurrentSchemaVersion, row.GetString(2));
        Assert.Equal(64, row.GetInt32(3));
        Assert.Equal(64, row.GetInt32(4));
        Assert.Equal(64, row.GetInt32(5));
        using var canonical = JsonDocument.Parse(row.GetString(6));
        Assert.Equal(InventoryExtractionOptions.CurrentSchemaVersion,
            canonical.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Equal(hasInterpretedRows ? 1 : 0,
            canonical.RootElement.GetProperty("rows").GetArrayLength());
        await row.CloseAsync();
        await using var mutate = new NpgsqlCommand(
            "UPDATE commercial.inventory_extractions SET adapter_version = 'changed' " +
            "WHERE import_id = $1", connection);
        mutate.Parameters.AddWithValue(importId);
        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => mutate.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.RaiseException, exception.SqlState);
    }
}

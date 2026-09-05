using System.Net;
using System.Text.Json;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Infrastructure.Inventory;
using Npgsql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class InventoryAcceptanceTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Category", "Migration")]
    public async Task DocumentReevaluationPreservesRevisionsAndSeparatesPublication(bool initialSchema)
    {
        await using var postgres = CreatePostgres();
        await postgres.StartAsync();
        var connection = postgres.GetConnectionString();
        await DisposableDatabaseRoles.ProvisionAsync(connection);
        await SeedAsync(connection);
        var adapter = new SchemaFixtureAdapter(initialSchema);
        await using var importerFactory = CreateFactory(connection, ImporterId, adapter);
        await using var reviewerFactory = CreateFactory(connection, ReviewerId);
        await using var otherFactory = CreateFactory(connection, OtherUserId);
        using var importer = importerFactory.CreateClient();
        using var reviewer = reviewerFactory.CreateClient();
        using var other = otherFactory.CreateClient();
        var record = await CreateAndExecuteAsync(importer, CsvFixture());
        var id = record.GetProperty("id").GetGuid();
        await AssertExtractionCheckpointAsync(connection, id, initialSchema);
        using var originalSource = await importer.GetAsync($"/api/v1/tenants/{TenantId}/inventory-imports/{id}/source");
        Assert.Equal(CsvFixture().Content, await originalSource.Content.ReadAsByteArrayAsync());
        Assert.Equal("attachment", originalSource.Content.Headers.ContentDisposition!.DispositionType);
        if (!initialSchema)
        {
            Assert.Empty(record.GetProperty("candidates").EnumerateArray());
            Assert.Equal(JsonValueKind.Null, record.GetProperty("interpretation").GetProperty("schemaJson").ValueKind);
            var first = InventoryAcceptancePolicyRegressionTests.Fixture(1,
                sourceHash: record.GetProperty("sourceHash").GetString()).Document.DiscoveredSchema;
            record = await ReevaluateAsync(reviewer, record, "first-interpretation", first);
        }
        Assert.Equal("APPROVED", record.GetProperty("candidates")[0].GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, record.GetProperty("publishedReleaseId").ValueKind);
        await AssertDocumentReviewGuardsAsync(importer, other, record);
        record = await ReevaluateAsync(reviewer, record, "policy-only");
        Assert.Equal(1, adapter.Calls);
        var schema = JsonSerializer.Deserialize<DiscoveredInventorySchema>(
            record.GetProperty("interpretation").GetProperty("schemaJson").GetString()!, InventoryRowMapper.StoredJson)!;
        var revision = record.GetProperty("interpretation").GetProperty("mappingRevision").GetString();
        record = await ReevaluateAsync(reviewer, record, "correction", schema);
        Assert.NotEqual(revision, record.GetProperty("interpretation").GetProperty("mappingRevision").GetString());
        Assert.Equal(1, adapter.Calls);
        await AssertTamperedAcceptanceBlockedAsync(connection, reviewer, record);
        using var published = await CommandAsync(reviewer,
            $"/api/v1/tenants/{TenantId}/inventory-imports/{id}:publish", "schema-publish",
            record.GetProperty("version").GetInt64(), new { });
        using var result = await ReadJsonAsync(published);
        Assert.Equal("COMPLETED", result.RootElement.GetProperty("status").GetString());
        using var search = await reviewer.GetAsync($"/api/v1/tenants/{TenantId}/inventory-products?pageSize=1");
        using var page = await ReadJsonAsync(search);
        var product = Assert.Single(page.RootElement.GetProperty("items").EnumerateArray());
        using var detail = await reviewer.GetAsync(
            $"/api/v1/tenants/{TenantId}/inventory-products/{product.GetProperty("id").GetGuid()}");
        detail.EnsureSuccessStatusCode();
        await using var auditDb = new NpgsqlConnection(connection);
        await auditDb.OpenAsync();
        await using var audit = new NpgsqlCommand("SELECT count(*) FROM commercial.audit_events WHERE metadata_json::text LIKE '%acceptanceEvaluations%'", auditDb);
        Assert.Equal(1L, await audit.ExecuteScalarAsync());
    }

    private static async Task AssertTamperedAcceptanceBlockedAsync(string connection, HttpClient reviewer, JsonElement record)
    {
        var candidate = record.GetProperty("candidates")[0];
        await using var db = new NpgsqlConnection(connection);
        await db.OpenAsync();
        await using var mutation = new NpgsqlCommand("""
            UPDATE commercial.inventory_candidates
            SET canonical_values_json = jsonb_set(canonical_values_json, '{name}', '"Forged identity"') WHERE id = @id
            """, db);
        mutation.Parameters.AddWithValue("id", candidate.GetProperty("id").GetGuid());
        await mutation.ExecuteNonQueryAsync();
        using var blocked = await CommandAsync(reviewer,
            $"/api/v1/tenants/{TenantId}/inventory-imports/{record.GetProperty("id").GetGuid()}:publish",
            "forged-publish", record.GetProperty("version").GetInt64(), new { });
        await AssertProblemAsync(blocked, HttpStatusCode.Conflict, "INVENTORY_PUBLISH_BLOCKED");
        mutation.CommandText = "UPDATE commercial.inventory_candidates SET canonical_values_json = @values::jsonb WHERE id = @id";
        mutation.Parameters.AddWithValue("values", candidate.GetProperty("values").GetRawText());
        await mutation.ExecuteNonQueryAsync();
    }

    private static async Task AssertDocumentReviewGuardsAsync(HttpClient importer, HttpClient other, JsonElement record)
    {
        var id = record.GetProperty("id").GetGuid();
        var body = ReevaluateBody(record, null);
        using var self = await CommandAsync(importer, $"/api/v1/tenants/{TenantId}/inventory-imports/{id}:reproject-extraction",
            "self-schema", record.GetProperty("version").GetInt64(), body);
        await AssertProblemAsync(self, HttpStatusCode.Forbidden, "APPROVAL_REQUIRED");
        using var foreign = await other.GetAsync($"/api/v1/tenants/{OtherTenantId}/inventory-imports/{id}");
        await AssertProblemAsync(foreign, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");
        using var foreignSource = await other.GetAsync($"/api/v1/tenants/{OtherTenantId}/inventory-imports/{id}/source");
        await AssertProblemAsync(foreignSource, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");
    }

    private static async Task<JsonElement> ReevaluateAsync(HttpClient client, JsonElement record, string key,
        DiscoveredInventorySchema? schema = null)
    {
        var id = record.GetProperty("id").GetGuid();
        using var response = await CommandAsync(client,
            $"/api/v1/tenants/{TenantId}/inventory-imports/{id}:reproject-extraction", key,
            record.GetProperty("version").GetInt64(), ReevaluateBody(record, schema));
        using var json = await ReadJsonAsync(response);
        return json.RootElement.Clone();
    }

    private static ReprojectInventoryExtractionCommand ReevaluateBody(JsonElement record, DiscoveredInventorySchema? schema) =>
        new("Source evidence checked by the assigned reviewer.", true, schema,
            record.GetProperty("interpretation").GetProperty("mappingRevision").GetString());

    private sealed class SchemaFixtureAdapter(bool initialSchema = true, bool readCsvIdentity = false) : IInventoryDocumentExtractionAdapter
    {
        internal int Calls { get; private set; }
        public Task<InventoryExtractionResult> ExtractAsync(InventoryExtractionRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            var identity = readCsvIdentity
                ? System.Text.Encoding.UTF8.GetString(request.Content)
                    .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)[1].Split(',')
                : null;
            var extraction = InventoryAcceptancePolicyRegressionTests.Fixture(1, sourceHash: request.SourceHash,
                productCode: identity?[0], name: identity?[1]);
            return Task.FromResult(InventoryExtractionContract.Create(extraction.AdapterCode, extraction.AdapterVersion,
                InventoryExtractionOptions.CurrentSchemaVersion, extraction.SourceHash, extraction.ProviderJson,
                initialSchema ? extraction.Rows : [], initialSchema ? extraction.Document.DiscoveredSchema : null,
                initialSchema ? null : "Fixture interpretation was interrupted."));
        }
    }
}

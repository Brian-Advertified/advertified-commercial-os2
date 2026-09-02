using System.Net;
using System.Text.Json;
using Npgsql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class InventoryAcceptanceTests
{
    private static readonly string[] ProposalAssetRightsScopes =
    [
        "INTERNAL_PLANNING",
        "NAMED_CLIENT_PROPOSAL",
        "MARKETPLACE_DISPLAY",
    ];

    [Fact]
    [Trait("Category", "Migration")]
    public async Task RightsVectorsAndDuplicateReconciliationAreHumanGoverned()
    {
        await using var postgres = CreatePostgres();
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DisposableDatabaseRoles.ProvisionAsync(connectionString);
        await SeedAsync(connectionString);
        await using var importerFactory = CreateFactory(connectionString, ImporterId);
        await using var reviewerFactory = CreateFactory(connectionString, ReviewerId);
        using var importer = importerFactory.CreateClient();
        using var reviewer = reviewerFactory.CreateClient();

        var source = CsvFixture("VECTOR-001", "Bree Street Gantry");
        var first = await PublishProductAsync(
            importer, reviewer, "vector-first", source);
        var peer = await PublishProductAsync(
            importer, reviewer, "vector-peer",
            CsvFixture("VECTOR-002", "Bree Street Large Format"));
        var exact = await PublishProductAsync(
            importer, reviewer, "vector-exact",
            CsvFixture("VECTOR-003", "Bree Street Gantry"));

        first = await AssertAssetRightsGateAsync(importer, first);
        await SubmitEmbeddingAsync(reviewer, first);
        first = first with { Version = first.Version + 1 };
        using var deniedBackfill = await CommandAsync(
            reviewer,
            $"/api/v1/tenants/{TenantId}/inventory-products/{first.ProductId}/embedding",
            "embedding-backfill-denied", first.Version,
            new { productVersionId = first.ProductVersionId, forceBackfill = true });
        Assert.Equal(HttpStatusCode.Forbidden, deniedBackfill.StatusCode);
        using var forcedBackfill = await CommandAsync(
            importer,
            $"/api/v1/tenants/{TenantId}/inventory-products/{first.ProductId}/embedding",
            "embedding-backfill-approved", first.Version,
            new { productVersionId = first.ProductVersionId, forceBackfill = true });
        forcedBackfill.EnsureSuccessStatusCode();
        first = first with { Version = first.Version + 1 };
        await AssertEmbeddingBackfillLineageAsync(
            connectionString, first.ProductVersionId);
        await SubmitEmbeddingAsync(reviewer, peer);

        using var recallResponse = await reviewer.GetAsync(
            $"/api/v1/tenants/{TenantId}/inventory-products/{first.ProductId}/" +
            "semantic-recall?limit=10");
        using var recall = await ReadJsonAsync(recallResponse);
        var recalledPeer = Assert.Single(recall.RootElement.EnumerateArray(), item =>
            item.GetProperty("productId").GetGuid() == peer.ProductId);
        Assert.True(recalledPeer.GetProperty("similarity").GetDecimal() > 0.5m);

        using var nomination = await CommandAsync(
            reviewer,
            $"/api/v1/tenants/{TenantId}/inventory-products/{first.ProductId}/" +
                "semantic-duplicate-candidates",
            "semantic-duplicate-nomination", first.Version,
            new
            {
                productVersionId = first.ProductVersionId,
                peerProductId = peer.ProductId,
                peerProductVersionId = peer.ProductVersionId,
                reason = "The vector recall suggests the descriptions may name one placement.",
            });
        using var nominated = await ReadJsonAsync(nomination);
        Assert.Equal("SEMANTIC_VECTOR",
            nominated.RootElement.GetProperty("method").GetString());
        Assert.Equal("OPEN", nominated.RootElement.GetProperty("status").GetString());

        using var candidatesResponse = await reviewer.GetAsync(
            $"/api/v1/tenants/{TenantId}/inventory-duplicate-candidates?status=OPEN");
        using var candidates = await ReadJsonAsync(candidatesResponse);
        var semantic = Assert.Single(candidates.RootElement.EnumerateArray(), item =>
            item.GetProperty("method").GetString() == "SEMANTIC_VECTOR");
        var exactCandidate = Assert.Single(candidates.RootElement.EnumerateArray(), item =>
            item.GetProperty("method").GetString() == "EXACT_NAME_LOCATION");
        await ReviewDuplicateAsync(
            reviewer, semantic, "DISMISSED", null,
            "The products have distinct supplier codes and placements.");
        await ReviewDuplicateAsync(
            reviewer, exactCandidate, "CONFIRMED_SAME_IDENTITY", first.ProductId,
            "The exact name, placement type and geography describe the same identity.");

        using var hiddenDuplicateResponse = await reviewer.GetAsync(
            $"/api/v1/tenants/{TenantId}/inventory-products?search=VECTOR-003&pageSize=10");
        using var hiddenDuplicate = await ReadJsonAsync(hiddenDuplicateResponse);
        Assert.Empty(hiddenDuplicate.RootElement.GetProperty("items").EnumerateArray());
        using var retainedResponse = await reviewer.GetAsync(
            $"/api/v1/tenants/{TenantId}/inventory-products/{exact.ProductId}");
        retainedResponse.EnsureSuccessStatusCode();
    }

    private static async Task<PublishedProduct> PublishProductAsync(
        HttpClient importer,
        HttpClient reviewer,
        string key,
        FileFixture fixture)
    {
        using var upload = await UploadAsync(importer, $"{key}-upload", "City Media", fixture);
        using var created = await ReadJsonAsync(upload);
        var importId = created.RootElement.GetProperty("id").GetGuid();
        using var execute = await CommandAsync(
            importer, $"/api/v1/tenants/{TenantId}/inventory-imports/{importId}:execute",
            $"{key}-execute", 1, new { });
        using var extracted = await ReadJsonAsync(execute);
        var candidateId = extracted.RootElement.GetProperty("candidates")[0]
            .GetProperty("id").GetGuid();
        using var review = await CommandAsync(
            reviewer, $"/api/v1/tenants/{TenantId}/inventory-candidates/{candidateId}:review",
            $"{key}-review", 1,
            new { decision = "APPROVE", rejectionReason = (string?)null,
                notes = "Source identity and rate verified.", correctedValues = (object?)null });
        review.EnsureSuccessStatusCode();
        using var publish = await CommandAsync(
            reviewer, $"/api/v1/tenants/{TenantId}/inventory-imports/{importId}:publish",
            $"{key}-publish", 2, new { });
        publish.EnsureSuccessStatusCode();
        var productCode = System.Text.Encoding.UTF8.GetString(fixture.Content)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)[1]
            .Split(',')[0];
        using var pageResponse = await reviewer.GetAsync(
            $"/api/v1/tenants/{TenantId}/inventory-products?search={productCode}&pageSize=10");
        using var page = await ReadJsonAsync(pageResponse);
        var item = Assert.Single(page.RootElement.GetProperty("items").EnumerateArray());
        var productId = item.GetProperty("id").GetGuid();
        using var detailResponse = await reviewer.GetAsync(
            $"/api/v1/tenants/{TenantId}/inventory-products/{productId}");
        using var detail = await ReadJsonAsync(detailResponse);
        return new(productId, detail.RootElement.GetProperty("productVersionId").GetGuid(),
            item.GetProperty("version").GetInt64());
    }

    private static async Task SubmitEmbeddingAsync(
        HttpClient reviewer,
        PublishedProduct product)
    {
        using var response = await CommandAsync(
            reviewer,
            $"/api/v1/tenants/{TenantId}/inventory-products/{product.ProductId}/embedding",
            $"embedding-{product.ProductId:N}", product.Version,
            new { productVersionId = product.ProductVersionId });
        response.EnsureSuccessStatusCode();
    }

    private static async Task AssertEmbeddingBackfillLineageAsync(
        string connectionString,
        Guid productVersionId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*), COUNT(*) FILTER (WHERE is_explicit_backfill)
            FROM commercial.inventory_embedding_jobs
            WHERE tenant_id = @tenant AND product_version_id = @version
            """;
        command.Parameters.AddWithValue("tenant", TenantId);
        command.Parameters.AddWithValue("version", productVersionId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(2, reader.GetInt64(0));
        Assert.Equal(1, reader.GetInt64(1));
    }

    private static async Task ReviewDuplicateAsync(
        HttpClient reviewer,
        JsonElement candidate,
        string decision,
        Guid? canonicalProductId,
        string reason)
    {
        using var response = await CommandAsync(
            reviewer,
            $"/api/v1/tenants/{TenantId}/inventory-duplicate-candidates/" +
                $"{candidate.GetProperty("id").GetGuid()}:review",
            $"duplicate-review-{candidate.GetProperty("id").GetGuid():N}",
            candidate.GetProperty("version").GetInt64(),
            new { decision, canonicalProductId, reason });
        response.EnsureSuccessStatusCode();
    }

    private static async Task<PublishedProduct> AssertAssetRightsGateAsync(
        HttpClient importer,
        PublishedProduct product)
    {
        byte[] expectedContent =
            [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0x00];
        using var body = new MultipartFormDataContent();
        body.Add(new StringContent(product.ProductVersionId.ToString()), "productVersionId");
        body.Add(new StringContent("PRODUCT_IMAGE"), "assetType");
        var file = new ByteArrayContent(expectedContent);
        file.Headers.ContentType = new("image/png");
        body.Add(file, "source", "rights-gated-product.png");
        var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/v1/tenants/{TenantId}/inventory-products/{product.ProductId}/assets")
        {
            Content = body,
        };
        AddCommandHeaders(request, "inventory-asset-upload", product.Version);
        using var upload = await importer.SendAsync(request);
        using var uploaded = await ReadJsonAsync(upload);
        var assetId = uploaded.RootElement.GetProperty("assetId").GetGuid();
        Assert.Equal("UNKNOWN", uploaded.RootElement.GetProperty("rightsStatus").GetString());
        using var blocked = await importer.GetAsync(
            $"/api/v1/tenants/{TenantId}/inventory-assets/{assetId}/content");
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        using var review = await CommandAsync(
            importer, $"/api/v1/tenants/{TenantId}/inventory-assets/{assetId}:review-rights",
            "asset-rights-review", 1,
            new { rightsStatus = "APPROVED",
                rightsBasis = "Supplier source supplied for proposal use.",
                licensedUntil = (string?)null,
                scopeCodes = ProposalAssetRightsScopes,
                territoryCode = "ZA", effectiveOn = "2026-08-29",
                untilRevoked = true, attestorRole = "platform_admin",
                evidenceReference = "inventory-import:supplier-permission",
                evidenceHash = new string('a', 64) });
        review.EnsureSuccessStatusCode();
        using var content = await importer.GetAsync(
            $"/api/v1/tenants/{TenantId}/inventory-assets/{assetId}/content");
        content.EnsureSuccessStatusCode();
        Assert.Equal(expectedContent, await content.Content.ReadAsByteArrayAsync());
        return product with { Version = product.Version + 1 };
    }

    private sealed record PublishedProduct(
        Guid ProductId,
        Guid ProductVersionId,
        long Version);
}

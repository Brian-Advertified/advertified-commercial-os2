using System.Net;
using System.Text;
using System.Text.Json;
using Advertified.Commercial.Infrastructure.Inventory;
using Npgsql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class InventoryAcceptanceTests
{
    [Fact]
    [Trait("Category", "Migration")]
    public async Task ProtectedFilesBecomeReviewedVersionedSearchableInventory()
    {
        await using var postgres = CreatePostgres();
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DisposableDatabaseRoles.ProvisionAsync(connectionString);
        await SeedAsync(connectionString);
        await using var importerFactory = CreateFactory(connectionString, ImporterId);
        await using var reviewerFactory = CreateFactory(connectionString, ReviewerId);
        await using var otherFactory = CreateFactory(connectionString, OtherUserId);
        using var importer = importerFactory.CreateClient();
        using var reviewer = reviewerFactory.CreateClient();
        using var other = otherFactory.CreateClient();

        await AssertProtectionBoundariesAsync(importer);
        await AssertWorkflowDocumentClassesAsync(importer);
        await AssertCandidatePagingAsync(importer);
        await AssertRejectedBlockingCandidateDoesNotBlockPublicationAsync(importer, reviewer);
        var imported = await CreateAndExecuteAsync(importer, CsvFixture());
        var importId = imported.GetProperty("id").GetGuid();
        await AssertExtractionCheckpointAsync(connectionString, importId);
        var candidate = imported.GetProperty("candidates")[0];
        var candidateId = candidate.GetProperty("id").GetGuid();

        using var selfReview = await CommandAsync(
            importer, $"/api/v1/tenants/{TenantId}/inventory-candidates/{candidateId}:review",
            "inventory-self-review", 1,
            new { decision = "APPROVE", rejectionReason = (string?)null,
                notes = "Source checked.", correctedValues = (object?)null });
        await AssertProblemAsync(selfReview, HttpStatusCode.Forbidden, "APPROVAL_REQUIRED");

        using var review = await CommandAsync(
            reviewer, $"/api/v1/tenants/{TenantId}/inventory-candidates/{candidateId}:review",
            "inventory-review", 1,
            new { decision = "APPROVE", rejectionReason = (string?)null,
                notes = "Coordinates and rate match the source.", correctedValues = (object?)null });
        using var reviewed = await ReadJsonAsync(review);
        Assert.Equal("APPROVED", reviewed.RootElement.GetProperty("status").GetString());

        using var publish = await CommandAsync(
            reviewer, $"/api/v1/tenants/{TenantId}/inventory-imports/{importId}:publish",
            "inventory-publish", 2, new { });
        using var published = await ReadJsonAsync(publish);
        Assert.Equal("COMPLETED", published.RootElement.GetProperty("status").GetString());

        await AssertSearchDetailAndScaleAsync(
            connectionString, reviewer, published.RootElement, candidateId);
        using var crossTenant = await other.GetAsync(
            $"/api/v1/tenants/{OtherTenantId}/inventory-imports/{importId}");
        await AssertProblemAsync(crossTenant, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");
        await AssertImmutableLineageAsync(connectionString, candidateId);
    }

    private static async Task AssertProtectionBoundariesAsync(HttpClient importer)
    {
        var oversizedBytes = new byte[InventoryProtectionOptions.MaximumSupportedSourceBytes + 1];
        Encoding.UTF8.GetBytes("product_code,name\n").CopyTo(oversizedBytes, 0);
        var oversized = new FileFixture(
            "CSV", "oversized.csv", "text/csv", oversizedBytes);
        using var oversizedResponse = await UploadAsync(
            importer, "inventory-size-boundary", "Boundary Supplier", oversized);
        await AssertProblemAsync(
            oversizedResponse, HttpStatusCode.BadRequest, "VALIDATION_FAILED");

        var mismatch = new FileFixture(
            "PNG", "not-an-image.csv", "text/csv",
            [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);
        using var mismatchResponse = await UploadAsync(
            importer, "inventory-type-mismatch", "Boundary Supplier", mismatch);
        await AssertProblemAsync(mismatchResponse, HttpStatusCode.BadRequest, "VALIDATION_FAILED");

        var unsafeSource = new FileFixture(
            "CSV", "unsafe.csv", "text/csv", Encoding.ASCII.GetBytes(
                "product_code,name\nEICAR-STANDARD-ANTIVIRUS-TEST-FILE,unsafe\n"));
        using var unsafeResponse = await UploadAsync(
            importer, "inventory-malware", "Boundary Supplier", unsafeSource);
        using var unsafeJson = await ReadJsonAsync(unsafeResponse);
        Assert.Equal("FAILED", unsafeJson.RootElement.GetProperty("status").GetString());
        Assert.Equal("INFECTED", unsafeJson.RootElement.GetProperty("scanStatus").GetString());
        Assert.Equal("MALWARE_DETECTED", unsafeJson.RootElement.GetProperty("failureCode").GetString());
    }

    private static async Task AssertWorkflowDocumentClassesAsync(HttpClient importer)
    {
        var ordinal = 0;
        foreach (var fixture in CorpusFixtures())
        {
            ordinal++;
            using var upload = await UploadAsync(
                importer, $"inventory-corpus-{ordinal}", "Held Out Supplier", fixture);
            using var created = await ReadJsonAsync(upload);
            Assert.Equal(fixture.DocumentClass,
                created.RootElement.GetProperty("documentClass").GetString());
            Assert.Equal("CLEAN", created.RootElement.GetProperty("scanStatus").GetString());
            var importId = created.RootElement.GetProperty("id").GetGuid();
            using var execute = await CommandAsync(
                importer, $"/api/v1/tenants/{TenantId}/inventory-imports/{importId}:execute",
                $"inventory-corpus-execute-{ordinal}", 1, new { });
            using var executed = await ReadJsonAsync(execute);
            AssertCorpusExtraction(executed.RootElement, fixture);
        }
    }

    private static void AssertCorpusExtraction(JsonElement record, FileFixture fixture)
    {
        var candidates = record.GetProperty("candidates");
        Assert.True(candidates.GetArrayLength() >= 1);
        var candidate = candidates[0];
        if (fixture.DocumentClass is "PNG" or "JPEG")
        {
            Assert.Contains(candidate.GetProperty("validation").EnumerateArray(),
                issue => issue.GetProperty("isBlocking").GetBoolean());
            return;
        }
        var values = candidate.GetProperty("values");
        AssertCriticalText(values, "productCode");
        AssertCriticalText(values, "name");
        AssertCriticalText(values, "channel");
        AssertCriticalText(values, "geography");
        AssertCriticalText(values, "rateType");
        AssertCriticalText(values, "currency");
        Assert.True(values.GetProperty("rateAmountMinor").GetInt64() > 0);
        Assert.DoesNotContain(candidate.GetProperty("validation").EnumerateArray(),
            issue => issue.GetProperty("isBlocking").GetBoolean());
    }

    private static void AssertCriticalText(JsonElement values, string name) =>
        Assert.False(string.IsNullOrWhiteSpace(values.GetProperty(name).GetString()));

    private static async Task AssertCandidatePagingAsync(HttpClient importer)
    {
        using var upload = await UploadAsync(
            importer, "inventory-candidate-page-upload", "Paging Supplier",
            CandidatePageFixture());
        using var created = await ReadJsonAsync(upload);
        var importId = created.RootElement.GetProperty("id").GetGuid();
        using var execute = await CommandAsync(
            importer, $"/api/v1/tenants/{TenantId}/inventory-imports/{importId}:execute",
            "inventory-candidate-page-execute", 1, new { });
        execute.EnsureSuccessStatusCode();

        using var firstResponse = await importer.GetAsync(
            $"/api/v1/tenants/{TenantId}/inventory-imports/{importId}?pageSize=2");
        using var first = await ReadJsonAsync(firstResponse);
        Assert.Equal(3, first.RootElement.GetProperty("candidateCounts")
            .GetProperty("total").GetInt32());
        Assert.Equal(2, first.RootElement.GetProperty("candidates").GetArrayLength());
        var firstId = first.RootElement.GetProperty("candidates")[0]
            .GetProperty("id").GetGuid();
        var cursor = Uri.EscapeDataString(first.RootElement
            .GetProperty("nextCandidateCursor").GetString()!);

        using var secondResponse = await importer.GetAsync(
            $"/api/v1/tenants/{TenantId}/inventory-imports/{importId}?pageSize=2&cursor={cursor}");
        using var second = await ReadJsonAsync(secondResponse);
        Assert.Single(second.RootElement.GetProperty("candidates").EnumerateArray());
        Assert.NotEqual(firstId, second.RootElement.GetProperty("candidates")[0]
            .GetProperty("id").GetGuid());
        Assert.Equal(JsonValueKind.Null,
            second.RootElement.GetProperty("nextCandidateCursor").ValueKind);

        using var invalid = await importer.GetAsync(
            $"/api/v1/tenants/{TenantId}/inventory-imports/{importId}?pageSize=101");
        await AssertProblemAsync(invalid, HttpStatusCode.BadRequest, "VALIDATION_FAILED");
    }

    private static async Task<JsonElement> CreateAndExecuteAsync(
        HttpClient importer,
        FileFixture fixture)
    {
        using var upload = await UploadAsync(
            importer, "inventory-main-upload", "City Media", fixture);
        using var created = await ReadJsonAsync(upload);
        var importId = created.RootElement.GetProperty("id").GetGuid();
        using var execute = await CommandAsync(
            importer, $"/api/v1/tenants/{TenantId}/inventory-imports/{importId}:execute",
            "inventory-main-execute", 1, new { });
        using var executed = await ReadJsonAsync(execute);
        return executed.RootElement.Clone();
    }

    private static async Task AssertRejectedBlockingCandidateDoesNotBlockPublicationAsync(
        HttpClient importer,
        HttpClient reviewer)
    {
        using var upload = await UploadAsync(
            importer, "inventory-mixed-review-upload", "Mixed Review Supplier",
            RejectedBlockingCandidateFixture());
        using var created = await ReadJsonAsync(upload);
        var importId = created.RootElement.GetProperty("id").GetGuid();
        using var execute = await CommandAsync(
            importer, $"/api/v1/tenants/{TenantId}/inventory-imports/{importId}:execute",
            "inventory-mixed-review-execute", 1, new { });
        using var extracted = await ReadJsonAsync(execute);
        var candidates = extracted.RootElement.GetProperty("candidates")
            .EnumerateArray().ToArray();
        var valid = candidates.Single(item => !item.GetProperty("validation")
            .EnumerateArray().Any(issue => issue.GetProperty("isBlocking").GetBoolean()));
        var invalid = candidates.Single(item => item.GetProperty("validation")
            .EnumerateArray().Any(issue => issue.GetProperty("isBlocking").GetBoolean()));
        using var approved = await CommandAsync(
            reviewer, $"/api/v1/tenants/{TenantId}/inventory-candidates/" +
                $"{valid.GetProperty("id").GetGuid()}:review",
            "inventory-mixed-review-approve", 1,
            new { decision = "APPROVE", rejectionReason = (string?)null,
                notes = "Valid row confirmed.", correctedValues = (object?)null });
        approved.EnsureSuccessStatusCode();
        using var rejected = await CommandAsync(
            reviewer, $"/api/v1/tenants/{TenantId}/inventory-candidates/" +
                $"{invalid.GetProperty("id").GetGuid()}:review",
            "inventory-mixed-review-reject", 1,
            new { decision = "REJECT", rejectionReason = "MISSING_INFO",
                notes = "Coordinates were not supplied.", correctedValues = (object?)null });
        rejected.EnsureSuccessStatusCode();
        using var reviewedResponse = await reviewer.GetAsync(
            $"/api/v1/tenants/{TenantId}/inventory-imports/{importId}");
        using var reviewed = await ReadJsonAsync(reviewedResponse);
        var counts = reviewed.RootElement.GetProperty("candidateCounts");
        Assert.Equal(1, counts.GetProperty("approved").GetInt32());
        Assert.Equal(1, counts.GetProperty("rejected").GetInt32());
        Assert.Equal(0, counts.GetProperty("blocking").GetInt32());
        using var publish = await CommandAsync(
            reviewer, $"/api/v1/tenants/{TenantId}/inventory-imports/{importId}:publish",
            "inventory-mixed-review-publish", 2, new { });
        using var published = await ReadJsonAsync(publish);
        Assert.Equal("COMPLETED", published.RootElement.GetProperty("status").GetString());
    }

    private static async Task AssertSearchDetailAndScaleAsync(
        string connectionString,
        HttpClient reviewer,
        JsonElement published,
        Guid candidateId)
    {
        var supplierId = published.GetProperty("supplierId").GetGuid();
        var importId = published.GetProperty("id").GetGuid();
        using var publishedResponse = await reviewer.GetAsync(
            $"/api/v1/tenants/{TenantId}/inventory-products?pageSize=1&search=OOH-001");
        using var publishedPage = await ReadJsonAsync(publishedResponse);
        var publishedProductId = publishedPage.RootElement.GetProperty("items")[0]
            .GetProperty("id").GetGuid();
        using var publishedDetailResponse = await reviewer.GetAsync(
            $"/api/v1/tenants/{TenantId}/inventory-products/{publishedProductId}");
        using var publishedDetail = await ReadJsonAsync(publishedDetailResponse);
        var profile = publishedDetail.RootElement.GetProperty("audienceProfile");
        Assert.Equal("English", profile.GetProperty("spokenLanguages")[0]
            .GetProperty("label").GetString());
        Assert.Equal(80m, profile.GetProperty("spokenLanguages")[0]
            .GetProperty("sharePercent").GetDecimal());
        Assert.Equal("TGI SEM", profile.GetProperty("taxonomyName").GetString());
        Assert.Equal("Fixture audience study",
            profile.GetProperty("measurementSource").GetString());
        var measurements = profile.GetProperty("measurements");
        Assert.Equal(2, measurements.GetArrayLength());
        Assert.Contains(measurements.EnumerateArray(), item =>
            item.GetProperty("metricType").GetString() == "REACH" &&
            item.GetProperty("value").GetDecimal() == 125_000m &&
            item.GetProperty("unit").GetString() == "PEOPLE");
        Assert.Contains(measurements.EnumerateArray(), item =>
            item.GetProperty("metricType").GetString() == "FOOTFALL" &&
            item.GetProperty("value").GetDecimal() == 42_000m);
        await SeedScaleProductsAsync(
            connectionString, supplierId, importId, candidateId);
        using var firstResponse = await reviewer.GetAsync(
            $"/api/v1/tenants/{TenantId}/inventory-products?pageSize=2&channel=OOH");
        using var first = await ReadJsonAsync(firstResponse);
        Assert.Equal(InventoryProtectionOptions.MaximumSupportedSourceBytes,
            first.RootElement.GetProperty("maximumSourceBytes").GetInt32());
        Assert.Equal(2, first.RootElement.GetProperty("items").GetArrayLength());
        var cursor = Uri.EscapeDataString(
            first.RootElement.GetProperty("nextCursor").GetString()!);
        using var secondResponse = await reviewer.GetAsync(
            $"/api/v1/tenants/{TenantId}/inventory-products?pageSize=2&channel=OOH&cursor={cursor}");
        using var second = await ReadJsonAsync(secondResponse);
        Assert.NotEqual(
            first.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid(),
            second.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid());
        var productId = first.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid();
        using var detailResponse = await reviewer.GetAsync(
            $"/api/v1/tenants/{TenantId}/inventory-products/{productId}");
        using var detail = await ReadJsonAsync(detailResponse);
        Assert.Equal("HUMAN_VERIFIED", detail.RootElement.GetProperty("product")
            .GetProperty("verification").GetString());
    }

    private static async Task SeedScaleProductsAsync(
        string connectionString, Guid supplierId, Guid importId, Guid candidateId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            WITH products AS (
                INSERT INTO commercial.inventory_products (
                    id, tenant_id, supplier_id, supplier_product_code, status_code,
                    version, created_at_utc, updated_at_utc)
                SELECT md5('inventory-product-' || n)::uuid, $1, $2, 'SCALE-' || lpad(n::text, 5, '0'),
                    'ACTIVE', 1, $6, $6 FROM generate_series(1, 10001) n
                RETURNING id, supplier_product_code
            ), versions AS (
                INSERT INTO commercial.inventory_product_versions (
                    id, tenant_id, product_id, version_number, name, channel_code,
                    product_type_code, geography, verification_code, source_import_id,
                    source_candidate_id, published_by, published_at_utc)
                SELECT md5('inventory-version-' || supplier_product_code)::uuid, $1, id, 1,
                    'Scale Site ' || supplier_product_code, 'OOH', 'OOH_SITE', 'Gauteng',
                    'HUMAN_VERIFIED', $3, $4, $5, $6 FROM products
                RETURNING id, product_id
            )
            SELECT count(*)::integer FROM versions;
            """, connection);
        command.Parameters.AddWithValue(TenantId);
        command.Parameters.AddWithValue(supplierId);
        command.Parameters.AddWithValue(importId);
        command.Parameters.AddWithValue(candidateId);
        command.Parameters.AddWithValue(ReviewerId);
        command.Parameters.AddWithValue(Now);
        var scalar = await command.ExecuteScalarAsync();
        var count = scalar is int value
            ? value : throw new InvalidOperationException("The scale count was unavailable.");
        Assert.True(count > 10_000);
        await using var update = new NpgsqlCommand(
            """
            UPDATE commercial.inventory_products
            SET current_version_id = md5('inventory-version-' || supplier_product_code)::uuid
            WHERE tenant_id = $1 AND supplier_product_code LIKE 'SCALE-%'
            """, connection);
        update.Parameters.AddWithValue(TenantId);
        Assert.Equal(10_001, await update.ExecuteNonQueryAsync());
    }

    private static async Task AssertImmutableLineageAsync(
        string connectionString,
        Guid candidateId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "UPDATE commercial.inventory_review_decisions SET notes = 'changed' " +
            "WHERE candidate_id = $1", connection);
        command.Parameters.AddWithValue(candidateId);
        var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.RaiseException, exception.SqlState);
    }

    private static async Task AssertExtractionCheckpointAsync(
        string connectionString,
        Guid importId)
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
        Assert.Equal("advertified-test-fixture", row.GetString(0));
        Assert.Equal("1.0.0", row.GetString(1));
        Assert.Equal(InventoryExtractionOptions.CurrentSchemaVersion, row.GetString(2));
        Assert.Equal(64, row.GetInt32(3));
        Assert.Equal(64, row.GetInt32(4));
        Assert.Equal(64, row.GetInt32(5));
        using var canonical = JsonDocument.Parse(row.GetString(6));
        Assert.Equal(InventoryExtractionOptions.CurrentSchemaVersion,
            canonical.RootElement.GetProperty("schemaVersion").GetString());
        Assert.NotEmpty(canonical.RootElement.GetProperty("rows").EnumerateArray());
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

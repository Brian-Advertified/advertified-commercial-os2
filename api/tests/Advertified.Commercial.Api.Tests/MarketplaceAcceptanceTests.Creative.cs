using System.Net;
using System.Text.Json;
using Npgsql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class MarketplaceAcceptanceTests
{
    private static readonly byte[] CreativePng =
        [137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82];

    private static async Task<long> AssertCreativeProductionReadinessAsync(
        HttpClient buyer,
        HttpClient client,
        HttpClient supplier,
        HttpClient other,
        Guid campaignId,
        Guid bookingId,
        long campaignVersion,
        string connectionString)
    {
        using var missing = await RawCommandAsync(
            buyer, BuyerTenantId, $"campaigns/{campaignId}:request-creative",
            "creative-request-missing", campaignVersion,
            new { requirements = Array.Empty<object>(), reason = "No booking coverage." });
        await AssertProblemAsync(
            missing, HttpStatusCode.Conflict, "CREATIVE_READINESS_BLOCKED");

        using var requested = await CommandAsync(
            buyer, BuyerTenantId, $"campaigns/{campaignId}:request-creative",
            "creative-request", campaignVersion,
            new
            {
                requirements = new[]
                {
                    new
                    {
                        bookingId,
                        formatCode = "OOH_1920X1080",
                        width = 1920,
                        height = 1080,
                        requiredMediaType = "image/png",
                        maximumBytes = 1_000_000,
                        instructions = "Supply final approved artwork for this booked OOH placement.",
                    },
                },
                reason = "Every confirmed booking now has an exact production requirement.",
            });
        Assert.Equal("CREATIVE_PENDING", requested.RootElement.GetProperty("status").GetString());
        var requestVersion = requested.RootElement.GetProperty("version").GetInt64();
        var requirementId = await GetRequirementIdAsync(buyer, campaignId);

        using var first = await CommandAsync(
            buyer, BuyerTenantId, $"campaigns/{campaignId}/creative",
            "creative-upload-v1", null,
            AssetBody(requestVersion, requirementId, "Initial approved campaign copy."));
        var assetId = first.RootElement.GetProperty("id").GetGuid();
        Assert.Equal(1, first.RootElement.GetProperty("version").GetInt64());
        Assert.Equal(JsonValueKind.Null, first.RootElement.GetProperty("currentVersion")
            .GetProperty("brandReview").ValueKind);

        await AssertSupplierProjectionAsync(supplier, other, assetId);
        using var buyerSupplierReview = await RawCommandAsync(
            buyer, BuyerTenantId, $"creative-assets/{assetId}:supplier-review",
            "creative-wrong-supplier-review", 1,
            SupplierReviewBody(true, "Buyer cannot act for supplier."));
        await AssertProblemAsync(
            buyerSupplierReview, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");

        using var rejected = await CommandAsync(
            client, BuyerTenantId,
            $"campaigns/{campaignId}/creative/{assetId}:brand-review",
            "creative-brand-reject-v1", 1,
            new
            {
                approved = false,
                rightsStatus = "RESTRICTED",
                evidenceReference = "brand-review:v1",
                reason = "The first file needs a corrected rights-cleared version.",
            });
        Assert.Equal("REJECTED", rejected.RootElement.GetProperty("currentVersion")
            .GetProperty("brandReview").GetProperty("decision").GetString());

        using var replacement = await CommandAsync(
            buyer, BuyerTenantId,
            $"campaigns/{campaignId}/creative/{assetId}:upload-version",
            "creative-upload-v2", 2,
            new
            {
                approvedCopy = "Corrected rights-cleared campaign copy.",
                file = CreativeFile("creative-v2.png"),
            });
        Assert.Equal(3, replacement.RootElement.GetProperty("version").GetInt64());
        Assert.Equal(JsonValueKind.Null, replacement.RootElement.GetProperty("currentVersion")
            .GetProperty("brandReview").ValueKind);

        using var staleSupplier = await RawCommandAsync(
            supplier, SupplierTenantId, $"creative-assets/{assetId}:supplier-review",
            "creative-supplier-stale", 1,
            SupplierReviewBody(true, "Stale version must not be approved."));
        await AssertProblemAsync(staleSupplier, HttpStatusCode.Conflict, "VERSION_CONFLICT");
        using var premature = await RawCommandAsync(
            client, BuyerTenantId, $"campaigns/{campaignId}:approve-creative",
            "creative-campaign-premature", requestVersion,
            new { reason = "Reviews are incomplete." });
        await AssertProblemAsync(
            premature, HttpStatusCode.Conflict, "CREATIVE_READINESS_BLOCKED");

        using var brandApproved = await CommandAsync(
            client, BuyerTenantId,
            $"campaigns/{campaignId}/creative/{assetId}:brand-review",
            "creative-brand-approve-v2", 3,
            new
            {
                approved = true,
                rightsStatus = "APPROVED",
                evidenceReference = "brand-legal-rights:v2",
                reason = "Brand, legal, copy and usage rights are approved for this exact file.",
            });
        Assert.Equal("APPROVED", brandApproved.RootElement.GetProperty("currentVersion")
            .GetProperty("brandReview").GetProperty("rightsStatus").GetString());
        using var supplierApproved = await CommandAsync(
            supplier, SupplierTenantId, $"creative-assets/{assetId}:supplier-review",
            "creative-supplier-approve-v2", 4,
            SupplierReviewBody(true, "Exact file meets the booked technical specification."));
        Assert.Equal("APPROVED", supplierApproved.RootElement
            .GetProperty("supplierDecision").GetString());
        await AssertReviewCannotAdvanceAssetTwiceAsync(connectionString, assetId);

        using var buyerApproval = await RawCommandAsync(
            buyer, BuyerTenantId, $"campaigns/{campaignId}:approve-creative",
            "creative-campaign-wrong-approver", requestVersion,
            new { reason = "Agency operator cannot self-approve client creative." });
        await AssertProblemAsync(buyerApproval, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");
        using var ready = await CommandAsync(
            client, BuyerTenantId, $"campaigns/{campaignId}:approve-creative",
            "creative-campaign-ready", requestVersion,
            new { reason = "Every current version has both exact human approvals." });
        Assert.Equal("READY", ready.RootElement.GetProperty("status").GetString());
        Assert.Equal(ClientUserId, ready.RootElement.GetProperty("creativeApprovedBy").GetGuid());
        using var afterReady = await RawCommandAsync(
            buyer, BuyerTenantId,
            $"campaigns/{campaignId}/creative/{assetId}:upload-version",
            "creative-upload-after-ready", 5,
            new { approvedCopy = "Not allowed.", file = CreativeFile("late.png") });
        await AssertProblemAsync(
            afterReady, HttpStatusCode.Conflict, "CREATIVE_READINESS_BLOCKED");
        await AssertCreativeEvidenceAsync(connectionString, campaignId, assetId);
        return ready.RootElement.GetProperty("version").GetInt64();
    }

    private static async Task<Guid> GetRequirementIdAsync(HttpClient buyer, Guid campaignId)
    {
        using var detail = await ReadAsync(buyer, BuyerTenantId, $"campaigns/{campaignId}");
        var creative = detail.RootElement.GetProperty("creative");
        Assert.False(creative.GetProperty("readyForApproval").GetBoolean());
        return Assert.Single(creative.GetProperty("requirements").EnumerateArray())
            .GetProperty("id").GetGuid();
    }

    private static object AssetBody(long campaignVersion, Guid requirementId, string copy) => new
    {
        campaignVersion,
        requirementId,
        approvedCopy = copy,
        file = CreativeFile("creative-v1.png"),
    };

    private static object CreativeFile(string name) => new
    {
        fileName = name,
        mediaType = "image/png",
        content = CreativePng,
    };

    private static object SupplierReviewBody(bool approved, string reason) => new
    {
        approved,
        evidenceReference = "supplier-technical:exact-file",
        reason,
    };

    private static async Task AssertSupplierProjectionAsync(
        HttpClient supplier, HttpClient other, Guid assetId)
    {
        using var response = await ReadAsync(
            supplier, SupplierTenantId, $"creative-assets/{assetId}");
        var text = response.RootElement.GetRawText();
        Assert.DoesNotContain("approvedCopy", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("commercialSnapshot", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("objectKey", text, StringComparison.OrdinalIgnoreCase);
        using var unrelated = await other.GetAsync(
            $"/api/v1/tenants/{OtherTenantId}/creative-assets/{assetId}");
        await AssertProblemAsync(unrelated, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");
    }

    private static async Task AssertReviewCannotAdvanceAssetTwiceAsync(
        string connectionString, Guid assetId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var role = new NpgsqlCommand(
            "SET LOCAL ROLE advertified_app", connection, transaction);
        await role.ExecuteNonQueryAsync();
        await using var user = new NpgsqlCommand(
            "SELECT set_config('advertified.user_id', $1, true)", connection, transaction);
        user.Parameters.AddWithValue(SupplierUserId.ToString());
        await user.ExecuteNonQueryAsync();
        await using var tenant = new NpgsqlCommand(
            "SELECT set_config('advertified.tenant_id', $1, true)", connection, transaction);
        tenant.Parameters.AddWithValue(SupplierTenantId.ToString());
        await tenant.ExecuteNonQueryAsync();
        await using var repeat = new NpgsqlCommand(
            "UPDATE commercial.creative_assets asset " +
            "SET version = asset.version + 1, updated_at_utc = (SELECT max(reviewed_at_utc) " +
            "FROM commercial.creative_asset_reviews WHERE asset_id = asset.id) " +
            "WHERE asset.id = $1", connection, transaction);
        repeat.Parameters.AddWithValue(assetId);
        Assert.Equal(PostgresErrorCodes.RaiseException,
            (await Assert.ThrowsAsync<PostgresException>(repeat.ExecuteNonQueryAsync)).SqlState);
        await transaction.RollbackAsync();
    }

    private static async Task AssertCreativeEvidenceAsync(
        string connectionString, Guid campaignId, Guid assetId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        Assert.Equal(2, await CountAsync(connection,
            "SELECT count(*)::integer FROM commercial.creative_asset_versions"));
        Assert.Equal(3, await CountAsync(connection,
            "SELECT count(*)::integer FROM commercial.creative_asset_reviews"));
        Assert.Equal(7, await CountAsync(connection,
            "SELECT count(*)::integer FROM commercial.audit_events " +
            "WHERE action_code LIKE 'creative_asset.%' OR action_code IN " +
            "('campaign.creative_requested','campaign.creative_approved')"));
        await using var mutate = new NpgsqlCommand(
            "UPDATE commercial.creative_asset_versions SET approved_copy = 'changed' " +
            "WHERE asset_id = $1", connection);
        mutate.Parameters.AddWithValue(assetId);
        Assert.Equal(PostgresErrorCodes.RaiseException,
            (await Assert.ThrowsAsync<PostgresException>(mutate.ExecuteNonQueryAsync)).SqlState);
        await using var advance = new NpgsqlCommand(
            "UPDATE commercial.campaigns SET status_code = 'LIVE' WHERE id = $1", connection);
        advance.Parameters.AddWithValue(campaignId);
        Assert.Equal(PostgresErrorCodes.RaiseException,
            (await Assert.ThrowsAsync<PostgresException>(advance.ExecuteNonQueryAsync)).SqlState);
    }
}

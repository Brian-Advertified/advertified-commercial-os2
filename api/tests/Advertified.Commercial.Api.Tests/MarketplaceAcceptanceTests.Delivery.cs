using System.Net;
using System.Text.Json;
using Npgsql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class MarketplaceAcceptanceTests
{
    private static async Task AssertCampaignDeliveryProofAsync(
        HttpClient buyer,
        HttpClient client,
        HttpClient supplier,
        HttpClient other,
        Guid campaignId,
        Guid bookingId,
        long readyVersion,
        AdjustableMarketplaceClock clock,
        string connectionString)
    {
        using var wrongStarter = await RawCommandAsync(
            other, OtherTenantId, $"campaigns/{campaignId}:start",
            "delivery-start-wrong-tenant", readyVersion,
            new { reason = "An unrelated supplier cannot start a campaign." });
        await AssertProblemAsync(wrongStarter, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");

        clock.Set(new DateTimeOffset(2026, 8, 31, 8, 0, 0, TimeSpan.Zero));
        using var earlyStart = await RawCommandAsync(
            buyer, BuyerTenantId, $"campaigns/{campaignId}:start",
            "delivery-start-early", readyVersion,
            new { reason = "The booked flight has not started." });
        await AssertProblemAsync(
            earlyStart, HttpStatusCode.Conflict, "CAMPAIGN_DELIVERY_BLOCKED");
        clock.Set(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
        using var lateStart = await RawCommandAsync(
            buyer, BuyerTenantId, $"campaigns/{campaignId}:start",
            "delivery-start-late", readyVersion,
            new { reason = "The booked flight has already closed." });
        await AssertProblemAsync(
            lateStart, HttpStatusCode.Conflict, "CAMPAIGN_DELIVERY_BLOCKED");
        clock.Set(InitialTime);

        using var live = await CommandAsync(
            buyer, BuyerTenantId, $"campaigns/{campaignId}:start",
            "delivery-start", readyVersion,
            new { reason = "The booked flight has started and delivery dependencies are healthy." });
        Assert.Equal("LIVE", live.RootElement.GetProperty("status").GetString());
        Assert.Equal(BuyerUserId, live.RootElement.GetProperty("startedBy").GetGuid());
        var liveVersion = live.RootElement.GetProperty("version").GetInt64();
        await AssertSupplierProofRequestBoundariesBeforeCompletionAsync(
            buyer, supplier, other);
        await AssertPerformanceBlockedBeforeCompletionAsync(buyer, campaignId);

        using var earlyCompletion = await RawCommandAsync(
            buyer, BuyerTenantId, $"campaigns/{campaignId}:complete",
            "delivery-complete-early", liveVersion,
            new
            {
                completionReason = "The booked delivery window is still open.",
                proofRequestReason = "Proof cannot be requested before delivery closes.",
            });
        await AssertProblemAsync(
            earlyCompletion, HttpStatusCode.Conflict, "CAMPAIGN_DELIVERY_BLOCKED");
        using var earlyProof = await RawCommandAsync(
            supplier, SupplierTenantId, $"campaigns/{campaignId}/delivery-proofs",
            "delivery-proof-before-request", null,
            ProofBody(bookingId, "2026-09-10T08:00:00Z", CreativePng, "before.png"));
        await AssertProblemAsync(earlyProof, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");

        clock.Advance(TimeSpan.FromDays(30));
        using var completed = await CommandAsync(
            buyer, BuyerTenantId, $"campaigns/{campaignId}:complete",
            "delivery-complete", liveVersion,
            new
            {
                completionReason = "The confirmed booking flight has closed.",
                proofRequestReason = "Request exact retained supplier delivery evidence.",
            });
        Assert.Equal("COMPLETED", completed.RootElement.GetProperty("status").GetString());
        Assert.Equal(BuyerUserId, completed.RootElement.GetProperty("proofRequestedBy").GetGuid());
        await AssertSupplierProofRequestAsync(
            supplier, campaignId, bookingId, null, null);
        await AssertSupplierProofRequestDatabaseBoundaryAsync(connectionString);

        using var buyerSubmission = await RawCommandAsync(
            buyer, BuyerTenantId, $"campaigns/{campaignId}/delivery-proofs",
            "delivery-proof-buyer-submit", null,
            ProofBody(bookingId, "2026-09-20T08:00:00Z", CreativePng, "buyer.png"));
        await AssertProblemAsync(
            buyerSubmission, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");
        using var unrelatedSubmission = await RawCommandAsync(
            other, OtherTenantId, $"campaigns/{campaignId}/delivery-proofs",
            "delivery-proof-wrong-supplier", null,
            ProofBody(bookingId, "2026-09-20T08:00:00Z", CreativePng, "other.png"));
        await AssertProblemAsync(
            unrelatedSubmission, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");
        using var outsideFlight = await RawCommandAsync(
            supplier, SupplierTenantId, $"campaigns/{campaignId}/delivery-proofs",
            "delivery-proof-outside-flight", null,
            ProofBody(bookingId, "2026-10-01T08:00:00Z", CreativePng, "late.png"));
        await AssertProblemAsync(
            outsideFlight, HttpStatusCode.Conflict, "DELIVERY_PROOF_BLOCKED");
        using var wrongFile = await RawCommandAsync(
            supplier, SupplierTenantId, $"campaigns/{campaignId}/delivery-proofs",
            "delivery-proof-wrong-file", null,
            ProofBody(bookingId, "2026-09-20T08:00:00Z", "%PDF"u8.ToArray(), "wrong.png"));
        await AssertProblemAsync(
            wrongFile, HttpStatusCode.BadRequest, "DELIVERY_PROOF_FILE_REJECTED");

        using var first = await CommandAsync(
            supplier, SupplierTenantId, $"campaigns/{campaignId}/delivery-proofs",
            "delivery-proof-first", null,
            ProofBody(bookingId, "2026-09-20T08:00:00Z", CreativePng, "delivery.png"));
        var firstProofId = first.RootElement.GetProperty("id").GetGuid();
        Assert.Equal("SUBMITTED", first.RootElement.GetProperty("status").GetString());
        Assert.True(first.RootElement.GetProperty("signatureValidated").GetBoolean());
        Assert.Equal("CLEAN", first.RootElement.GetProperty("malwareScanStatus").GetString());
        Assert.DoesNotContain("objectKey", first.RootElement.GetRawText(),
            StringComparison.OrdinalIgnoreCase);
        await AssertSupplierProofRequestAsync(
            supplier, campaignId, bookingId, firstProofId, "SUBMITTED");
        using var activeReplacement = await RawCommandAsync(
            supplier, SupplierTenantId, $"campaigns/{campaignId}/delivery-proofs",
            "delivery-proof-active-replacement", null,
            ProofBody(
                bookingId, "2026-09-20T08:00:00Z",
                CreativePng.Append((byte)2).ToArray(), "active-replacement.png"));
        await AssertProblemAsync(
            activeReplacement, HttpStatusCode.Conflict, "DELIVERY_PROOF_BLOCKED");
        using var duplicateContent = await RawCommandAsync(
            supplier, SupplierTenantId, $"campaigns/{campaignId}/delivery-proofs",
            "delivery-proof-duplicate-content", null,
            ProofBody(bookingId, "2026-09-20T08:00:00Z", CreativePng, "duplicate.png"));
        await AssertProblemAsync(
            duplicateContent, HttpStatusCode.Conflict, "DELIVERY_PROOF_BLOCKED");
        await AssertDeliveryProofVisibilityAsync(
            buyer, supplier, other, campaignId, firstProofId);
        await AssertPendingDeliveryTaskAsync(buyer, firstProofId);
        await AssertSupplierCannotSelfReviewAtDatabaseAsync(connectionString, firstProofId);

        using var supplierReview = await RawCommandAsync(
            supplier, SupplierTenantId,
            $"campaigns/{campaignId}/delivery-proofs/{firstProofId}:review",
            "delivery-proof-supplier-review", 1,
            new { approved = true, reason = "Supplier cannot review its own proof." });
        await AssertProblemAsync(
            supplierReview, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");
        using var rejected = await CommandAsync(
            buyer, BuyerTenantId,
            $"campaigns/{campaignId}/delivery-proofs/{firstProofId}:review",
            "delivery-proof-reject", 1,
            new { approved = false, reason = "The image does not clearly identify the site." });
        Assert.Equal("REJECTED", rejected.RootElement.GetProperty("status").GetString());
        await AssertSupplierProofRequestAsync(
            supplier, campaignId, bookingId, firstProofId, "REJECTED");
        using var repeatedReview = await RawCommandAsync(
            buyer, BuyerTenantId,
            $"campaigns/{campaignId}/delivery-proofs/{firstProofId}:review",
            "delivery-proof-repeat-review", 2,
            new { approved = true, reason = "A retained rejection cannot be overwritten." });
        await AssertProblemAsync(
            repeatedReview, HttpStatusCode.Conflict, "DELIVERY_PROOF_BLOCKED");

        var replacementBytes = CreativePng.Append((byte)1).ToArray();
        using var replacement = await CommandAsync(
            supplier, SupplierTenantId, $"campaigns/{campaignId}/delivery-proofs",
            "delivery-proof-replacement", null,
            ProofBody(
                bookingId, "2026-09-21T08:00:00Z", replacementBytes, "replacement.png"));
        var replacementId = replacement.RootElement.GetProperty("id").GetGuid();
        Assert.NotEqual(firstProofId, replacementId);
        await AssertSupplierProofRequestAsync(
            supplier, campaignId, bookingId, replacementId, "SUBMITTED");
        using var approved = await CommandAsync(
            buyer, BuyerTenantId,
            $"campaigns/{campaignId}/delivery-proofs/{replacementId}:review",
            "delivery-proof-approve", 1,
            new { approved = true, reason = "The replacement identifies the exact booked site." });
        Assert.Equal("APPROVED", approved.RootElement.GetProperty("status").GetString());
        await AssertSupplierProofRequestAsync(
            supplier, campaignId, bookingId, replacementId, "APPROVED");
        using var approvedReplacement = await RawCommandAsync(
            supplier, SupplierTenantId, $"campaigns/{campaignId}/delivery-proofs",
            "delivery-proof-approved-replacement", null,
            ProofBody(
                bookingId, "2026-09-22T08:00:00Z",
                CreativePng.Append((byte)3).ToArray(), "approved-replacement.png"));
        await AssertProblemAsync(
            approvedReplacement, HttpStatusCode.Conflict, "DELIVERY_PROOF_BLOCKED");
        await AssertDeliveryEvidenceAsync(
            connectionString, firstProofId, replacementId, campaignId);
        await AssertMeasurementBlockedWithoutFactsAsync(buyer, campaignId);
        await AssertPerformanceEvidenceAsync(
            buyer, client, other, campaignId, connectionString);
    }

    private static object ProofBody(
        Guid bookingId, string capturedAtUtc, byte[] content, string fileName) => new
        {
            bookingId,
            proofType = "PHOTO",
            capturedAtUtc,
            locationDescription = "Johannesburg confirmed OOH site",
            latitude = -26.2041m,
            longitude = 28.0473m,
            sourceReference = "supplier-camera:site-001",
            reason = "Supplier submits retained proof for the exact confirmed booking.",
            file = new { fileName, mediaType = "image/png", content },
        };

    private static async Task AssertSupplierProofRequestAsync(
        HttpClient supplier,
        Guid campaignId,
        Guid bookingId,
        Guid? expectedProofId,
        string? expectedProofStatus)
    {
        using var requests = await ReadAsync(
            supplier, SupplierTenantId, "delivery-proof-requests");
        var request = Assert.Single(
            requests.RootElement.EnumerateArray(),
            item => item.GetProperty("campaignId").GetGuid() == campaignId &&
                item.GetProperty("bookingId").GetGuid() == bookingId);
        var responseText = request.GetRawText();
        Assert.DoesNotContain("buyerTenantId", responseText, StringComparison.Ordinal);
        Assert.DoesNotContain("supplierTenantId", responseText, StringComparison.Ordinal);
        Assert.DoesNotContain("supplierCost", responseText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("clientPrice", responseText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("margin", responseText, StringComparison.OrdinalIgnoreCase);
        var proofId = request.GetProperty("latestProofId");
        var proofStatus = request.GetProperty("latestProofStatus");
        if (expectedProofId.HasValue)
        {
            Assert.Equal(expectedProofId.Value, proofId.GetGuid());
            Assert.Equal(expectedProofStatus, proofStatus.GetString());
            return;
        }
        Assert.Equal(JsonValueKind.Null, proofId.ValueKind);
        Assert.Equal(JsonValueKind.Null, proofStatus.ValueKind);
    }

    private static async Task AssertDeliveryProofVisibilityAsync(
        HttpClient buyer, HttpClient supplier, HttpClient other,
        Guid campaignId, Guid proofId)
    {
        using var buyerProof = await ReadAsync(
            buyer, BuyerTenantId, $"delivery-proofs/{proofId}");
        Assert.Equal(proofId, buyerProof.RootElement.GetProperty("id").GetGuid());
        using var supplierProof = await ReadAsync(
            supplier, SupplierTenantId, $"delivery-proofs/{proofId}");
        Assert.Equal(proofId, supplierProof.RootElement.GetProperty("id").GetGuid());
        using var unrelated = await other.GetAsync(
            $"/api/v1/tenants/{OtherTenantId}/delivery-proofs/{proofId}");
        await AssertProblemAsync(unrelated, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");
        using var detail = await ReadAsync(
            buyer, BuyerTenantId, $"campaigns/{campaignId}");
        Assert.Contains(detail.RootElement.GetProperty("deliveryProofs").EnumerateArray(),
            proof => proof.GetProperty("id").GetGuid() == proofId);
        Assert.DoesNotContain("objectKey", detail.RootElement.GetRawText(),
            StringComparison.OrdinalIgnoreCase);
    }

    private static async Task AssertPendingDeliveryTaskAsync(HttpClient buyer, Guid proofId)
    {
        using var tasks = await ReadAsync(buyer, BuyerTenantId, "human-tasks");
        var task = Assert.Single(
            tasks.RootElement.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("taskType").GetString() ==
                "DELIVERY_PROOF_REVIEW" &&
                item.GetProperty("resourceId").GetGuid() == proofId);
        Assert.Equal("PENDING", task.GetProperty("status").GetString());
        Assert.Equal(BuyerUserId, task.GetProperty("assigneeUserId").GetGuid());
    }

    private static async Task AssertSupplierCannotSelfReviewAtDatabaseAsync(
        string connectionString, Guid proofId)
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
        tenant.Parameters.AddWithValue(BuyerTenantId.ToString());
        await tenant.ExecuteNonQueryAsync();
        await using var review = new NpgsqlCommand(
            "UPDATE commercial.delivery_proofs SET status_code = 'APPROVED', " +
            "reviewed_by = $1, reviewed_at_utc = submitted_at_utc, " +
            "review_reason = 'self review', version = 2, updated_at_utc = submitted_at_utc " +
            "WHERE id = $2", connection, transaction);
        review.Parameters.AddWithValue(SupplierUserId);
        review.Parameters.AddWithValue(proofId);
        Assert.Equal(PostgresErrorCodes.RaiseException,
            (await Assert.ThrowsAsync<PostgresException>(review.ExecuteNonQueryAsync)).SqlState);
        await transaction.RollbackAsync();
    }

    private static async Task AssertDeliveryEvidenceAsync(
        string connectionString, Guid rejectedProofId, Guid approvedProofId, Guid campaignId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        Assert.Equal(2, await CountAsync(connection,
            "SELECT count(*)::integer FROM commercial.delivery_proofs"));
        Assert.Equal(2, await CountAsync(connection,
            "SELECT count(*)::integer FROM commercial.human_tasks " +
            "WHERE task_type_code = 'DELIVERY_PROOF_REVIEW' AND status_code = 'COMPLETED'"));
        Assert.Equal(2, await CountAsync(connection,
            "SELECT count(*)::integer FROM commercial.audit_events " +
            "WHERE action_code IN ('campaign.started', 'campaign.completed')"));
        Assert.Equal(4, await CountAsync(connection,
            "SELECT count(*)::integer FROM commercial.audit_events " +
            "WHERE action_code LIKE 'delivery_proof.%'"));
        await using var mutate = new NpgsqlCommand(
            "UPDATE commercial.delivery_proofs SET source_reference = 'changed' WHERE id = $1",
            connection);
        mutate.Parameters.AddWithValue(rejectedProofId);
        Assert.Equal(PostgresErrorCodes.RaiseException,
            (await Assert.ThrowsAsync<PostgresException>(mutate.ExecuteNonQueryAsync)).SqlState);
        await using var campaign = new NpgsqlCommand(
            "SELECT status_code FROM commercial.campaigns WHERE id = $1", connection);
        campaign.Parameters.AddWithValue(campaignId);
        Assert.Equal("COMPLETED", (string?)await campaign.ExecuteScalarAsync());
        await using var retained = new NpgsqlCommand(
            "SELECT count(*)::integer FROM commercial.delivery_proofs " +
            "WHERE (id = $1 AND status_code = 'REJECTED') " +
            "OR (id = $2 AND status_code = 'APPROVED')", connection);
        retained.Parameters.AddWithValue(rejectedProofId);
        retained.Parameters.AddWithValue(approvedProofId);
        Assert.Equal(2, (int)(await retained.ExecuteScalarAsync())!);
    }
}

using System.Net;
using System.Text.Json;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class MarketplaceAcceptanceTests
{
    private static async Task<BackendScenarioObservation> RunDeliveryScenarioAsync(string id)
    {
        await using var postgres = CreatePostgres();
        await postgres.StartAsync();
        var connection = postgres.GetConnectionString();
        await DisposableDatabaseRoles.ProvisionAsync(connection);
        await SeedAsync(connection);
        var clock = new AdjustableMarketplaceClock(InitialTime);
        await using var supplierFactory = CreateFactory(connection, SupplierUserId, clock);
        await using var buyerFactory = CreateFactory(connection, BuyerUserId, clock);
        await using var reviewerFactory = CreateFactory(connection, ReviewerUserId, clock);
        await using var clientFactory = CreateFactory(connection, ClientUserId, clock);
        await using var otherFactory = CreateFactory(connection, OtherUserId, clock);
        using var supplier = supplierFactory.CreateClient();
        using var buyer = buyerFactory.CreateClient();
        using var reviewer = reviewerFactory.CreateClient();
        using var client = clientFactory.CreateClient();
        using var other = otherFactory.CreateClient();
        var listing = await CreateAndPublishListingAsync(supplier, buyer);
        var booked = await ExerciseBookingScenarioAsync(buyer, supplier, reviewer, client, other, listing, clock, id);
        using var missing = await RawCommandAsync(buyer, BuyerTenantId,
            $"campaigns/{booked.CampaignId}:start", id + ":missing-readiness",
            booked.Campaign.GetProperty("version").GetInt64(), new { reason = "Missing creative cannot go live." });
        await AssertProblemAsync(missing, HttpStatusCode.Conflict, "INVALID_LIFECYCLE_TRANSITION");
        object? memoryChange = id == "MEASURE-005"
            ? await RecomputeScenarioMemoryAsync(buyer, supplier, other, listing.ListingVersionId, clock) : null;
        object result = booked;
        if (id != "FUND-007")
        {
            var readyVersion = await ReadyScenarioCampaignAsync(buyer, client, supplier, booked, id);
            await CompleteScenarioCampaignAsync(buyer, booked.CampaignId, readyVersion, clock, id);
            if (id != "MEASURE-002")
                result = await ReviewScenarioProofAsync(buyer, supplier, booked, id, id != "FUND-009");
        }
        if (id.StartsWith("MEASURE-", StringComparison.Ordinal))
            return await ExerciseMeasurementScenarioAsync(buyer, client, other, booked, id, memoryChange);
        using var current = await ReadAsync(buyer, BuyerTenantId, $"campaigns/{booked.CampaignId}");
        return new(new { booked.CampaignId, booked.BookingId, source = "Synthetic funded and confirmed campaign" },
            new { result, campaign = current.RootElement.Clone(), missingReadinessRejected = true },
            id == "FUND-007" ? "BOOKED_READINESS_MISSING" : id == "FUND-008" ? "PROOF_APPROVED" : "PROOF_REJECTED",
            new Dictionary<string, bool>
            {
                ["FUNDING_BEFORE_BOOKING"] = true,
                ["CONSEQUENCE_AUTHORITY_ENFORCED"] = true,
                ["TENANT_SECURITY_PASS"] = true,
            }, ["INDEPENDENT_FUNDING", "SUPPLIER_CONFIRMATION", "CLIENT_CREATIVE_APPROVAL", "HUMAN_PROOF_REVIEW"],
            0, null, "FUNDED_EXACT_LINE_AND_CONFIRMED_BOOKING", "WRONG_SUPPLIER_CONFIRMATION_REJECTED");
    }

    private static async Task<long> ReadyScenarioCampaignAsync(
        HttpClient buyer, HttpClient client, HttpClient supplier, ScenarioBookedCampaign booked, string id)
    {
        var campaignId = booked.CampaignId;
        using var requested = await CommandAsync(buyer, BuyerTenantId,
            $"campaigns/{campaignId}:request-creative", id + ":creative", booked.Campaign.GetProperty("version").GetInt64(),
            new { requirements = new[] { new
            {
                bookingId = booked.BookingId, formatCode = "OOH_1920X1080", width = 1920, height = 1080,
                requiredMediaType = "image/png", maximumBytes = 1_000_000,
                instructions = "Supply rights-cleared synthetic fixture artwork for this exact booking.",
            } }, reason = "Human supplies every booked creative requirement." });
        var version = requested.RootElement.GetProperty("version").GetInt64();
        var requirementId = await GetRequirementIdAsync(buyer, campaignId);
        using var uploaded = await CommandAsync(buyer, BuyerTenantId, $"campaigns/{campaignId}/creative",
            id + ":upload", null, AssetBody(version, requirementId, "Synthetic approved campaign copy."));
        var assetId = uploaded.RootElement.GetProperty("id").GetGuid();
        using var brand = await CommandAsync(client, BuyerTenantId,
            $"campaigns/{campaignId}/creative/{assetId}:brand-review", id + ":brand", 1,
            new { approved = true, rightsStatus = "APPROVED", evidenceReference = "fixture:rights-approval",
                reason = "Human client approves exact artwork and rights." });
        using var technical = await CommandAsync(supplier, SupplierTenantId,
            $"creative-assets/{assetId}:supplier-review", id + ":technical", 2,
            SupplierReviewBody(true, "Supplier verifies exact file against booked requirements."));
        using var ready = await CommandAsync(client, BuyerTenantId,
            $"campaigns/{campaignId}:approve-creative", id + ":ready", version,
            new { reason = "Human approves the exact reviewed files." });
        Assert.Equal("READY", ready.RootElement.GetProperty("status").GetString());
        return ready.RootElement.GetProperty("version").GetInt64();
    }

    private static async Task CompleteScenarioCampaignAsync(
        HttpClient buyer, Guid campaignId, long version, AdjustableMarketplaceClock clock, string id)
    {
        using var live = await CommandAsync(buyer, BuyerTenantId, $"campaigns/{campaignId}:start",
            id + ":start", version, new { reason = "Human starts the ready campaign inside its booked flight." });
        Assert.Equal("LIVE", live.RootElement.GetProperty("status").GetString());
        clock.Set(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
        using var completed = await CommandAsync(buyer, BuyerTenantId, $"campaigns/{campaignId}:complete",
            id + ":complete", live.RootElement.GetProperty("version").GetInt64(),
            new { completionReason = "Booked flight ended.", proofRequestReason = "Human requests supplier proof." });
        Assert.Equal("COMPLETED", completed.RootElement.GetProperty("status").GetString());
    }

    private static async Task<JsonElement> ReviewScenarioProofAsync(
        HttpClient buyer, HttpClient supplier, ScenarioBookedCampaign booked, string id, bool approve)
    {
        var campaignId = booked.CampaignId;
        using var submitted = await CommandAsync(supplier, SupplierTenantId,
            $"campaigns/{campaignId}/delivery-proofs", id + ":proof", null,
            ProofBody(booked.BookingId, "2026-09-20T08:00:00Z", CreativePng, "fixture-proof.png"));
        var proofId = submitted.RootElement.GetProperty("id").GetGuid();
        using var self = await RawCommandAsync(supplier, SupplierTenantId,
            $"campaigns/{campaignId}/delivery-proofs/{proofId}:review", id + ":proof-self", 1,
            new { approved = true, reason = "Supplier cannot approve their own evidence." });
        await AssertProblemAsync(self, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");
        using var reviewed = await CommandAsync(buyer, BuyerTenantId,
            $"campaigns/{campaignId}/delivery-proofs/{proofId}:review", id + ":proof-review", 1,
            new { approved = approve, reason = approve ? "Exact booked site identified." : "Image fails to identify site." });
        Assert.Equal(approve ? "APPROVED" : "REJECTED", reviewed.RootElement.GetProperty("status").GetString());
        using var overwrite = await RawCommandAsync(buyer, BuyerTenantId,
            $"campaigns/{campaignId}/delivery-proofs/{proofId}:review", id + ":proof-overwrite", 2,
            new { approved = !approve, reason = "A retained decision cannot be overwritten." });
        await AssertProblemAsync(overwrite, HttpStatusCode.Conflict, "DELIVERY_PROOF_BLOCKED");
        await AssertSupplierProofRequestAsync(supplier, campaignId, booked.BookingId, proofId, approve ? "APPROVED" : "REJECTED");
        return reviewed.RootElement.Clone();
    }
}

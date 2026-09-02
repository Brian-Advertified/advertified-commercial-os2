using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class MarketplaceAcceptanceTests
{
    [Fact]
    [Trait("Category", "Migration")]
    public async Task ClientSelectedMarketplaceLineRequiresBuyerRequestAndSupplierConfirmation()
    {
        await using var postgres = CreatePostgres();
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DisposableDatabaseRoles.ProvisionAsync(connectionString);
        await SeedAsync(connectionString);
        var clock = new AdjustableMarketplaceClock(InitialTime);
        await using var supplierFactory = CreateFactory(
            connectionString, SupplierUserId, clock);
        await using var buyerFactory = CreateFactory(connectionString, BuyerUserId, clock);
        await using var clientFactory = CreateFactory(connectionString, ClientUserId, clock);
        await using var reviewerFactory = CreateFactory(connectionString, ReviewerUserId, clock);
        await using var otherFactory = CreateFactory(connectionString, OtherUserId, clock);
        using var supplier = supplierFactory.CreateClient();
        using var buyer = buyerFactory.CreateClient();
        using var client = clientFactory.CreateClient();
        using var reviewer = reviewerFactory.CreateClient();
        using var other = otherFactory.CreateClient();

        var listing = await CreateAndPublishListingAsync(supplier, buyer);
        var plan = await BuildBuyerPlanAsync(buyer, listing.ListingVersionId);
        using var approvedPlan = await CommandAsync(
            buyer, BuyerTenantId, $"media-plan-versions/{plan.Id}:approve",
            "booking-plan-approve", plan.Version,
            new { reason = "Buyer approved the exact marketplace placement." });
        var selected = await CreateSelectedProposalAsync(buyer, client, plan.Id, clock);
        using var unfundedBooking = await RawCommandAsync(
            buyer, BuyerTenantId, "bookings", "booking-before-funding", null,
            new
            {
                proposalVersionId = selected.ProposalId,
                proposalOptionId = selected.OptionId,
                mediaPlanLineId = plan.LineId,
                terms = "Funding must be confirmed before this can become a booking.",
            });
        await AssertProblemAsync(
            unfundedBooking, HttpStatusCode.Conflict, "BOOKING_REVIEW_REQUIRED");
        using var beforeFunding = await ReadAsync(
            buyer, BuyerTenantId, "bookings/bookable-lines");
        Assert.Empty(beforeFunding.RootElement.EnumerateArray());
        var campaignId = await FundSelectedProposalAsync(buyer, reviewer, selected);
        using var supplierCampaign = await supplier.GetAsync(
            $"/api/v1/tenants/{SupplierTenantId}/campaigns/{campaignId}");
        await AssertProblemAsync(
            supplierCampaign, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");

        using var bookable = await ReadAsync(buyer, BuyerTenantId, "bookings/bookable-lines");
        var line = Assert.Single(bookable.RootElement.EnumerateArray());
        Assert.Equal(selected.ProposalId, line.GetProperty("proposalVersionId").GetGuid());
        Assert.False(line.GetProperty("alreadyBooked").GetBoolean());

        var createBody = new
        {
            proposalVersionId = selected.ProposalId,
            proposalOptionId = selected.OptionId,
            mediaPlanLineId = line.GetProperty("mediaPlanLineId").GetGuid(),
            terms = "Supplier confirmation is required before this booking is binding.",
        };
        using var draft = await CommandAsync(
            buyer, BuyerTenantId, "bookings", "booking-create", null,
            createBody);
        var bookingId = draft.RootElement.GetProperty("id").GetGuid();
        Assert.Equal("DRAFT", draft.RootElement.GetProperty("status").GetString());

        using var unrelated = await other.GetAsync(
            $"/api/v1/tenants/{OtherTenantId}/bookings/{bookingId}");
        await AssertProblemAsync(unrelated, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");
        using var supplierDraft = await ReadAsync(
            supplier, SupplierTenantId, $"bookings/{bookingId}");
        Assert.Equal(JsonValueKind.Null,
            supplierDraft.RootElement.GetProperty("clientPriceMinor").ValueKind);
        Assert.Equal(JsonValueKind.Null,
            supplierDraft.RootElement.GetProperty("feesMinor").ValueKind);
        Assert.Equal(JsonValueKind.Null,
            supplierDraft.RootElement.GetProperty("proposalVersionId").ValueKind);
        Assert.Equal(JsonValueKind.Null,
            supplierDraft.RootElement.GetProperty("mediaPlanLineId").ValueKind);

        using var requested = await CommandAsync(
            buyer, BuyerTenantId, $"bookings/{bookingId}:request-confirmation",
            "booking-request", 1,
            new { reason = "Buyer explicitly requests confirmation of this frozen line." });
        Assert.Equal("PENDING_SUPPLIER", requested.RootElement.GetProperty("status").GetString());
        using var prematureCampaign = await RawCommandAsync(
            buyer, BuyerTenantId, $"campaigns/{campaignId}:confirm-bookings",
            "campaign-confirm-premature", 1,
            new { reason = "Pending supplier confirmation must not be enough." });
        await AssertProblemAsync(
            prematureCampaign, HttpStatusCode.Conflict, "CAMPAIGN_READINESS_BLOCKED");

        using var rejected = await RawCommandAsync(
            supplier, SupplierTenantId, $"bookings/{bookingId}:confirm",
            "booking-reject-unaccepted-terms", 2,
            new { acceptTerms = false, reason = "Terms were not accepted.", note = "No." });
        await AssertProblemAsync(rejected, HttpStatusCode.BadRequest, "VALIDATION_FAILED");
        using var confirmed = await CommandAsync(
            supplier, SupplierTenantId, $"bookings/{bookingId}:confirm",
            "booking-confirm", 2,
            new
            {
                acceptTerms = true,
                reason = "Current rate and availability confirmed by the supplier.",
                note = "Placement reserved only through this confirmed booking record.",
            });
        Assert.Equal("CONFIRMED", confirmed.RootElement.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null,
            confirmed.RootElement.GetProperty("clientPriceMinor").ValueKind);
        await AssertRoleAppropriateBookingProjectionsAsync(
            buyer, supplier, reviewer, bookingId);
        await AssertBuyerSafeBookingProjectionAsync(client, bookingId);
        using var campaignReady = await ReadAsync(
            buyer, BuyerTenantId, $"campaigns/{campaignId}");
        Assert.Equal("campaign_confirm_bookings", campaignReady.RootElement
            .GetProperty("nextActionPermission").GetString());
        using var bookedCampaign = await CommandAsync(
            buyer, BuyerTenantId, $"campaigns/{campaignId}:confirm-bookings",
            "campaign-confirm-bookings", 1,
            new { reason = "Every exact selected line has a confirmed supplier booking." });
        Assert.Equal("BOOKED", bookedCampaign.RootElement.GetProperty("status").GetString());
        Assert.Equal(1, bookedCampaign.RootElement
            .GetProperty("confirmedBookingCount").GetInt32());
        Assert.Equal("campaign_request_creative", bookedCampaign.RootElement
            .GetProperty("nextActionPermission").GetString());
        using var duplicateCampaign = await RawCommandAsync(
            buyer, BuyerTenantId, $"campaigns/{campaignId}:confirm-bookings",
            "campaign-confirm-bookings-duplicate", 2,
            new { reason = "A booked campaign cannot repeat readiness." });
        await AssertProblemAsync(
            duplicateCampaign, HttpStatusCode.Conflict, "INVALID_LIFECYCLE_TRANSITION");
        using var wrongSupplier = await RawCommandAsync(
            other, OtherTenantId, $"bookings/{bookingId}:confirm",
            "booking-wrong-supplier-confirm", 2,
            new { acceptTerms = true, reason = "Wrong supplier.", note = "Denied." });
        await AssertProblemAsync(
            wrongSupplier, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");
        using var duplicate = await RawCommandAsync(
            supplier, SupplierTenantId, $"bookings/{bookingId}:confirm",
            "booking-duplicate-confirm", 3,
            new { acceptTerms = true, reason = "Duplicate confirmation.", note = "Denied." });
        await AssertProblemAsync(
            duplicate, HttpStatusCode.Conflict, "INVALID_LIFECYCLE_TRANSITION");

        using var buyerView = await ReadAsync(buyer, BuyerTenantId, $"bookings/{bookingId}");
        Assert.Equal(1_443_250,
            buyerView.RootElement.GetProperty("clientPriceMinor").GetInt64());
        Assert.Equal(5_000, buyerView.RootElement.GetProperty("feesMinor").GetInt64());
        Assert.Equal(188_250, buyerView.RootElement.GetProperty("vatMinor").GetInt64());
        await AssertBookingEvidenceAsync(connectionString, bookingId, campaignId);
        var readyVersion = await AssertCreativeProductionReadinessAsync(
            buyer, client, supplier, other, campaignId, bookingId,
            bookedCampaign.RootElement.GetProperty("version").GetInt64(), connectionString);
        await AssertCampaignDeliveryProofAsync(
            buyer, client, supplier, other, campaignId, bookingId, readyVersion,
            clock, connectionString);
        await ChangeClientBookingRoleAsync(connectionString);
        await AssertBuyerSafeBookingProjectionAsync(client, bookingId);
    }

    [Fact]
    [Trait("Category", "Migration")]
    public async Task WithdrawnMarketplaceSupplyBlocksBookingRequestWithoutMutation()
    {
        await using var postgres = CreatePostgres();
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DisposableDatabaseRoles.ProvisionAsync(connectionString);
        await SeedAsync(connectionString);
        var clock = new AdjustableMarketplaceClock(InitialTime);
        await using var supplierFactory = CreateFactory(
            connectionString, SupplierUserId, clock);
        await using var buyerFactory = CreateFactory(connectionString, BuyerUserId, clock);
        await using var clientFactory = CreateFactory(connectionString, ClientUserId, clock);
        await using var reviewerFactory = CreateFactory(connectionString, ReviewerUserId, clock);
        using var supplier = supplierFactory.CreateClient();
        using var buyer = buyerFactory.CreateClient();
        using var client = clientFactory.CreateClient();
        using var reviewer = reviewerFactory.CreateClient();

        var listing = await CreateAndPublishListingAsync(supplier, buyer);
        var plan = await BuildBuyerPlanAsync(buyer, listing.ListingVersionId);
        using var approvedPlan = await CommandAsync(
            buyer, BuyerTenantId, $"media-plan-versions/{plan.Id}:approve",
            "booking-plan-approve", plan.Version,
            new { reason = "Approve before booking preparation." });
        var selected = await CreateSelectedProposalAsync(buyer, client, plan.Id, clock);
        await FundSelectedProposalAsync(buyer, reviewer, selected);
        using var bookable = await ReadAsync(buyer, BuyerTenantId, "bookings/bookable-lines");
        var line = Assert.Single(bookable.RootElement.EnumerateArray());
        using var draft = await CommandAsync(
            buyer, BuyerTenantId, "bookings", "booking-create", null,
            new
            {
                proposalVersionId = selected.ProposalId,
                proposalOptionId = selected.OptionId,
                mediaPlanLineId = line.GetProperty("mediaPlanLineId").GetGuid(),
                terms = "Supplier confirmation is required.",
            });
        var bookingId = draft.RootElement.GetProperty("id").GetGuid();
        await ArchiveListingAsync(supplier, buyer, listing.ListingId);

        using var request = await RawCommandAsync(
            buyer, BuyerTenantId, $"bookings/{bookingId}:request-confirmation",
            "booking-request-withdrawn", 1,
            new { reason = "Withdrawn supply must not proceed." });
        await AssertProblemAsync(
            request, HttpStatusCode.Conflict, "BOOKING_REVIEW_REQUIRED");
        using var unchanged = await ReadAsync(buyer, BuyerTenantId, $"bookings/{bookingId}");
        Assert.Equal("DRAFT", unchanged.RootElement.GetProperty("status").GetString());
        Assert.Equal(1, unchanged.RootElement.GetProperty("version").GetInt64());
    }

    private static async Task<SelectedProposalFixture> CreateSelectedProposalAsync(
        HttpClient buyer,
        HttpClient client,
        Guid planId,
        AdjustableMarketplaceClock clock)
    {
        using var generated = await CommandAsync(
            buyer, BuyerTenantId, $"briefs/{BuyerBriefId}/proposals:generate",
            "booking-proposal-generate", null,
            new
            {
                title = "Client-selected Johannesburg OOH plan",
                options = new[]
                {
                    new
                    {
                        planVersionId = planId,
                        label = "Approved marketplace placement",
                        outcome = "Deliver the client-approved Johannesburg OOH campaign.",
                    },
                },
                terms = "Only this exact approved line may proceed to booking.",
                expiryAtUtc = clock.GetUtcNow().AddDays(30),
            });
        var proposalId = generated.RootElement.GetProperty("id").GetGuid();
        var optionId = generated.RootElement.GetProperty("options")[0]
            .GetProperty("id").GetGuid();
        using var approved = await CommandAsync(
            buyer, BuyerTenantId, $"proposal-versions/{proposalId}:approve",
            "booking-proposal-approve", 1,
            new { reason = "Exact plan and commercial wording reviewed." });
        using var rendered = await CommandAsync(
            buyer, BuyerTenantId, $"proposal-versions/{proposalId}:render",
            "booking-proposal-render", 2, new { });
        using var shared = await CommandAsync(
            buyer, BuyerTenantId, $"proposal-versions/{proposalId}:share",
            "booking-proposal-share", 3,
            new { recipientUserId = ClientUserId, reason = "Send for client decision." });
        using var selected = await CommandAsync(
            client, BuyerTenantId, $"proposal-versions/{proposalId}:select-option",
            "booking-proposal-select", 4,
            new { optionId, reason = "Client selects this exact option." });
        Assert.Equal("SELECTED", selected.RootElement.GetProperty("status").GetString());
        return new SelectedProposalFixture(proposalId, optionId);
    }

    private static async Task AssertBookingEvidenceAsync(
        string connectionString,
        Guid bookingId,
        Guid campaignId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        Assert.Equal(3, await CountAsync(connection,
            "SELECT count(*)::integer FROM commercial.audit_events WHERE action_code LIKE 'booking.%'"));
        Assert.Equal(3, await CountAsync(connection,
            "SELECT count(*)::integer FROM commercial.outbox_messages WHERE event_type_code LIKE 'Booking%'"));
        Assert.Equal(2, await CountAsync(connection,
            "SELECT count(*)::integer FROM commercial.audit_events WHERE action_code LIKE 'campaign.%'"));
        Assert.Equal(2, await CountAsync(connection,
            """
            SELECT count(*)::integer FROM commercial.outbox_messages
            WHERE event_type_code IN ('CampaignPlanned', 'CampaignBookingsConfirmed')
            """));
        await using var mutate = new NpgsqlCommand(
            "UPDATE commercial.bookings SET client_price_minor = 1 WHERE id = $1", connection);
        mutate.Parameters.AddWithValue(bookingId);
        var exception = await Assert.ThrowsAsync<PostgresException>(mutate.ExecuteNonQueryAsync);
        Assert.Equal(PostgresErrorCodes.RaiseException, exception.SqlState);
        await using var mutateCampaign = new NpgsqlCommand(
            "UPDATE commercial.campaigns SET status_code = 'LIVE' WHERE id = $1", connection);
        mutateCampaign.Parameters.AddWithValue(campaignId);
        var campaignException = await Assert.ThrowsAsync<PostgresException>(
            mutateCampaign.ExecuteNonQueryAsync);
        Assert.Equal(PostgresErrorCodes.RaiseException, campaignException.SqlState);
    }

    private sealed record SelectedProposalFixture(Guid ProposalId, Guid OptionId);
}

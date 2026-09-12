using System.Net;
using System.Text.Json;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class MarketplaceAcceptanceTests
{
    private sealed record ScenarioBookedCampaign(Guid CampaignId, Guid BookingId,
        JsonElement Campaign, JsonElement Booking, JsonElement Quotes);

    private static async Task<ScenarioBookedCampaign> ExerciseBookingScenarioAsync(
        HttpClient buyer, HttpClient supplier, HttpClient reviewer, HttpClient client, HttpClient other,
        ListingFixture listing, AdjustableMarketplaceClock clock, string id)
    {
        var plan = await BuildBuyerPlanAsync(buyer, listing.ListingVersionId);
        using var approved = await CommandAsync(buyer, BuyerTenantId,
            $"media-plan-versions/{plan.Id}:approve", id + ":plan", plan.Version,
            new { reason = "Human approves the exact plan." });
        var selected = await CreateSelectedProposalAsync(buyer, reviewer, client, plan.Id, clock);
        var body = new { proposalVersionId = selected.ProposalId, proposalOptionId = selected.OptionId,
            mediaPlanLineId = plan.LineId, terms = "Supplier must explicitly confirm accepted terms." };
        using var unfunded = await RawCommandAsync(buyer, BuyerTenantId, "bookings", id + ":unfunded", null, body);
        await AssertProblemAsync(unfunded, HttpStatusCode.Conflict, "BOOKING_REVIEW_REQUIRED");
        await AcceptPlanLineRfqAsync(buyer, supplier, listing.ListingVersionId, clock, id + ":rfq");
        using var quoteHistory = await ReadAsync(buyer, BuyerTenantId, "marketplace-rfqs");
        var campaignId = await FundSelectedProposalAsync(buyer, reviewer, selected);
        using var draft = await CommandAsync(buyer, BuyerTenantId, "bookings", id + ":book", null, body);
        var bookingId = draft.RootElement.GetProperty("id").GetGuid();
        using var requested = await CommandAsync(buyer, BuyerTenantId,
            $"bookings/{bookingId}:request-confirmation", id + ":request", 1,
            new { reason = "Human requests supplier confirmation of the funded line." });
        using var premature = await RawCommandAsync(buyer, BuyerTenantId,
            $"campaigns/{campaignId}:confirm-bookings", id + ":premature", 1,
            new { reason = "Pending supplier confirmation must block readiness." });
        await AssertProblemAsync(premature, HttpStatusCode.Conflict, "CAMPAIGN_READINESS_BLOCKED");
        using var denied = await RawCommandAsync(other, OtherTenantId,
            $"bookings/{bookingId}:confirm", id + ":wrong-supplier", 2,
            new { acceptTerms = true, reason = "Unrelated supplier must be denied.", note = "Denied." });
        await AssertProblemAsync(denied, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");
        using var confirmed = await CommandAsync(supplier, SupplierTenantId,
            $"bookings/{bookingId}:confirm", id + ":confirm", 2,
            new { acceptTerms = true, reason = "Supplier confirms the exact accepted line.", note = "Synthetic confirmation." });
        Assert.Equal("CONFIRMED", confirmed.RootElement.GetProperty("status").GetString());
        using var booked = await CommandAsync(buyer, BuyerTenantId,
            $"campaigns/{campaignId}:confirm-bookings", id + ":booked", 1,
            new { reason = "All funded lines have supplier confirmation." });
        Assert.Equal("BOOKED", booked.RootElement.GetProperty("status").GetString());
        using var retained = await ReadAsync(buyer, BuyerTenantId, "marketplace-rfqs");
        Assert.Equal(quoteHistory.RootElement.GetRawText(), retained.RootElement.GetRawText());
        await AssertBuyerSafeBookingProjectionAsync(client, bookingId);
        return new(campaignId, bookingId, booked.RootElement.Clone(), confirmed.RootElement.Clone(),
            retained.RootElement.Clone());
    }
}

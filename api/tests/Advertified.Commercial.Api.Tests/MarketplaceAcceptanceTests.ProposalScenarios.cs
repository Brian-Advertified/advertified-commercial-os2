using System.Net;
using System.Text.Json;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class MarketplaceAcceptanceTests
{
    private static async Task<object> ExerciseProposalScenarioAsync(
        HttpClient buyer, HttpClient supplier, HttpClient reviewer, HttpClient client, HttpClient other,
        ListingFixture listing, AdjustableMarketplaceClock clock, string id)
    {
        var plan = await BuildBuyerPlanAsync(buyer, listing.ListingVersionId);
        using var approved = await CommandAsync(buyer, BuyerTenantId,
            $"media-plan-versions/{plan.Id}:approve", id + ":plan-approve", plan.Version,
            new { reason = "Human approves exact source plan." });
        var planIds = id == "TXN-002"
            ? await CreateScenarioAlternativePlansAsync(buyer, plan.Id) : new[] { plan.Id };
        using var generated = await CommandAsync(buyer, BuyerTenantId,
            $"briefs/{BuyerBriefId}/proposals:generate", id + ":generate", null, new
            {
                title = "Synthetic executable proposal scenario",
                options = planIds.Select((planId, index) => new
                {
                    planVersionId = planId, label = $"Option {index + 1}",
                    outcome = "Deliver the approved Johannesburg placement.",
                }).ToArray(),
                terms = "Exact approved inventory and prices only.",
                expiryAtUtc = clock.GetUtcNow().AddDays(30),
            });
        var proposal = generated.RootElement;
        var proposalId = proposal.GetProperty("id").GetGuid();
        var options = proposal.GetProperty("options").Clone();
        Assert.Equal(id == "TXN-002" ? 3 : 1, options.GetArrayLength());
        using var denied = await other.GetAsync($"/api/v1/tenants/{OtherTenantId}/proposals/{proposalId}");
        await AssertProblemAsync(denied, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");
        using var early = await RawCommandAsync(client, BuyerTenantId,
            $"proposal-versions/{proposalId}:select-option", id + ":early-select", 1,
            new { optionId = options[0].GetProperty("id").GetGuid(), reason = "Unshared output cannot be selected." });
        Assert.Equal(HttpStatusCode.Forbidden, early.StatusCode);
        if (id == "TXN-003")
            await AssertScenarioStaleProposalAsync(buyer, supplier, listing.ListingId, proposalId, id);
        else
        {
            await ShareScenarioProposalAsync(buyer, reviewer, proposalId, id);
            if (id is "TXN-004" or "TXN-005")
                await DecideScenarioProposalAsync(client, proposalId, options[0].GetProperty("id").GetGuid(), id);
        }
        using var retained = await ReadAsync(buyer, BuyerTenantId, $"proposals/{proposalId}");
        Assert.Equal(options.GetRawText(), retained.RootElement.GetProperty("options").GetRawText());
        using var quotes = await ReadAsync(buyer, BuyerTenantId, "marketplace-rfqs");
        Assert.Empty(quotes.RootElement.GetProperty("items").EnumerateArray());
        return new { proposal = retained.RootElement.Clone(), sourcePlan = approved.RootElement.Clone(),
            unsharedDecisionRejected = true, negotiationCount = 0 };
    }

    private static async Task ShareScenarioProposalAsync(
        HttpClient buyer, HttpClient reviewer, Guid proposalId, string id)
    {
        using var branded = await CommandAsync(buyer, BuyerTenantId,
            $"proposal-versions/{proposalId}:approve-unbranded", id + ":unbranded", 1,
            new { reason = "Human approves neutral layout because no brand assets were supplied." });
        using var submitted = await CommandAsync(buyer, BuyerTenantId,
            $"proposal-versions/{proposalId}:submit", id + ":submit", 2,
            new { approverUserId = ReviewerUserId, comment = "Independent review of exact options." });
        using var approved = await CommandAsync(reviewer, BuyerTenantId,
            $"proposal-versions/{proposalId}:approve", id + ":approve", 3,
            new { reason = "Reviewer approves the exact commercial content." });
        using var rendered = await CommandAsync(buyer, BuyerTenantId,
            $"proposal-versions/{proposalId}:render", id + ":render", 4, new { });
        using var shared = await CommandAsync(buyer, BuyerTenantId,
            $"proposal-versions/{proposalId}:share", id + ":share", 5,
            new { recipientUserId = ClientUserId, reason = "Human authorises client access." });
        Assert.Equal("SENT", shared.RootElement.GetProperty("status").GetString());
    }

    private static async Task DecideScenarioProposalAsync(
        HttpClient client, Guid proposalId, Guid optionId, string id)
    {
        var action = id == "TXN-005" ? "decline" : "select-option";
        using var decision = await CommandAsync(client, BuyerTenantId,
            $"proposal-versions/{proposalId}:{action}", id + ":decision", 6,
            new { optionId, reason = "Authorised client records their exact decision." });
        Assert.Equal(id == "TXN-005" ? "DECLINED" : "SELECTED",
            decision.RootElement.GetProperty("status").GetString());
        using var repeated = await RawCommandAsync(client, BuyerTenantId,
            $"proposal-versions/{proposalId}:{action}", id + ":repeat", 7,
            new { optionId, reason = "Second decision must not overwrite the first." });
        Assert.Equal(HttpStatusCode.Forbidden, repeated.StatusCode);
    }

    private static async Task AssertScenarioStaleProposalAsync(
        HttpClient buyer, HttpClient supplier, Guid listingId, Guid proposalId, string id)
    {
        await ArchiveListingAsync(supplier, buyer, listingId);
        using var branded = await CommandAsync(buyer, BuyerTenantId,
            $"proposal-versions/{proposalId}:approve-unbranded", id + ":unbranded", 1,
            new { reason = "Neutral layout approval does not approve withdrawn supply." });
        using var blocked = await RawCommandAsync(buyer, BuyerTenantId,
            $"proposal-versions/{proposalId}:submit", id + ":stale-submit", 2,
            new { approverUserId = ReviewerUserId, comment = "Withdrawn listing must block progression." });
        await AssertProblemAsync(blocked, HttpStatusCode.Conflict, "PROPOSAL_STALE");
    }
}

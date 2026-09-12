using System.Text.Json;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class MarketplaceAcceptanceTests
{
    private static async Task<Guid[]> CreateScenarioAlternativePlansAsync(HttpClient buyer, Guid firstPlanId)
    {
        var second = await CreateScenarioAlternativeMixAsync(buyer, 500);
        var third = await CreateScenarioAlternativeMixAsync(buyer, 750);
        Assert.NotEqual(second, third);
        return [firstPlanId, second, third];
    }

    private static async Task<Guid> CreateScenarioAlternativeMixAsync(HttpClient buyer, int quantity)
    {
        var key = "scenario-alternative-" + quantity;
        using var mix = await CommandAsync(buyer, BuyerTenantId,
            $"brief-versions/{BuyerBriefVersionId}/media-mixes:generate", key + ":mix", 1, new { });
        var mixId = mix.RootElement.GetProperty("id").GetGuid();
        using var edited = await CommandAsync(buyer, BuyerTenantId,
            $"media-mix-versions/{mixId}:update", key + ":edit", 1, new
            {
                allocations = new[] { new
                {
                    channel = "OOH", budgetMinor = 2_000_000, role = "Alternative explicitly sized purchase",
                    runningPeriods = new[] { new { start = "2026-09-01", end = "2026-09-30" } },
                    purchases = new[] { new
                    {
                        inventoryTenantId = SupplierTenantId, inventoryProductId = ProductId,
                        productVersionId = ProductVersionId, rateId = RateId, rateType = "CPM", quantity,
                    } },
                } },
                reason = "Human selects a materially different purchase quantity.",
            });
        using var approved = await CommandAsync(buyer, BuyerTenantId,
            $"media-mix-versions/{mixId}:approve", key + ":approve", 2,
            new { reason = "Human approves the independently retained alternative mix." });
        using var shortlist = await CommandAsync(buyer, BuyerTenantId,
            $"brief-versions/{BuyerBriefVersionId}/shortlists:generate", key + ":shortlist", 1, new { });
        Assert.Equal(mixId, shortlist.RootElement.GetProperty("mixVersionId").GetGuid());
        var candidate = Assert.Single(shortlist.RootElement.GetProperty("candidates").EnumerateArray());
        Assert.True(candidate.GetProperty("isEligible").GetBoolean());
        var shortlistId = shortlist.RootElement.GetProperty("id").GetGuid();
        using var selected = await CommandAsync(buyer, BuyerTenantId,
            $"shortlist-versions/{shortlistId}:select", key + ":select", 1,
            new { selectedCandidateIds = new[] { candidate.GetProperty("id").GetGuid() },
                reason = "Human confirms the exact alternative purchase." });
        return await ApproveScenarioAlternativePlanAsync(buyer, key, quantity);
    }

    private static async Task<Guid> ApproveScenarioAlternativePlanAsync(HttpClient buyer, string key, int quantity)
    {
        using var draft = await CommandAsync(buyer, BuyerTenantId,
            $"brief-versions/{BuyerBriefVersionId}/media-plans:generate", key + ":plan", 1, new { });
        var plan = draft.RootElement;
        var planId = plan.GetProperty("id").GetGuid();
        var version = plan.GetProperty("version").GetInt64();
        var supplierCost = 1_250L * quantity;
        var fee = 5L * quantity;
        var vat = (long)decimal.Round((supplierCost + fee) * 0.15m, 0, MidpointRounding.AwayFromZero);
        Assert.Equal(fee, plan.GetProperty("feesMinor").GetInt64());
        Assert.Equal(vat, plan.GetProperty("vatMinor").GetInt64());
        Assert.Equal(supplierCost + fee + vat, plan.GetProperty("totalMinor").GetInt64());
        foreach (var objection in plan.GetProperty("objections").EnumerateArray())
        {
            var code = objection.GetProperty("code").GetString()!;
            using var resolved = await CommandAsync(buyer, BuyerTenantId,
                $"media-plan-versions/{planId}/objections/{code}:resolve", key + ":resolve:" + code, version,
                new { resolution = "ACCEPTED_WITH_REASON",
                    reason = "Human accepts the visible benchmark limitation for this exact quantity." });
            version = resolved.RootElement.GetProperty("version").GetInt64();
        }
        using var approved = await CommandAsync(buyer, BuyerTenantId,
            $"media-plan-versions/{planId}:approve", key + ":plan-approve", version,
            new { reason = "Human approves exact price and quantity for this alternative." });
        Assert.Equal("APPROVED", approved.RootElement.GetProperty("status").GetString());
        return planId;
    }
}

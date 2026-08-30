using System.Net;
using System.Text.Json;
using Npgsql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class MarketplaceAcceptanceTests
{
    private static async Task SeedBuyerPlanningAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var batch = new NpgsqlBatch(connection);
        Add(batch, """
            INSERT INTO commercial.campaign_briefs (
                id, tenant_id, client_account_id, title, owner_user_id, status_code,
                version, created_at_utc, updated_at_utc)
            VALUES ($1, $2, $3, 'Marketplace supplied campaign', $4, 'APPROVED', 1, $5, $5)
            """, BuyerBriefId, BuyerTenantId, BuyerClientId, BuyerUserId, InitialTime);
        Add(batch, """
            INSERT INTO commercial.brief_sources (
                id, tenant_id, brief_id, source_type_code, locator, title, content,
                content_hash, created_by, created_at_utc)
            VALUES ($1, $2, $3, 'SUPPLIED_TEXT', 'owner:supplied', 'Approved source',
                'Johannesburg OOH campaign', repeat('b', 64), $4, $5)
            """, BuyerBriefSourceId, BuyerTenantId, BuyerBriefId, BuyerUserId, InitialTime);
        Add(batch, """
            INSERT INTO commercial.brief_versions (
                id, tenant_id, brief_id, source_id, version_no, business_problem,
                objective, audiences_json, geographies_json, timing, budget_minor,
                budget_unknown, currency_code, vat_status_code, fees_minor,
                constraints_json, measurement_json, facts_json, unknowns_json,
                assumptions_json, conflicts_json, evidence_bindings_json, status_code,
                created_by, approved_by, approved_at_utc, version, created_at_utc)
            VALUES ($1, $2, $3, $4, 1, 'Build local awareness',
                'Reach Johannesburg business decision makers',
                '["Business decision makers"]', '["Johannesburg"]', 'September 2026',
                2000000, false, 'ZAR', 'REGISTERED', 5000, '[]', '[]',
                '["Owner supplied objective"]', '[]', '[]', '[]', '[]',
                'APPROVED', $5, $5, $6, 1, $6)
            """, BuyerBriefVersionId, BuyerTenantId, BuyerBriefId,
            BuyerBriefSourceId, BuyerUserId, InitialTime);
        Add(batch, """
            UPDATE commercial.campaign_briefs
            SET current_draft_version_id = $1, approved_version_id = $1
            WHERE id = $2
            """, BuyerBriefVersionId, BuyerBriefId);
        await batch.ExecuteNonQueryAsync();
    }

    private static async Task<PlanFixture> BuildBuyerPlanAsync(
        HttpClient buyer,
        Guid listingVersionId)
    {
        using var mode = await CommandAsync(
            buyer, BuyerTenantId, $"brief-versions/{BuyerBriefVersionId}/campaign-mode:select",
            "marketplace-plan-mode", 1, new
            {
                mode = "OOH_ONLY",
                decisionSource = "AGENT",
                confidence = 1m,
                reason = "The supplied brief explicitly requests OOH.",
            });
        using var audience = await CommandAsync(
            buyer, BuyerTenantId, $"brief-versions/{BuyerBriefVersionId}/audiences:generate",
            "marketplace-plan-audience", 1, new { });
        using var mix = await CommandAsync(
            buyer, BuyerTenantId, $"brief-versions/{BuyerBriefVersionId}/media-mixes:generate",
            "marketplace-plan-mix", 1, new { });
        var mixId = mix.RootElement.GetProperty("id").GetGuid();
        using var editedMix = await CommandAsync(
            buyer, BuyerTenantId, $"media-mix-versions/{mixId}:update",
            "marketplace-plan-mix-update", 1, new
            {
                allocations = new[]
                {
                    new
                    {
                        channel = "OOH",
                        budgetMinor = 2_000_000,
                        role = "Primary Johannesburg awareness",
                        runningPeriods = new[]
                        {
                            new { start = "2026-09-01", end = "2026-09-30" },
                        },
                    },
                },
                reason = "Use the supplied OOH timing.",
            });
        using var approvedMix = await CommandAsync(
            buyer, BuyerTenantId, $"media-mix-versions/{mixId}:approve",
            "marketplace-plan-mix-approve", 2, new { reason = "Approved buyer mix." });
        using var shortlist = await CommandAsync(
            buyer, BuyerTenantId, $"brief-versions/{BuyerBriefVersionId}/shortlists:generate",
            "marketplace-plan-shortlist", 1, new { });
        var projection = shortlist.RootElement.GetRawText();
        Assert.DoesNotContain("sourceLocator", projection, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("address", projection, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("latitude", projection, StringComparison.OrdinalIgnoreCase);
        var candidate = Assert.Single(shortlist.RootElement.GetProperty("candidates")
            .EnumerateArray());
        Assert.Equal(SupplierTenantId,
            candidate.GetProperty("inventoryTenantId").GetGuid());
        Assert.Equal(listingVersionId,
            candidate.GetProperty("marketplaceListingVersionId").GetGuid());
        Assert.True(candidate.GetProperty("isEligible").GetBoolean());

        var shortlistId = shortlist.RootElement.GetProperty("id").GetGuid();
        var candidateId = candidate.GetProperty("id").GetGuid();
        using var selected = await CommandAsync(
            buyer, BuyerTenantId, $"shortlist-versions/{shortlistId}:select",
            "marketplace-plan-select", 1,
            new { selectedCandidateIds = new[] { candidateId }, reason = "Selected listing." });
        using var plan = await CommandAsync(
            buyer, BuyerTenantId, $"brief-versions/{BuyerBriefVersionId}/media-plans:generate",
            "marketplace-plan-generate", 1, new { });
        var line = Assert.Single(plan.RootElement.GetProperty("lines").EnumerateArray());
        Assert.Equal(SupplierTenantId, line.GetProperty("inventoryTenantId").GetGuid());
        Assert.Equal(listingVersionId,
            line.GetProperty("marketplaceListingVersionId").GetGuid());
        Assert.Equal("MARKETPLACE_LISTING", line.GetProperty("supplySource").GetString());

        var planId = plan.RootElement.GetProperty("id").GetGuid();
        var version = plan.RootElement.GetProperty("version").GetInt64();
        foreach (var objection in plan.RootElement.GetProperty("objections").EnumerateArray())
        {
            var code = objection.GetProperty("code").GetString()!;
            using var resolved = await CommandAsync(
                buyer, BuyerTenantId,
                $"media-plan-versions/{planId}/objections/{code}:resolve",
                $"marketplace-plan-resolve-{code.ToLowerInvariant()}", version,
                new
                {
                    resolution = "ACCEPTED_WITH_REASON",
                    reason = "Buyer reviewed the visible benchmark limitation.",
                });
            version = resolved.RootElement.GetProperty("version").GetInt64();
        }
        return new PlanFixture(
            planId, line.GetProperty("id").GetGuid(), version);
    }

    private static async Task AssertArchivedListingInvalidatesPlanAsync(
        HttpClient buyer,
        PlanFixture plan)
    {
        using var approval = await RawCommandAsync(
            buyer, BuyerTenantId, $"media-plan-versions/{plan.Id}:approve",
            "marketplace-plan-stale-approve", plan.Version,
            new { reason = "Archived supply must fail closed." });
        await AssertProblemAsync(
            approval, HttpStatusCode.Conflict, "PLANNING_INPUT_STALE");
    }

    private sealed record PlanFixture(Guid Id, Guid LineId, long Version);
}

using System.Net;
using System.Text.Json;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class MarketplaceAcceptanceTests
{
    public static IEnumerable<object[]> BackendMeasurementCases() =>
        BackendScenarioEvidence.Cases("MEASUREMENT_LEARNING");

    [Theory]
    [MemberData(nameof(BackendMeasurementCases))]
    [Trait("Category", "Migration")]
    public Task CanonicalMeasurementScenario(string scenarioId) =>
        BackendScenarioEvidence.RunAsync(scenarioId, () => RunDeliveryScenarioAsync(scenarioId));

    private static readonly string[] ScenarioMeasurementLimitations =
        ["Synthetic consented-device panel only; no causal attribution or population extrapolation."];

    private static async Task<BackendScenarioObservation> ExerciseMeasurementScenarioAsync(
        HttpClient buyer, HttpClient client, HttpClient other, ScenarioBookedCampaign booked, string id, object? memoryChange)
    {
        var campaignId = booked.CampaignId;
        if (id == "MEASURE-002")
            return await MissingProofMeasurementScenarioAsync(buyer, other, campaignId, id);
        var incompatible = id == "MEASURE-003";
        using var submitted = await CommandAsync(buyer, BuyerTenantId,
            $"campaigns/{campaignId}/performance-evidence", id + ":facts", null,
            PerformanceBody(ClientUserId, incompatible ? "UNUSABLE" : "VERIFIED", ValidMetric(), id,
                ScenarioMeasurementLimitations, methodology: incompatible
                    ? "Self-reported recall survey; incompatible with delivered-impression methodology."
                    : "Aggregated synthetic supplier delivery logs for the exact flight."));
        var evidenceId = submitted.RootElement.GetProperty("id").GetGuid();
        await AssertScenarioMeasurementBlockedAsync(buyer, campaignId, id + ":unapproved");
        if (incompatible)
        {
            using var unusableApproval = await RawCommandAsync(client, BuyerTenantId,
                $"campaigns/{campaignId}/performance-evidence/{evidenceId}:review", id + ":unusable", 1,
                new { approved = true, reason = "Unusable methodology must not become approved evidence." });
            await AssertProblemAsync(unusableApproval, HttpStatusCode.Conflict, "PERFORMANCE_EVIDENCE_BLOCKED");
        }
        using var reviewed = await CommandAsync(client, BuyerTenantId,
            $"campaigns/{campaignId}/performance-evidence/{evidenceId}:review", id + ":review-facts", 1,
            new { approved = !incompatible, reason = incompatible
                ? "Human rejects the incompatible source methodology." : "Human verifies exact method and limitations." });
        Assert.Equal(incompatible ? "REJECTED" : "APPROVED", reviewed.RootElement.GetProperty("status").GetString());
        Assert.Equal(ScenarioMeasurementLimitations,
            reviewed.RootElement.GetProperty("limitations").EnumerateArray().Select(value => value.GetString()).ToArray());
        object report;
        if (id is "MEASURE-002" or "MEASURE-003")
        {
            await AssertScenarioMeasurementBlockedAsync(buyer, campaignId, id + ":missing-input");
            report = new { blocked = true, missingProof = id == "MEASURE-002", incompatibleMethodology = incompatible };
        }
        else report = await ApproveScenarioMeasurementReportAsync(buyer, client, campaignId, evidenceId, id);
        using var denied = await other.GetAsync($"/api/v1/tenants/{OtherTenantId}/campaigns/{campaignId}");
        await AssertProblemAsync(denied, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");
        using var memory = await ReadAsync(buyer, BuyerTenantId, "reporting/commercial-memory?channel=OOH");
        var derived = FindScenarioMemoryMetric(memory.RootElement, "supplier_quote_versions_per_rfq");
        Assert.Equal(id == "MEASURE-005" ? 1.5m : 1m, derived.GetProperty("value").GetDecimal());
        Assert.NotEmpty(derived.GetProperty("sources").EnumerateArray());
        return new(new { campaignId, evidenceId, limitations = ScenarioMeasurementLimitations, incompatible },
            new { evidence = reviewed.RootElement.Clone(), report, memory = memory.RootElement.Clone(), memoryChange },
            id is "MEASURE-002" or "MEASURE-003" ? "MEASUREMENT_BLOCKED" : "MEASUREMENT_APPROVED",
            new Dictionary<string, bool>
            {
                ["APPROVED_EVIDENCE_ONLY"] = true,
                ["LIMITATIONS_PRESERVED"] = true,
                ["COMMERCIAL_MEMORY_DERIVED"] = true,
            }, ["SUPPLIER_PROOF_REVIEW", "INDEPENDENT_PERFORMANCE_REVIEW", "INDEPENDENT_REPORT_REVIEW"],
            0, null, "NO_AUTONOMOUS_COMMERCIAL_CHANGE", "CROSS_TENANT_CAMPAIGN_REJECTED");
    }

    private static async Task AssertScenarioMeasurementBlockedAsync(HttpClient buyer, Guid campaignId, string key)
    {
        using var blocked = await RawCommandAsync(buyer, BuyerTenantId,
            $"campaigns/{campaignId}/measurement-reports:generate", key, null,
            new { approverUserId = ClientUserId });
        await AssertProblemAsync(blocked, HttpStatusCode.Conflict, "MEASUREMENT_REPORT_BLOCKED");
    }

    private static async Task<JsonElement> ApproveScenarioMeasurementReportAsync(
        HttpClient buyer, HttpClient client, Guid campaignId, Guid evidenceId, string id)
    {
        using var generated = await CommandAsync(buyer, BuyerTenantId,
            $"campaigns/{campaignId}/measurement-reports:generate", id + ":report", null,
            new { approverUserId = ClientUserId });
        var report = generated.RootElement;
        var reportId = report.GetProperty("id").GetGuid();
        Assert.Equal(evidenceId, Assert.Single(report.GetProperty("evidence").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Equal("NOT_ESTABLISHED", report.GetProperty("interpretation").GetProperty("causalityStatus").GetString());
        Assert.Equal(ScenarioMeasurementLimitations,
            report.GetProperty("interpretation").GetProperty("limitations").EnumerateArray().Select(value => value.GetString()).ToArray());
        using var self = await RawCommandAsync(buyer, BuyerTenantId,
            $"campaigns/{campaignId}/measurement-reports/{reportId}:review", id + ":self-review", 1,
            new { approved = true, reason = "Report generator must not approve its output." });
        await AssertProblemAsync(self, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");
        using var approved = await CommandAsync(client, BuyerTenantId,
            $"campaigns/{campaignId}/measurement-reports/{reportId}:review", id + ":approve-report", 1,
            new { approved = true, reason = "Client approves exact evidence and retained limitations." });
        Assert.Equal("APPROVED", approved.RootElement.GetProperty("status").GetString());
        return approved.RootElement.Clone();
    }

    private static async Task<object> RecomputeScenarioMemoryAsync(
        HttpClient buyer, HttpClient supplier, HttpClient other, Guid listingVersionId, AdjustableMarketplaceClock clock)
    {
        using var before = await ReadAsync(buyer, BuyerTenantId, "reporting/commercial-memory?channel=OOH");
        var firstMetric = FindScenarioMemoryMetric(before.RootElement, "supplier_quote_versions_per_rfq");
        Assert.Equal(1m, firstMetric.GetProperty("value").GetDecimal());
        await ExerciseQuoteScenarioAsync(buyer, supplier, other, listingVersionId, clock, "TXN-008");
        using var after = await ReadAsync(buyer, BuyerTenantId, "reporting/commercial-memory?channel=OOH");
        var metric = FindScenarioMemoryMetric(after.RootElement, "supplier_quote_versions_per_rfq");
        Assert.Equal(1.5m, metric.GetProperty("value").GetDecimal());
        Assert.Equal(2, metric.GetProperty("sampleSize").GetInt32());
        Assert.NotEmpty(metric.GetProperty("sources").EnumerateArray());
        using var repeated = await ReadAsync(buyer, BuyerTenantId, "reporting/commercial-memory?channel=OOH");
        Assert.Equal(metric.GetRawText(), FindScenarioMemoryMetric(repeated.RootElement,
            "supplier_quote_versions_per_rfq").GetRawText());
        return new { before = firstMetric.Clone(), recomputed = metric.Clone(), canonicalRfqCount = 2, quoteVersionCount = 3 };
    }

    private static JsonElement FindScenarioMemoryMetric(JsonElement memory, string code) =>
        Assert.Single(memory.GetProperty("metrics").EnumerateArray(), item => item.GetProperty("code").GetString() == code);
}

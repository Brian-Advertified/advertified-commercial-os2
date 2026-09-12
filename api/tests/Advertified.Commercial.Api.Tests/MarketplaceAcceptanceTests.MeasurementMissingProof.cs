using System.Net;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class MarketplaceAcceptanceTests
{
    private static async Task<BackendScenarioObservation> MissingProofMeasurementScenarioAsync(
        HttpClient buyer, HttpClient other, Guid campaignId, string id)
    {
        var input = PerformanceBody(ClientUserId, "VERIFIED", ValidMetric(), id, ScenarioMeasurementLimitations);
        using var facts = await RawCommandAsync(buyer, BuyerTenantId,
            $"campaigns/{campaignId}/performance-evidence", id + ":facts", null, input);
        await AssertProblemAsync(facts, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");
        await AssertScenarioMeasurementBlockedAsync(buyer, campaignId, id + ":report");
        using var current = await ReadAsync(buyer, BuyerTenantId, $"campaigns/{campaignId}");
        Assert.Empty(current.RootElement.GetProperty("performanceEvidence").EnumerateArray());
        using var denied = await other.GetAsync($"/api/v1/tenants/{OtherTenantId}/campaigns/{campaignId}");
        await AssertProblemAsync(denied, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");
        using var memory = await ReadAsync(buyer, BuyerTenantId, "reporting/commercial-memory?channel=OOH");
        Assert.Equal(1m, FindScenarioMemoryMetric(memory.RootElement,
            "supplier_quote_versions_per_rfq").GetProperty("value").GetDecimal());
        return new(new { campaignId, attemptedEvidence = input, missingApprovedProof = true },
            new { campaign = current.RootElement.Clone(), evidenceSubmissionStatus = 403,
                reportGenerationStatus = 409, memory = memory.RootElement.Clone() },
            "MEASUREMENT_BLOCKED",
            new Dictionary<string, bool>
            {
                ["APPROVED_EVIDENCE_ONLY"] = true,
                ["LIMITATIONS_PRESERVED"] = true,
                ["COMMERCIAL_MEMORY_DERIVED"] = true,
            }, ["SUPPLIER_PROOF_REQUIRED_BEFORE_PERFORMANCE_EVIDENCE"],
            0, 1, "NO_COMMERCIAL_MUTATION_FROM_UNAPPROVED_EVIDENCE", "CROSS_TENANT_CAMPAIGN_REJECTED");
    }
}

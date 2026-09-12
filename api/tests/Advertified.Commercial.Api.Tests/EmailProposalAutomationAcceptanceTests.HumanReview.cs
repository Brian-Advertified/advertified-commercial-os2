using System.Text.Json;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class CanonicalPlanningAcceptanceTests
{
    // Transport tests cross real human command boundaries before exercising a provider failure.
    private static async Task CompleteEmailHumanReviewsAsync(HttpClient client, Guid inboundEmailId,
        bool stopBeforeDelivery = false)
    {
        for (var stage = 0; stage < 3; stage++)
        {
            using var detail = await GetJsonAsync(client, Path($"email-automation/messages/{inboundEmailId}"));
            var run = detail.RootElement.GetProperty("run");
            var failure = run.GetProperty("failureCode").GetString();
            if (failure == "STP_UNREADY")
                await ApproveEmailAudienceAsync(client, run);
            else if (failure == "PROPOSAL_UNREADY" && run.GetProperty("proposalVersionId").ValueKind != JsonValueKind.Null)
                await AuthoriseEmailBrandingAsync(client, run);
            else return;
            if (failure == "PROPOSAL_UNREADY" && stopBeforeDelivery) return;
            using var resumed = await CommandAsync(client, Path($"email-automation/messages/{inboundEmailId}:retry"),
                $"human-review-resume-{inboundEmailId}-{stage}", run.GetProperty("version").GetInt64(),
                new { reason = "Human completed the exact audience or branding review.", clarifications = Array.Empty<object>() });
        }
    }

    private static async Task ApproveEmailAudienceAsync(HttpClient client, JsonElement run)
    {
        var briefId = run.GetProperty("briefVersionId").GetGuid();
        using var workspace = await GetJsonAsync(client, Path($"brief-versions/{briefId}/planning"));
        var audience = workspace.RootElement.GetProperty("audience");
        var status = audience.GetProperty("status").GetString();
        if (status == "APPROVED") return;
        Assert.Equal("DRAFT", status);
        var id = audience.GetProperty("id").GetGuid();
        using var approved = await CommandAsync(client, Path($"audience-strategies/{id}:approve"),
            $"email-human-audience-{id}", audience.GetProperty("version").GetInt64(), new
            {
                targetAudienceIds = audience.GetProperty("targetAudienceIds").EnumerateArray().Select(value => value.GetGuid()).ToArray(),
                targetingRationale = audience.GetProperty("targetingRationale").GetString(),
                positioningStatement = audience.GetProperty("positioningStatement").ValueKind == JsonValueKind.Null
                    ? "Human-approved direction: make the supplied business furniture offer easy for the approved audience to understand and act on."
                    : audience.GetProperty("positioningStatement").GetString(),
                reason = "The human fixture operator reviewed this exact audience proposal and supplied any missing positioning direction.",
            });
        Assert.Equal("APPROVED", approved.RootElement.GetProperty("status").GetString());
        Assert.Equal(OperatorId, approved.RootElement.GetProperty("approvedBy").GetGuid());
    }

    private static async Task AuthoriseEmailBrandingAsync(HttpClient client, JsonElement run)
    {
        var id = run.GetProperty("proposalVersionId").GetGuid();
        using var proposal = await GetJsonAsync(client, Path($"proposals/{id}"));
        Assert.Equal("DRAFT", proposal.RootElement.GetProperty("status").GetString());
        using var approved = await CommandAsync(client, Path($"proposal-versions/{id}:approve-unbranded"),
            $"email-human-branding-{id}", proposal.RootElement.GetProperty("version").GetInt64(),
            new { reason = "The human fixture operator explicitly authorises this synthetic unbranded document." });
        Assert.Equal("UNBRANDED_AUTHORISED", approved.RootElement.GetProperty("branding").GetProperty("status").GetString());
    }
}

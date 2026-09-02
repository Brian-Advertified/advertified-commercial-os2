using System.Net;
using System.Text.Json;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class CanonicalPlanningAcceptanceTests
{
    private static async Task AssertExternalDecisionRecordingAsync(
        HttpClient operatorClient,
        HttpClient reviewerClient,
        JsonElement automationRun)
    {
        var proposalVersionId = automationRun.GetProperty("proposalVersionId").GetGuid();
        using var proposal = await GetJsonAsync(
            operatorClient, Path($"proposals/{proposalVersionId}"));
        var version = proposal.RootElement.GetProperty("version").GetInt64();
        var optionId = proposal.RootElement.GetProperty("options")[0]
            .GetProperty("id").GetGuid();
        var decision = new
        {
            optionId,
            declined = false,
            evidenceReference = "provider-reply:message-ooh-001",
            reason = "The client selected this route in the retained reply.",
        };

        using var denied = await RawCommandAsync(
            reviewerClient,
            Path($"proposal-versions/{proposalVersionId}:record-external-decision"),
            "email-decision-wrong-recorder",
            version,
            decision);
        await AssertProblemAsync(denied, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");

        using var recorded = await CommandAsync(
            operatorClient,
            Path($"proposal-versions/{proposalVersionId}:record-external-decision"),
            "email-decision-mailbox-owner",
            version,
            decision);
        Assert.Equal("SELECTED", recorded.RootElement.GetProperty("status").GetString());
        var recordedDecision = recorded.RootElement.GetProperty("decision");
        Assert.Equal(optionId, recordedDecision.GetProperty("optionId").GetGuid());
        Assert.True(recordedDecision.GetProperty("recordedForExternalParty").GetBoolean());
        Assert.Equal("brief@client.example",
            recordedDecision.GetProperty("externalPartyEmail").GetString());
        Assert.Equal("provider-reply:message-ooh-001",
            recordedDecision.GetProperty("evidenceReference").GetString());
        Assert.Equal(OperatorId, recordedDecision.GetProperty("decidedBy").GetGuid());
    }
}

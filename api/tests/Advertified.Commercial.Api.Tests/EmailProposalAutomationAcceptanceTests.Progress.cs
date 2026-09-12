using System.Text.Json;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class CanonicalPlanningAcceptanceTests
{
    private static async Task AssertEmailAutomationProgressAsync(
        HttpClient client,
        Guid automationRunId)
    {
        var path = Path($"agent-operations/{automationRunId}");
        using var response = await GetJsonAsync(client, path);
        var root = response.RootElement;
        Assert.Equal("email_proposal_automation_run",
            root.GetProperty("runKind").GetString());
        Assert.Equal("email_proposal_automation_run",
            root.GetProperty("subject").GetProperty("resourceType").GetString());
        Assert.Equal(automationRunId,
            root.GetProperty("subject").GetProperty("resourceId").GetGuid());
        Assert.Equal("SENT", root.GetProperty("status").GetString());
        Assert.Equal("SENT", root.GetProperty("currentStep").GetString());
        Assert.Equal(JsonValueKind.Null,
            root.GetProperty("reviewRequiredStep").ValueKind);
        Assert.Contains(
            root.GetProperty("completedSteps").EnumerateArray(),
            item => item.GetString() == "email_automation.sent");
        Assert.Equal(0, root.GetProperty("incrementalCostMinor").GetInt64());
        Assert.Contains(root.GetProperty("steps").EnumerateArray(), step =>
            step.GetProperty("stepCode").GetString() == "SENT" &&
            step.GetProperty("status").GetString() == "SENT");

        using var replay = await GetJsonAsync(client, path);
        Assert.Equal(root.GetProperty("updatedAtUtc").GetDateTimeOffset(),
            replay.RootElement.GetProperty("updatedAtUtc").GetDateTimeOffset());
    }
}

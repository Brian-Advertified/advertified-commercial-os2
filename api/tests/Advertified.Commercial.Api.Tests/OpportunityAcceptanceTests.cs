using System.Net;
using System.Text.Json;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class OpportunityAcceptanceTests
{
    [Fact]
    [Trait("Category", "Migration")]
    public async Task OpportunityAndSuppliedPathsProduceCanonicalHumanConfirmedBriefs()
    {
        await using var postgres = CreatePostgres();
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DisposableDatabaseRoles.ProvisionAsync(connectionString);
        await SeedAsync(connectionString);

        await using var ownerFactory = CreateFactory(connectionString, OwnerId, enableRuntime: true);
        await using var reviewerFactory = CreateFactory(connectionString, ReviewerId);
        await using var approverFactory = CreateFactory(connectionString, ApproverId);
        await using var soloFactory = CreateFactory(connectionString, SoloOperatorId);
        using var owner = ownerFactory.CreateClient();
        using var reviewer = reviewerFactory.CreateClient();
        using var approver = approverFactory.CreateClient();
        using var solo = soloFactory.CreateClient();

        var clientId = await CreateClientAsync(owner);
        var opportunity = await CreateOpportunityAsync(owner, clientId);
        var opportunityId = opportunity.GetProperty("id").GetGuid();
        await AssertCaptureBoundaryAsync(owner, opportunityId);
        await RegisterEvidenceAsync(owner, opportunityId);
        await StartQualificationAsync(owner, opportunityId, 1);

        var detail = await GetOpportunityAsync(owner, opportunityId);
        var evidenceItem = detail.GetProperty("evidenceItems")[0];
        var itemId = evidenceItem.GetProperty("id").GetGuid();
        using var selfReview = await SendCommandAsync(
            owner,
            $"/api/v1/tenants/{TenantId}/evidence-items/{itemId}/review",
            "opportunity-self-review",
            new { decision = "APPROVE", structuredValueJson = (string?)null, reason = (string?)null },
            1);
        await AssertProblemAsync(selfReview, HttpStatusCode.Forbidden, "APPROVAL_REQUIRED");

        await ReviewEvidenceAsync(reviewer, itemId);
        await SubmitAndApproveEvidenceAsync(owner, reviewer, opportunityId);
        await QueueAsync(owner, opportunityId, "interpret", "opportunity-interpret", new { });
        detail = await WaitForAsync(
            owner,
            opportunityId,
            value => value.GetProperty("interpretation").ValueKind == JsonValueKind.Object);
        var interpretation = detail.GetProperty("interpretation");
        await SendSuccessfulCommandAsync(
            owner,
            $"/api/v1/tenants/{TenantId}/business-interpretations/" +
                $"{interpretation.GetProperty("id").GetGuid()}:confirm",
            "opportunity-confirm-interpretation",
            new { comment = "Confirmed against the approved evidence." },
            interpretation.GetProperty("version").GetInt64());

        await QueueAsync(owner, opportunityId, "angles:generate", "opportunity-angles", new { });
        detail = await WaitForAsync(
            owner,
            opportunityId,
            value => value.GetProperty("angles").GetArrayLength() >= 2);
        var angle = detail.GetProperty("angles")[0];
        await SendSuccessfulCommandAsync(
            owner,
            $"/api/v1/tenants/{TenantId}/opportunity-angles/" +
                $"{angle.GetProperty("id").GetGuid()}:select",
            "opportunity-select-angle",
            new { reason = "Best supported route to measurable demand." },
            angle.GetProperty("version").GetInt64());

        await QueueAsync(
            owner,
            opportunityId,
            "strategies:generate",
            "opportunity-strategy",
            new { approverUserId = ApproverId });
        detail = await WaitForAsync(
            owner,
            opportunityId,
            value => value.GetProperty("strategy").ValueKind == JsonValueKind.Object &&
                value.GetProperty("strategy").GetProperty("objections").GetArrayLength() > 0);
        var strategy = detail.GetProperty("strategy");
        var objection = strategy.GetProperty("objections")[0];
        await ResolveSubmitAndApproveStrategyAsync(
            owner, approver, strategy, objection, opportunityId);

        detail = await GetOpportunityAsync(owner, opportunityId);
        Assert.Equal("BRIEF_READY", detail.GetProperty("opportunity")
            .GetProperty("stage").GetString());
        Assert.Equal("Draft the campaign brief.",
            detail.GetProperty("nextAction").GetString());
        await AssertDurableLineageAsync(connectionString, opportunityId);
        await AssertSuppliedBriefPathAsync(solo, clientId);
        await AssertOpportunityBriefPathAsync(
            owner, solo, approver, opportunityId, connectionString);
    }
}

using System.Text.Json;
using Advertified.Commercial.Infrastructure.Planning;
using Advertified.Commercial.Infrastructure.Worker;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class MarketplaceAcceptanceTests
{
    private static readonly Guid OldReleaseId =
        Guid.Parse("97000000-0000-0000-0000-000000000001");
    private static readonly Guid ReplacementImportId =
        Guid.Parse("97000000-0000-0000-0000-000000000002");
    private static readonly Guid ReplacementCandidateId =
        Guid.Parse("97000000-0000-0000-0000-000000000003");
    private static readonly Guid ReplacementProductVersionId =
        Guid.Parse("97000000-0000-0000-0000-000000000004");
    private static readonly Guid ReplacementRateId =
        Guid.Parse("97000000-0000-0000-0000-000000000005");
    private static readonly Guid ReplacementAvailabilityId =
        Guid.Parse("97000000-0000-0000-0000-000000000006");

    [Fact]
    [Trait("Category", "Migration")]
    public async Task SupplierReleaseProducesOneRestartSafeHumanReviewedReplan()
    {
        await using var postgres = CreatePostgres();
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DisposableDatabaseRoles.ProvisionAsync(connectionString);
        await SeedAsync(connectionString);
        var clock = new AdjustableMarketplaceClock(InitialTime);
        await using var supplierFactory = CreateFactory(
            connectionString, SupplierUserId, clock);
        await using var buyerFactory = CreateFactory(
            connectionString, BuyerUserId, clock);
        using var supplier = supplierFactory.CreateClient();
        using var buyer = buyerFactory.CreateClient();

        var listing = await CreateAndPublishListingAsync(supplier, buyer);
        var plan = await BuildBuyerPlanAsync(buyer, listing.ListingVersionId);
        using var approvedPlan = await CommandAsync(
            buyer, BuyerTenantId, $"media-plan-versions/{plan.Id}:approve",
            "replan-plan-approve", plan.Version,
            new { reason = "Approve the original current supplier placement." });
        var proposalId = await CreateSharedProposalForReplanAsync(
            buyer, plan.Id, clock);
        var before = await ReadHistoricalReplanStateAsync(
            connectionString, proposalId, plan.Id);

        clock.Advance(TimeSpan.FromHours(1));
        var replacementReleaseId = await PublishReplacementReleaseAsync(
            connectionString, clock.GetUtcNow());
        await AssertReleaseRegisteredOneReplanAsync(
            connectionString, proposalId, replacementReleaseId);

        var scheduler = new WorkerSchedulerStore(connectionString);
        var firstClaim = await scheduler.ClaimProposalReplanAsync(
            Guid.NewGuid(), 120, CancellationToken.None);
        Assert.NotNull(firstClaim);
        var firstAssessment = await AssessReplanAsync(connectionString, firstClaim);
        Assert.Equal("completed", await scheduler.CompleteProposalReplanAsync(
            firstClaim.ClaimToken, firstClaim.SourceGeneration, true,
            firstAssessment.ProposedRevisionJson, firstAssessment.ComparisonJson,
            null, null, 1, 5, CancellationToken.None));
        AssertReplanAssessment(
            firstAssessment, continuityCandidates: 0, reselectionRequired: 1,
            expectedCurrent: false);

        using var archived = await ReadAsync(
            supplier, SupplierTenantId,
            $"marketplace-listings/{listing.ListingId}");
        Assert.Equal("ARCHIVED", archived.RootElement.GetProperty("status").GetString());
        using var reopened = await CommandAsync(
            supplier, SupplierTenantId,
            $"marketplace-listings/{listing.ListingId}:relist",
            "replan-listing-reopen",
            archived.RootElement.GetProperty("version").GetInt64(),
            new { terms = "Replacement release available for buyer review." });
        Assert.Equal(listing.ListingId, reopened.RootElement.GetProperty("id").GetGuid());
        Assert.Equal("DRAFT", reopened.RootElement.GetProperty("status").GetString());
        var reopenedVersion = reopened.RootElement.GetProperty("version").GetInt64();
        using var republished = await CommandAsync(
            supplier, SupplierTenantId,
            $"marketplace-listings/{listing.ListingId}:publish",
            "replan-listing-republish", reopenedVersion, new { });
        Assert.Equal(ReplacementRateId,
            republished.RootElement.GetProperty("currentVersion")
                .GetProperty("rateId").GetGuid());

        var staleClaim = await scheduler.ClaimProposalReplanAsync(
            Guid.NewGuid(), 120, CancellationToken.None);
        Assert.NotNull(staleClaim);
        using var racePublish = await CommandAsync(
            supplier, SupplierTenantId,
            $"marketplace-listings/{listing.ListingId}:publish",
            "replan-listing-race-publish",
            republished.RootElement.GetProperty("version").GetInt64(), new { });
        var staleAssessment = await AssessReplanAsync(connectionString, staleClaim);
        Assert.Equal("superseded_by_newer_fact",
            await scheduler.CompleteProposalReplanAsync(
                staleClaim.ClaimToken, staleClaim.SourceGeneration, true,
                staleAssessment.ProposedRevisionJson, staleAssessment.ComparisonJson,
                null, null, 1, 5, CancellationToken.None));

        var currentClaim = await scheduler.ClaimProposalReplanAsync(
            Guid.NewGuid(), 120, CancellationToken.None);
        Assert.NotNull(currentClaim);
        Assert.True(currentClaim.SourceGeneration > staleClaim.SourceGeneration);
        var currentAssessment = await AssessReplanAsync(
            connectionString, currentClaim);
        Assert.Equal("completed", await scheduler.CompleteProposalReplanAsync(
            currentClaim.ClaimToken, currentClaim.SourceGeneration, true,
            currentAssessment.ProposedRevisionJson, currentAssessment.ComparisonJson,
            null, null, 1, 5, CancellationToken.None));
        AssertReplanAssessment(
            currentAssessment, continuityCandidates: 1,
            reselectionRequired: 0, expectedCurrent: true);

        var finalReplan = await AssertFinalReplanStateAsync(
            connectionString, proposalId, replacementReleaseId,
            expectedGeneration: currentClaim.SourceGeneration);
        await AssertReplanProgressAsync(buyer, finalReplan.Id);
        await AssertHistoricalReplanStateUnchangedAsync(
            connectionString, proposalId, plan.Id, before);
    }

    private static async Task AssertReplanProgressAsync(
        HttpClient buyer,
        Guid replanId)
    {
        var path = $"agent-operations/{replanId}";
        using var json = await ReadAsync(buyer, BuyerTenantId, path);
        var root = json.RootElement;
        Assert.Equal("proposal_replan_revision", root.GetProperty("runKind").GetString());
        Assert.Equal("proposal_replan_revision",
            root.GetProperty("subject").GetProperty("resourceType").GetString());
        Assert.Equal(replanId,
            root.GetProperty("subject").GetProperty("resourceId").GetGuid());
        Assert.Equal("REVIEW_REQUIRED", root.GetProperty("status").GetString());
        Assert.Equal("proposal_replan_revision",
            root.GetProperty("reviewRequiredStep").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("correlationId").ValueKind);
        Assert.Equal(0, root.GetProperty("incrementalCostMinor").GetInt64());
        var step = Assert.Single(root.GetProperty("steps").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, step.GetProperty("agentCode").ValueKind);
        using var replayJson = await ReadAsync(buyer, BuyerTenantId, path);
        Assert.Equal(root.GetProperty("updatedAtUtc").GetDateTimeOffset(),
            replayJson.RootElement.GetProperty("updatedAtUtc").GetDateTimeOffset());
    }

    private static void AssertReplanAssessment(
        ProposalReplanAssessmentResult assessment,
        int continuityCandidates,
        int reselectionRequired,
        bool expectedCurrent)
    {
        using var proposed = JsonDocument.Parse(assessment.ProposedRevisionJson);
        Assert.True(proposed.RootElement.GetProperty("requiresHumanApproval").GetBoolean());
        Assert.True(proposed.RootElement.GetProperty("requiresHumanSelection").GetBoolean());
        Assert.True(
            proposed.RootElement.GetProperty("continuityCandidateCount").GetInt32() ==
                continuityCandidates,
            assessment.ProposedRevisionJson);
        Assert.True(
            proposed.RootElement.GetProperty("reselectionRequiredCount").GetInt32() ==
                reselectionRequired,
            assessment.ProposedRevisionJson);
        var line = Assert.Single(proposed.RootElement.GetProperty("lines").EnumerateArray());
        Assert.Equal(expectedCurrent,
            line.GetProperty("triggeredReplacementStillCurrent").GetBoolean());
        Assert.True(line.GetProperty("requiresHumanSelection").GetBoolean());
    }
}

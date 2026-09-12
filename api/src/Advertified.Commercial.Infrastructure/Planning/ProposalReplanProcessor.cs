using System.Text.Json;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Worker;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Planning;

public sealed class ProposalReplanProcessor(
    PlanningRecordStore store,
    PlanningPolicy planningPolicy)
{
    private static readonly JsonSerializerOptions StoredJson =
        new(JsonSerializerDefaults.Web);

    public async Task<ProposalReplanAssessmentResult> AssessAsync(
        ProposalReplanWorkerClaim claim,
        CancellationToken cancellationToken)
    {
        var tenantId = new TenantId(claim.TenantId);
        await using var transaction = await store.BeginSessionAsync(
            new ActorId(claim.ReviewOwnerUserId), tenantId, cancellationToken);
        var impactIds = Read<Guid[]>(claim.AffectedImpactIdsJson);
        var impacts = await LoadImpactsAsync(
            tenantId, claim.SourceProposalVersionId, impactIds, cancellationToken);
        EnsureClaimMatches(claim, impactIds, impacts);
        var briefVersionId = impacts.Select(item => item.BriefVersionId)
            .Distinct().Single();
        var brief = await store.FindBriefAsync(
            tenantId, briefVersionId, cancellationToken)
            ?? throw new InvalidOperationException("The affected Brief is unavailable.");
        var inventory = await store.ListInventoryAsync(
            tenantId,
            cancellationToken,
            impacts.Select(item => item.OldProductId).Distinct().ToArray());
        var spatial = await store.EvaluateSpatialMatchesAsync(
            tenantId, briefVersionId, inventory, cancellationToken);
        var contexts = await LoadMixContextsAsync(
            tenantId, impacts, cancellationToken);
        var lines = impacts.Select(impact => AssessLine(
            claim, impact, brief, inventory, spatial, contexts)).ToArray();
        await transaction.CommitAsync(cancellationToken);
        return BuildResult(claim, lines);
    }

    private async Task<Dictionary<Guid, ReplanMixContext>> LoadMixContextsAsync(
        TenantId tenantId,
        IReadOnlyList<ProposalReplanImpactRow> impacts,
        CancellationToken cancellationToken)
    {
        var contexts = new Dictionary<Guid, ReplanMixContext>();
        foreach (var mixId in impacts.Select(item => item.MixVersionId).Distinct())
        {
            var mix = await store.FindMixAsync(tenantId, mixId, cancellationToken)
                ?? throw new InvalidOperationException("The affected media mix is unavailable.");
            var audienceRow = await store.FindAudienceAsync(
                tenantId, mix.AudienceArtifactId, cancellationToken)
                ?? throw new InvalidOperationException("The affected audience is unavailable.");
            var audience = PlanningRecordStore.BuildAudienceView(audienceRow);
            var targetIds = audience.TargetAudienceIds.ToHashSet();
            var targets = audience.Definitions
                .Where(item => targetIds.Contains(item.Id)).ToArray();
            if (targets.Length != targetIds.Count)
                throw new InvalidOperationException("The affected audience set is incomplete.");
            contexts[mixId] = new ReplanMixContext(
                mix,
                Read<MediaAllocationView[]>(mix.AllocationsJson)
                    .ToDictionary(item => item.Channel, StringComparer.Ordinal),
                targets);
        }
        return contexts;
    }

    private ProposalReplanLineAssessment AssessLine(
        ProposalReplanWorkerClaim claim,
        ProposalReplanImpactRow impact,
        PlanningBriefRow brief,
        IReadOnlyList<PlanningInventoryRow> inventory,
        IReadOnlyDictionary<PlanningInventoryKey, InventorySpatialMatchView> spatial,
        Dictionary<Guid, ReplanMixContext> contexts)
    {
        var current = inventory.SingleOrDefault(item =>
            item.InventoryTenantId == claim.InventoryTenantId &&
            item.ProductId == impact.OldProductId);
        if (current is null)
            return Missing(impact, "CURRENT_SUPPLY_NOT_PUBLISHED");
        if (impact.ReplacementProductId != impact.OldProductId ||
            current.ProductVersionId != impact.ReplacementProductVersionId ||
            current.RateId != impact.ReplacementRateId ||
            current.AvailabilityId != impact.ReplacementAvailabilityId)
            return Missing(impact, "TRIGGERED_REPLACEMENT_IS_NO_LONGER_CURRENT");

        var context = contexts[impact.MixVersionId];
        var allocation = context.Allocations.GetValueOrDefault(current.Channel);
        if (allocation is null)
            return Ineligible(impact, current,
                MasterDataCodes.RejectionReasons.IneligibleFormat,
                "The replacement channel is not present in the approved media mix.");
        var periods = Read<MediaRunningPeriodView[]>(impact.RunningPeriodsJson);
        var originalPurchase = ReadNullable<InventoryPurchaseQuantity>(impact.PurchaseJson);
        if (originalPurchase is not null &&
            (current.RateId is null || string.IsNullOrWhiteSpace(current.RateType)))
            return Ineligible(impact, current,
                MasterDataCodes.RejectionReasons.MissingInfo,
                "The current replacement rate has no complete buying identity.");
        var replacementPurchase = originalPurchase is null ? null : new InventoryPurchaseQuantity(
            current.InventoryTenantId,
            current.ProductId,
            current.ProductVersionId,
            current.RateId!.Value,
            current.RateType!,
            originalPurchase.Quantity,
            originalPurchase.Denominator);
        var evaluationAllocation = ContinuityAllocation(
            allocation, impact.OldProductId, periods, replacementPurchase);
        var evaluationAllocations = context.Allocations.ToDictionary(
            item => item.Key,
            item => item.Key == current.Channel ? evaluationAllocation : item.Value,
            StringComparer.Ordinal);
        var geographies = Read<string[]>(brief.GeographiesJson);
        var constraints = Read<string[]>(brief.ConstraintsJson);
        var key = new PlanningInventoryKey(
            current.InventoryTenantId,
            current.MarketplaceListingVersionId,
            current.ProductVersionId);
        var spatialMatch = spatial[key];
        var eligibility = InventoryEligibilityEvaluator.Evaluate(
            current, geographies, constraints, evaluationAllocations,
            context.Mix.Currency, planningPolicy, spatialMatch.HasRequirements);
        eligibility = PlanningSpatialMatcher.ApplyEligibility(
            eligibility, spatialMatch);
        var audienceFit = InventoryAudienceMatcher.Evaluate(
            current.AudienceProfileJson, context.Targets);
        eligibility = InventoryAudienceMatcher.ApplyMandatoryEligibility(
            eligibility, audienceFit);
        if (!eligibility.IsEligible)
            return Ineligible(
                impact, current, eligibility.RejectionReason, eligibility.RejectionDetail);

        var price = SupplierRateCalculator.Calculate(
            current, periods, planningPolicy, replacementPurchase);
        return new ProposalReplanLineAssessment(
            impact.ImpactId,
            impact.MediaPlanLineId,
            impact.OldProductId,
            impact.OldProductVersionId,
            impact.OldRateId,
            impact.OldAvailabilityId,
            current.ProductId,
            current.ProductVersionId,
            current.RateId,
            current.AvailabilityId,
            current.Name,
            current.Channel,
            current.Geography,
            current.Currency,
            current.RateAmountMinor,
            impact.OldSupplierCostMinor,
            price.PayableMinor,
            price.PayableMinor - impact.OldSupplierCostMinor,
            true,
            null,
            null,
            RequiresHumanSelection: true,
            TriggeredReplacementStillCurrent: true);
    }

    private static MediaAllocationView ContinuityAllocation(
        MediaAllocationView allocation,
        Guid oldProductId,
        IReadOnlyList<MediaRunningPeriodView> periods,
        InventoryPurchaseQuantity? replacementPurchase)
    {
        var purchases = (allocation.Purchases ?? [])
            .Where(item => item.InventoryProductId != oldProductId)
            .ToList();
        if (replacementPurchase is not null)
            purchases.Add(replacementPurchase);
        return allocation with
        {
            RunningPeriods = periods,
            Purchases = purchases.Count == 0 ? null : purchases,
        };
    }

    private static ProposalReplanLineAssessment Missing(
        ProposalReplanImpactRow impact,
        string reason) => new(
        impact.ImpactId, impact.MediaPlanLineId,
        impact.OldProductId, impact.OldProductVersionId,
        impact.OldRateId, impact.OldAvailabilityId,
        impact.ReplacementProductId, impact.ReplacementProductVersionId,
        impact.ReplacementRateId, impact.ReplacementAvailabilityId,
        null, null, null, null, null,
        impact.OldSupplierCostMinor, null, null,
        false, MasterDataCodes.RejectionReasons.MissingInfo, reason,
        RequiresHumanSelection: true,
        TriggeredReplacementStillCurrent: false);

    private static ProposalReplanLineAssessment Ineligible(
        ProposalReplanImpactRow impact,
        PlanningInventoryRow current,
        string? reason,
        string? detail) => new(
        impact.ImpactId, impact.MediaPlanLineId,
        impact.OldProductId, impact.OldProductVersionId,
        impact.OldRateId, impact.OldAvailabilityId,
        current.ProductId, current.ProductVersionId,
        current.RateId, current.AvailabilityId,
        current.Name, current.Channel, current.Geography,
        current.Currency, current.RateAmountMinor,
        impact.OldSupplierCostMinor, null, null,
        false, reason, detail,
        RequiresHumanSelection: true,
        TriggeredReplacementStillCurrent: true);

    private static ProposalReplanAssessmentResult BuildResult(
        ProposalReplanWorkerClaim claim,
        ProposalReplanLineAssessment[] lines)
    {
        var continuity = lines.Count(item => item.ContinuityCandidate);
        var proposed = JsonSerializer.Serialize(new
        {
            sourceProposalVersionId = claim.SourceProposalVersionId,
            replacementReleaseId = claim.ReplacementReleaseId,
            requiresHumanApproval = true,
            requiresHumanSelection = true,
            continuityCandidateCount = continuity,
            reselectionRequiredCount = lines.Length - continuity,
            lines,
        }, StoredJson);
        var comparison = JsonSerializer.Serialize(new
        {
            affectedLineCount = lines.Length,
            continuityCandidateCount = continuity,
            reselectionRequiredCount = lines.Length - continuity,
            supplierCostDeltaMinor = lines
                .Where(item => item.SupplierCostDeltaMinor.HasValue)
                .Sum(item => item.SupplierCostDeltaMinor!.Value),
            allTriggeredReplacementsStillCurrent = lines.All(
                item => item.TriggeredReplacementStillCurrent),
        }, StoredJson);
        return new ProposalReplanAssessmentResult(proposed, comparison);
    }

    private Task<List<ProposalReplanImpactRow>> LoadImpactsAsync(
        TenantId tenantId,
        Guid proposalVersionId,
        Guid[] impactIds,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.SqlQuery<ProposalReplanImpactRow>($"""
            SELECT impact.id AS "ImpactId",
                impact.media_plan_line_id AS "MediaPlanLineId",
                impact.old_product_id AS "OldProductId",
                impact.old_product_version_id AS "OldProductVersionId",
                impact.old_rate_id AS "OldRateId",
                impact.old_availability_id AS "OldAvailabilityId",
                impact.replacement_product_id AS "ReplacementProductId",
                impact.replacement_product_version_id AS "ReplacementProductVersionId",
                impact.replacement_rate_id AS "ReplacementRateId",
                impact.replacement_availability_id AS "ReplacementAvailabilityId",
                plan.brief_version_id AS "BriefVersionId",
                plan.mix_version_id AS "MixVersionId",
                line.running_periods_json::text AS "RunningPeriodsJson",
                line.purchase_json::text AS "PurchaseJson",
                line.supplier_cost_minor AS "OldSupplierCostMinor"
            FROM commercial.proposal_inventory_impacts impact
            JOIN commercial.proposal_options option
              ON option.tenant_id = impact.tenant_id
             AND option.id = impact.proposal_option_id
            JOIN commercial.media_plan_versions plan
              ON plan.tenant_id = option.tenant_id
             AND plan.id = option.plan_version_id
            JOIN commercial.media_plan_lines line
              ON line.tenant_id = impact.tenant_id
             AND line.id = impact.media_plan_line_id
            WHERE impact.tenant_id = {tenantId.Value}
              AND impact.proposal_version_id = {proposalVersionId}
              AND impact.id = ANY({impactIds})
              AND impact.status_code = {MasterDataCodes.ProposalInventoryImpactStatuses.Open}
            ORDER BY impact.id
            """).ToListAsync(cancellationToken);

    private static void EnsureClaimMatches(
        ProposalReplanWorkerClaim claim,
        Guid[] expectedIds,
        List<ProposalReplanImpactRow> impacts)
    {
        if (expectedIds.Length == 0 || impacts.Count != expectedIds.Length ||
            impacts.Any(item => item.ReplacementProductId.HasValue &&
                claim.InventoryTenantId == Guid.Empty))
            throw new InvalidOperationException("Proposal replan impact scope changed.");
    }

    private static T Read<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, StoredJson)
        ?? throw new InvalidOperationException("Stored replan JSON is invalid.");

    private static T? ReadNullable<T>(string? json) where T : class =>
        json is null ? null : JsonSerializer.Deserialize<T>(json, StoredJson)
            ?? throw new InvalidOperationException("Stored replan JSON is invalid.");
}

public sealed record ProposalReplanAssessmentResult(
    string ProposedRevisionJson,
    string ComparisonJson);

internal sealed record ReplanMixContext(
    MediaMixRow Mix,
    IReadOnlyDictionary<string, MediaAllocationView> Allocations,
    IReadOnlyList<AudienceSegmentView> Targets);

internal sealed record ProposalReplanImpactRow(
    Guid ImpactId,
    Guid MediaPlanLineId,
    Guid OldProductId,
    Guid OldProductVersionId,
    Guid OldRateId,
    Guid? OldAvailabilityId,
    Guid? ReplacementProductId,
    Guid? ReplacementProductVersionId,
    Guid? ReplacementRateId,
    Guid? ReplacementAvailabilityId,
    Guid BriefVersionId,
    Guid MixVersionId,
    string RunningPeriodsJson,
    string? PurchaseJson,
    long OldSupplierCostMinor);

public sealed record ProposalReplanLineAssessment(
    Guid ImpactId,
    Guid MediaPlanLineId,
    Guid OldProductId,
    Guid OldProductVersionId,
    Guid OldRateId,
    Guid? OldAvailabilityId,
    Guid? ProposedProductId,
    Guid? ProposedProductVersionId,
    Guid? ProposedRateId,
    Guid? ProposedAvailabilityId,
    string? ProductName,
    string? Channel,
    string? Geography,
    string? Currency,
    long? RateAmountMinor,
    long OldSupplierCostMinor,
    long? ProposedSupplierCostMinor,
    long? SupplierCostDeltaMinor,
    bool ContinuityCandidate,
    string? RejectionReason,
    string? RejectionDetail,
    bool RequiresHumanSelection,
    bool TriggeredReplacementStillCurrent);

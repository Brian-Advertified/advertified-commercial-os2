using System.Globalization;
using System.Text.Json;
using Advertified.Commercial.Application.Intelligence;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Intelligence;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Persistence;

namespace Advertified.Commercial.Infrastructure.Planning;

public sealed class InventoryIntelligenceService(
    GovernanceDbContext dbContext,
    PlanningRecordStore planningStore,
    IInventoryIntelligenceAgentClient agent,
    TimeProvider timeProvider) : IInventoryIntelligenceService
{
    private const string SubjectType = "InventoryShortlistVersion";
    private const string SchemaVersion = "inventory-intelligence.v1";
    private static readonly JsonSerializerOptions StoredJson = new(JsonSerializerDefaults.Web);

    public async Task<IntelligenceArtifactView> AnalyseShortlistAsync(
        Guid actorId,
        Guid tenantId,
        Guid shortlistVersionId,
        CancellationToken cancellationToken)
    {
        var actor = new ActorId(actorId);
        var tenant = new TenantId(tenantId);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await ApplicationDatabaseSession.SetAsync(
            dbContext, new UserId(actorId), tenant, cancellationToken);

        var context = await LoadContextAsync(
            actorId, tenant, shortlistVersionId, cancellationToken);
        var inputHash = IntelligenceInputHash.Combine(
            context.Shortlist.InputHash,
            context.Shortlist.Version.ToString(CultureInfo.InvariantCulture),
            context.Audience.Id.ToString("N"),
            context.Audience.Version.ToString(CultureInfo.InvariantCulture),
            context.MediaStrategy.Id.ToString("N"),
            context.MediaStrategy.Version.ToString(CultureInfo.InvariantCulture),
            context.Mix.Id.ToString("N"),
            context.Mix.Version.ToString(CultureInfo.InvariantCulture));
        var existing = await IntelligenceArtifactStore.FindLatestAsync(
            dbContext,
            tenant,
            SubjectType,
            shortlistVersionId,
            MasterDataCodes.AgentTypes.InventoryIntelligence,
            cancellationToken);
        if (existing is not null && existing.InputHash == inputHash &&
            existing.SubjectVersion == context.Shortlist.Version)
        {
            await transaction.CommitAsync(cancellationToken);
            return existing;
        }

        var shortlistView = await planningStore.BuildShortlistViewAsync(
            tenant, context.Shortlist, cancellationToken);
        if (shortlistView.Candidates.Count == 0)
            throw new InvalidOperationException("Inventory Intelligence requires a non-empty deterministic shortlist.");
        var audienceView = PlanningRecordStore.BuildAudienceView(context.Audience);
        var targetIds = audienceView.TargetAudienceIds.ToHashSet();
        var targetAudiences = audienceView.Definitions
            .Where(item => targetIds.Contains(item.Id))
            .ToArray();
        if (targetAudiences.Length == 0 || targetAudiences.Length != targetIds.Count)
            throw new InvalidOperationException("Inventory Intelligence requires a complete approved target audience.");
        var mixView = PlanningRecordStore.BuildMixView(context.Mix);
        var strategy = new InventoryStrategyInput(
            context.Audience.Id,
            context.Audience.Version,
            context.MediaStrategy.Id,
            context.MediaStrategy.Version,
            context.Mix.Id,
            context.Mix.Version,
            context.Brief.Objective,
            audienceView.TargetingRationale,
            audienceView.PositioningStatement,
            targetAudiences.Select(item => new InventoryStrategyAudienceInput(
                item.Id,
                item.Name,
                item.NeedState,
                item.BuyingContext,
                item.Geographies,
                item.Classification,
                item.Exclusions,
                item.EvidenceItemIds)).ToArray(),
            mixView.Allocations.Select(item => new InventoryStrategyAllocationInput(
                item.Channel,
                item.BudgetMinor,
                item.Role,
                item.RunningPeriods.Select(period => new InventoryStrategyPeriodInput(
                    period.Start, period.End)).ToArray())).ToArray());
        var evidenceIds = JsonSerializer.Deserialize<Guid[]>(
            context.Brief.EvidenceIdsJson, StoredJson) ?? [];
        var runId = Guid.NewGuid();
        var proposal = await agent.InterpretShortlistAsync(
            new InventoryIntelligenceInput(
                tenantId,
                actorId,
                runId,
                Guid.NewGuid(),
                context.Shortlist.BriefVersionId,
                context.Brief.Version,
                shortlistVersionId,
                context.Shortlist.Version,
                shortlistView.Candidates.Select(BuildCandidateInput).ToArray(),
                strategy,
                evidenceIds),
            cancellationToken);
        if (proposal.Interpretations.Count != shortlistView.Candidates.Count)
            throw new InvalidOperationException("Inventory Intelligence returned an incomplete interpretation set.");

        var artifactJson = JsonSerializer.Serialize(
            new InventoryIntelligenceArtifact(proposal.Interpretations), StoredJson);
        var saved = await IntelligenceArtifactStore.InsertDraftAsync(
            dbContext,
            tenant,
            actor,
            new IntelligenceArtifactDraft(
                SubjectType,
                shortlistVersionId,
                context.Shortlist.Version,
                MasterDataCodes.AgentTypes.InventoryIntelligence,
                SchemaVersion,
                artifactJson,
                proposal.Unknowns,
                proposal.Assumptions,
                inputHash,
                [proposal.Usage],
                [
                    new("BriefVersion", context.Shortlist.BriefVersionId,
                        context.Brief.Version, "commercial_problem"),
                    new("IntelligenceArtifact", context.Audience.Id,
                        context.Audience.Version, "approved_audience_strategy"),
                    new("IntelligenceArtifact", context.MediaStrategy.Id,
                        context.MediaStrategy.Version, "approved_media_strategy"),
                    new("MediaMixVersion", context.Mix.Id,
                        context.Mix.Version, "approved_media_mix"),
                    new(SubjectType, shortlistVersionId,
                        context.Shortlist.Version, "deterministic_shortlist"),
                ],
                []),
            timeProvider.GetUtcNow(),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return saved;
    }

    public async Task<IntelligenceArtifactView?> GetLatestAsync(
        Guid actorId,
        Guid tenantId,
        Guid shortlistVersionId,
        CancellationToken cancellationToken)
    {
        var tenant = new TenantId(tenantId);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await ApplicationDatabaseSession.SetAsync(
            dbContext, new UserId(actorId), tenant, cancellationToken);
        _ = await LoadContextAsync(actorId, tenant, shortlistVersionId, cancellationToken);
        var artifact = await IntelligenceArtifactStore.FindLatestAsync(
            dbContext,
            tenant,
            SubjectType,
            shortlistVersionId,
            MasterDataCodes.AgentTypes.InventoryIntelligence,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return artifact;
    }

    private async Task<InventoryContext> LoadContextAsync(
        Guid actorId,
        TenantId tenant,
        Guid shortlistVersionId,
        CancellationToken cancellationToken)
    {
        var shortlist = await planningStore.FindShortlistAsync(
            tenant, shortlistVersionId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Shortlist access denied.");
        if (shortlist.Status is not (MasterDataCodes.LifecycleStatuses.Draft or
            MasterDataCodes.LifecycleStatuses.Approved))
            throw new InvalidOperationException("Inventory Intelligence requires a current shortlist.");
        var brief = await planningStore.FindBriefAsync(
            tenant, shortlist.BriefVersionId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Brief access denied.");
        if (brief.OwnerUserId != actorId)
            throw new UnauthorizedAccessException("Shortlist interpretation denied.");
        var mix = await planningStore.FindMixAsync(
            tenant, shortlist.MixVersionId, cancellationToken)
            ?? throw new InvalidOperationException("The shortlist media mix is unavailable.");
        if (mix.Status != MasterDataCodes.LifecycleStatuses.Approved)
            throw new InvalidOperationException("Inventory Intelligence requires an approved media mix.");
        var audience = await planningStore.FindAudienceAsync(
            tenant, mix.AudienceArtifactId, cancellationToken)
            ?? throw new InvalidOperationException("The approved audience artifact is unavailable.");
        if (audience.Status != MasterDataCodes.LifecycleStatuses.Approved)
            throw new InvalidOperationException("Inventory Intelligence requires an approved audience artifact.");
        if (mix.MediaStrategyArtifactId is not { } mediaStrategyId)
            throw new InvalidOperationException("Inventory Intelligence requires exact Media Strategy lineage.");
        var mediaStrategy = await IntelligenceArtifactStore.FindAsync(
            dbContext, tenant, mediaStrategyId, cancellationToken)
            ?? throw new InvalidOperationException("The approved Media Strategy artifact is unavailable.");
        if (mediaStrategy.Status != MasterDataCodes.LifecycleStatuses.Approved ||
            mediaStrategy.ServiceCode != MasterDataCodes.AgentTypes.MediaStrategy)
            throw new InvalidOperationException("Inventory Intelligence requires an approved Media Strategy artifact.");
        return new InventoryContext(shortlist, brief, mix, audience, mediaStrategy);
    }

    private static InventoryCandidateInput BuildCandidateInput(
        InventoryShortlistCandidateView candidate)
    {
        var suitability = candidate.Suitability
            ?? throw new InvalidOperationException("The deterministic shortlist suitability is unavailable.");
        return new InventoryCandidateInput(
            candidate.Id,
            candidate.ProductVersionId,
            candidate.Name,
            candidate.Channel,
            candidate.Geography,
            candidate.RateAmountMinor,
            candidate.Currency,
            candidate.IsEligible,
            candidate.RejectionReason,
            candidate.RejectionDetail,
            candidate.Score,
            BuildAudienceFitInput(candidate.AudienceFit),
            BuildSuitabilityInput(suitability),
            candidate.Benchmark is null ? null : BuildBenchmarkInput(candidate.Benchmark));
    }

    private static InventoryAudienceFitInput BuildAudienceFitInput(
        InventoryAudienceFitView fit) => new(
            fit.LanguageScore,
            fit.LifeStageScore,
            fit.LsmSemScore,
            fit.EvidenceGaps,
            fit.MeasurementSource,
            fit.MeasurementPeriod,
            fit.Methodology,
            fit.TaxonomyName,
            fit.TaxonomyVersion,
            (fit.DeliveryMeasurements ?? []).Select(item => new InventoryDeliveryMeasurementInput(
                item.MetricType,
                item.Value,
                item.Unit,
                item.Universe,
                item.MeasurementSource,
                item.MeasurementPeriod,
                item.Methodology,
                item.Limitations)).ToArray(),
            fit.DeliveryEvidenceGaps ?? []);

    private static InventorySuitabilityInput BuildSuitabilityInput(
        InventorySuitabilityView suitability) => new(
            suitability.PolicyVersion,
            suitability.Geography,
            suitability.AudienceContext,
            suitability.ObjectiveFormat,
            suitability.BudgetEfficiency,
            suitability.EvidenceQualityFreshness,
            suitability.PortfolioCoverageDiversity,
            suitability.Total,
            suitability.EvidenceGaps,
            suitability.BuyAssessment is null
                ? null
                : BuildBuyAssessmentInput(suitability.BuyAssessment));

    private static InventoryBuyAssessmentInput BuildBuyAssessmentInput(
        InventoryBuyAssessmentView assessment) => new(
            assessment.CampaignSupplierCostMinor,
            assessment.Currency,
            assessment.Reach,
            assessment.Impressions,
            assessment.AverageFrequency,
            assessment.CostPerThousandImpressionsMinor,
            assessment.CostPerPersonReachedMinor,
            assessment.Universe,
            assessment.MeasurementPeriod,
            assessment.MeasurementSource,
            assessment.Methodology,
            assessment.IsTargetAudience,
            assessment.DigitalExposure is null
                ? null
                : new InventoryDigitalExposureInput(
                    assessment.DigitalExposure.SpotLengthSeconds,
                    assessment.DigitalExposure.SlotLengthSeconds,
                    assessment.DigitalExposure.LoopLengthSeconds,
                    assessment.DigitalExposure.PlaysPerLoop,
                    assessment.DigitalExposure.LoopSharePercent),
            assessment.EvidenceGaps,
            assessment.PlannerReasoning is null
                ? null
                : new InventoryPlannerReasoningInput(
                    assessment.PlannerReasoning.PlannedChannelRole,
                    assessment.PlannerReasoning.TargetContexts.Select(item =>
                        new InventoryPlannerAudienceContextInput(
                            item.Name, item.NeedState, item.BuyingContext)).ToArray(),
                    assessment.PlannerReasoning.RequiredPlacesMatched,
                    assessment.PlannerReasoning.RequiredPlacesTotal,
                    assessment.PlannerReasoning.HasMeasuredTargetAudience,
                    assessment.PlannerReasoning.ReviewQuestions,
                    assessment.PlannerReasoning.SupportedReasons,
                    assessment.PlannerReasoning.BuyingWarnings));

    private static InventoryBenchmarkInput BuildBenchmarkInput(
        InventoryBenchmarkView benchmark) => new(
            benchmark.PolicyVersion,
            benchmark.GeographyBasis,
            benchmark.CohortSize,
            benchmark.MedianMinor,
            benchmark.Percentile,
            benchmark.Position,
            benchmark.Confidence,
            benchmark.Exclusions);

    private sealed record InventoryContext(
        ShortlistRow Shortlist,
        PlanningBriefRow Brief,
        MediaMixRow Mix,
        AudienceArtifactRow Audience,
        IntelligenceArtifactView MediaStrategy);
}

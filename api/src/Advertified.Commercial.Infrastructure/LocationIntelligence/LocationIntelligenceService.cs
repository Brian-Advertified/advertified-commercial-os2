using System.Globalization;
using System.Text.Json;
using Advertified.Commercial.Application.Intelligence;
using Advertified.Commercial.Application.LocationIntelligence;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Intelligence;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Persistence;

namespace Advertified.Commercial.Infrastructure.LocationIntelligence;

public sealed class LocationIntelligenceService(
    GovernanceDbContext dbContext,
    ILocationIntelligenceAgentClient agent,
    LocationResearchExecutor researchExecutor,
    TimeProvider timeProvider) : ILocationIntelligenceService
{
    private const string SubjectType = "BriefVersion";
    private const string SchemaVersion = "location-intelligence.v1";
    private static readonly JsonSerializerOptions StoredJson = new(JsonSerializerDefaults.Web);

    public async Task<IntelligenceArtifactView> AnalyseBriefAsync(
        Guid actorId,
        Guid tenantId,
        Guid briefVersionId,
        CancellationToken cancellationToken)
    {
        var actor = new ActorId(actorId);
        var tenant = new TenantId(tenantId);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await ApplicationDatabaseSession.SetAsync(
            dbContext, new UserId(actorId), tenant, cancellationToken);

        var brief = await CommercialProblemReader.ReadAsync(
            dbContext, tenant, briefVersionId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Location Intelligence Brief access denied.");
        if (brief.OwnerUserId != actorId)
            throw new UnauthorizedAccessException("Location Intelligence assignment denied.");
        if (brief.Status is not (MasterDataCodes.LifecycleStatuses.Ready or MasterDataCodes.LifecycleStatuses.Approved))
            throw new InvalidOperationException("Location Intelligence requires a ready or approved Brief version.");

        var audience = await IntelligenceArtifactStore.FindLatestApprovedAsync(
            dbContext,
            tenant,
            SubjectType,
            briefVersionId,
            MasterDataCodes.AgentTypes.AudienceIntelligence,
            cancellationToken)
            ?? throw new InvalidOperationException("Location Intelligence requires an approved Audience Intelligence artifact.");
        var audienceArtifact = JsonSerializer.Deserialize<AudienceStrategyArtifact>(
            audience.ArtifactJson, StoredJson)
            ?? throw new InvalidOperationException("The approved Audience Intelligence artifact is invalid.");
        var targetIds = audienceArtifact.TargetAudienceIds.ToHashSet();
        var targetSegments = audienceArtifact.Segments
            .Where(item => targetIds.Contains(item.Id))
            .Select(item => new LocationAudienceSegmentInput(
                item.Id,
                item.Name,
                item.Description,
                item.Geographies,
                item.Classification,
                item.EvidenceItemIds,
                item.ReferenceObservationIds,
                true))
            .ToArray();
        if (targetSegments.Length == 0 || targetSegments.Length != targetIds.Count)
            throw new InvalidOperationException("Location Intelligence requires a complete approved target audience.");

        var sensitiveContext = await ReferenceObservationReader.ReadAsync(
            dbContext,
            brief.Geographies,
            "AUDIENCE",
            ["SENSITIVE_CONTEXT_ONLY"],
            100,
            cancellationToken);
        var aggregateContext = await ReferenceObservationReader.ReadAsync(
            dbContext,
            brief.Geographies,
            "AUDIENCE",
            ["AGGREGATE_PLANNING_ONLY"],
            100,
            cancellationToken);
        var referenceEvidence = sensitiveContext.Concat(aggregateContext)
            .DistinctBy(item => item.ObservationId)
            .ToArray();

        var runId = Guid.NewGuid();
        var problem = brief.ToInput(tenant, actor, runId, runId);
        var input = new LocationIntelligenceInput(
            problem,
            audience.Id,
            audience.Version,
            targetSegments,
            referenceEvidence,
            OpenStreetMapPoiCategoryCatalog.Options);
        var researchPlan = await agent.PlanResearchAsync(input, cancellationToken);
        var researchResult = await researchExecutor.ExecuteAsync(
            researchPlan.Queries,
            referenceEvidence,
            cancellationToken);
        var effectiveResearchPlan = researchPlan with
        {
            Queries = researchResult.ResearchQueries,
            Rationale = BuildEffectiveResearchRationale(researchResult.ResearchQueries),
            EvidenceGaps = researchPlan.EvidenceGaps
                .Concat(researchResult.EvidenceGaps)
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
        };
        var proposal = await agent.SynthesizeAsync(
            input, effectiveResearchPlan, researchResult.Places, cancellationToken);

        var artifact = new LocationIntelligenceArtifact(
            proposal.Summary,
            proposal.Opportunities.Select(item => new LocationOpportunityAreaArtifact(
                item.Name,
                item.Geography,
                item.Rationale,
                item.Classification,
                item.PlaceIds,
                item.ReferenceObservationIds,
                item.Confidence,
                item.EvidenceGaps)).ToArray(),
            proposal.ResearchQueries.Select(item => new LocationResearchQueryArtifact(
                item.PoiCategory,
                item.AnchorGeography,
                item.Purpose,
                item.Priority,
                item.Classification,
                item.ReferenceObservationIds)).ToArray(),
            proposal.ResolvedPlaces.Select(item => new ResolvedLocationPlaceArtifact(
                item.PlaceId,
                item.Query,
                item.Purpose,
                item.Name,
                item.Address,
                item.Latitude,
                item.Longitude,
                item.SourceLocator,
                item.Attribution,
                item.GeometryBasis)).ToArray(),
            proposal.EvidenceGaps);
        var artifactJson = JsonSerializer.Serialize(artifact, StoredJson);
        var researchJson = JsonSerializer.Serialize(new
        {
            effectiveResearchPlan.Queries,
            effectiveResearchPlan.Rationale,
            effectiveResearchPlan.EvidenceGaps,
        }, StoredJson);
        var placesJson = JsonSerializer.Serialize(researchResult.Places, StoredJson);
        var referenceIdsJson = JsonSerializer.Serialize(
            referenceEvidence.Select(item => item.ObservationId).Order().ToArray(), StoredJson);
        var inputHash = IntelligenceInputHash.Combine(
            brief.InputHash(),
            audience.Id.ToString("N"),
            audience.Version.ToString(CultureInfo.InvariantCulture),
            referenceIdsJson,
            researchJson,
            placesJson);
        var evidence = proposal.Opportunities
            .SelectMany((area, index) => area.ReferenceObservationIds.Select(observationId =>
                new IntelligenceArtifactEvidenceInput(
                    $"artifact.opportunities[{index}]",
                    area.Classification,
                    null,
                    observationId,
                    area.Rationale)))
            .ToArray();
        var unknowns = researchPlan.Unknowns.Concat(proposal.Unknowns).Distinct().ToArray();
        var draft = new IntelligenceArtifactDraft(
            SubjectType,
            briefVersionId,
            brief.Version,
            MasterDataCodes.AgentTypes.LocationIntelligence,
            SchemaVersion,
            artifactJson,
            unknowns,
            proposal.Assumptions,
            inputHash,
            [researchPlan.Usage, proposal.Usage],
            [
                new IntelligenceArtifactDependencyInput(
                    SubjectType, briefVersionId, brief.Version, "commercial_problem"),
                new IntelligenceArtifactDependencyInput(
                    MasterDataReferences.CommercialResourceTypes.IntelligenceArtifact.Value,
                    audience.Id,
                    audience.Version,
                    "approved_audience_strategy"),
            ],
            evidence);
        var saved = await IntelligenceArtifactStore.InsertDraftAsync(
            dbContext,
            tenant,
            actor,
            draft,
            timeProvider.GetUtcNow(),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return saved;
    }

    private static string BuildEffectiveResearchRationale(
        IReadOnlyList<PlaceResearchQueryProposal> queries) =>
        queries.Count == 0
            ? "No bounded Location Intelligence POI research request could be executed safely."
            : "Execute only the governed bounded POI research requests: " +
              string.Join("; ", queries.Select(item => $"{item.PoiCategory} in {item.AnchorGeography}")) + ".";

    public async Task<IntelligenceArtifactView?> GetLatestAsync(
        Guid actorId,
        Guid tenantId,
        Guid briefVersionId,
        CancellationToken cancellationToken)
    {
        var tenant = new TenantId(tenantId);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await ApplicationDatabaseSession.SetAsync(
            dbContext, new UserId(actorId), tenant, cancellationToken);
        var brief = await CommercialProblemReader.ReadAsync(
            dbContext, tenant, briefVersionId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Location Intelligence Brief access denied.");
        if (brief.OwnerUserId != actorId)
            throw new UnauthorizedAccessException("Location Intelligence assignment denied.");
        var artifact = await IntelligenceArtifactStore.FindLatestAsync(
            dbContext,
            tenant,
            SubjectType,
            briefVersionId,
            MasterDataCodes.AgentTypes.LocationIntelligence,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return artifact;
    }
}

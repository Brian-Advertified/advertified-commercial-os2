using System.Globalization;
using System.Text.Json;
using Advertified.Commercial.Application.Intelligence;
using Advertified.Commercial.Application.LocationIntelligence;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Persistence;
using Advertified.Commercial.Infrastructure.Planning;

namespace Advertified.Commercial.Infrastructure.Intelligence;

public sealed class MediaStrategyIntelligenceService(
    GovernanceDbContext dbContext,
    PlanningRecordStore planningStore,
    CampaignModePolicy campaignModePolicy,
    IMediaStrategyIntelligenceAgentClient agent,
    TimeProvider timeProvider) : IMediaStrategyIntelligenceService
{
    private const string SubjectType = "BriefVersion";
    private const string SchemaVersion = "media-strategy-intelligence.v1";
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
            ?? throw new UnauthorizedAccessException("Media Strategy Intelligence Brief access denied.");
        if (brief.OwnerUserId != actorId)
            throw new UnauthorizedAccessException("Media Strategy Intelligence assignment denied.");
        if (brief.Status is not (MasterDataCodes.LifecycleStatuses.Ready or MasterDataCodes.LifecycleStatuses.Approved))
            throw new InvalidOperationException("Media Strategy Intelligence requires a ready or approved Brief version.");

        var audience = await IntelligenceArtifactStore.FindLatestApprovedAsync(
            dbContext,
            tenant,
            SubjectType,
            briefVersionId,
            MasterDataCodes.AgentTypes.AudienceIntelligence,
            cancellationToken)
            ?? throw new InvalidOperationException("Media Strategy Intelligence requires an approved Audience Intelligence artifact.");
        var audienceArtifact = JsonSerializer.Deserialize<AudienceStrategyArtifact>(audience.ArtifactJson, StoredJson)
            ?? throw new InvalidOperationException("The approved Audience Intelligence artifact is invalid.");
        var targetIds = audienceArtifact.TargetAudienceIds.ToHashSet();
        var targetAudiences = audienceArtifact.Segments
            .Where(item => targetIds.Contains(item.Id))
            .Select(item => new MediaStrategyAudienceContext(
                item.Id,
                item.Name,
                item.Description,
                item.Geographies,
                item.Classification))
            .ToArray();
        if (targetAudiences.Length == 0 || targetAudiences.Length != targetIds.Count)
            throw new InvalidOperationException("Media Strategy Intelligence requires a complete approved target audience.");

        var location = await IntelligenceArtifactStore.FindLatestApprovedAsync(
            dbContext,
            tenant,
            SubjectType,
            briefVersionId,
            MasterDataCodes.AgentTypes.LocationIntelligence,
            cancellationToken);
        var locationOpportunities = Array.Empty<MediaStrategyLocationContext>();
        var locationEvidenceGaps = Array.Empty<string>();
        if (location is not null)
        {
            var locationArtifact = JsonSerializer.Deserialize<LocationIntelligenceArtifact>(
                location.ArtifactJson, StoredJson)
                ?? throw new InvalidOperationException("The approved Location Intelligence artifact is invalid.");
            locationOpportunities = locationArtifact.Opportunities
                .Select(item => new MediaStrategyLocationContext(
                    item.Name,
                    item.Geography,
                    item.Rationale,
                    item.Classification))
                .ToArray();
            locationEvidenceGaps = locationArtifact.EvidenceGaps.ToArray();
        }

        var campaignMode = await planningStore.FindCampaignModeAsync(
            tenant, briefVersionId, cancellationToken)
            ?? throw new InvalidOperationException("Media Strategy Intelligence requires a governed campaign-mode selection.");
        var availableChannels = campaignModePolicy.AllowedChannels(campaignMode.Mode);

        var runId = Guid.NewGuid();
        var problem = brief.ToInput(tenant, actor, runId, runId);
        var input = new MediaStrategyIntelligenceInput(
            problem,
            audience.Id,
            audience.Version,
            location?.Id,
            location?.Version,
            targetAudiences,
            locationOpportunities,
            audience.Unknowns,
            locationEvidenceGaps,
            availableChannels);
        var proposal = await agent.AnalyseAsync(input, cancellationToken);

        var artifactJson = JsonSerializer.Serialize(
            new MediaStrategyIntelligenceArtifact(
                proposal.Summary,
                proposal.ChannelRecommendations,
                proposal.StrategicPrinciples,
                proposal.ExcludedChannels,
                proposal.EvidenceGaps),
            StoredJson);
        var dependencyHash = JsonSerializer.Serialize(new
        {
            audience = new { audience.Id, audience.Version },
            location = location is null ? null : new { location.Id, location.Version },
            availableChannels,
        }, StoredJson);
        var inputHash = IntelligenceInputHash.Combine(
            brief.InputHash(),
            dependencyHash,
            problem.BudgetMinor?.ToString(CultureInfo.InvariantCulture) ?? "NO_BUDGET",
            problem.Currency ?? "NO_CURRENCY");

        var dependencies = new List<IntelligenceArtifactDependencyInput>
        {
            new(SubjectType, briefVersionId, brief.Version, "commercial_problem"),
            new(
                MasterDataReferences.CommercialResourceTypes.IntelligenceArtifact.Value,
                audience.Id,
                audience.Version,
                "approved_audience_strategy"),
        };
        if (location is not null)
        {
            dependencies.Add(new IntelligenceArtifactDependencyInput(
                MasterDataReferences.CommercialResourceTypes.IntelligenceArtifact.Value,
                location.Id,
                location.Version,
                "approved_location_intelligence"));
        }

        var draft = new IntelligenceArtifactDraft(
            SubjectType,
            briefVersionId,
            brief.Version,
            MasterDataCodes.AgentTypes.MediaStrategy,
            SchemaVersion,
            artifactJson,
            proposal.Unknowns,
            proposal.Assumptions,
            inputHash,
            [proposal.Usage],
            dependencies,
            []);
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

    public async Task<IntelligenceArtifactView> ApproveAsync(
        Guid actorId,
        Guid tenantId,
        Guid briefVersionId,
        Guid artifactId,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        var actor = new ActorId(actorId);
        var tenant = new TenantId(tenantId);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await ApplicationDatabaseSession.SetAsync(
            dbContext, new UserId(actorId), tenant, cancellationToken);
        var brief = await CommercialProblemReader.ReadAsync(
            dbContext, tenant, briefVersionId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Media Strategy Intelligence Brief access denied.");
        if (brief.OwnerUserId != actorId)
            throw new UnauthorizedAccessException("Media Strategy Intelligence assignment denied.");
        var artifact = await IntelligenceArtifactStore.FindAsync(
            dbContext, tenant, artifactId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Media Strategy Intelligence artifact access denied.");
        if (artifact.SubjectType != SubjectType || artifact.SubjectId != briefVersionId ||
            artifact.ServiceCode != MasterDataCodes.AgentTypes.MediaStrategy)
            throw new UnauthorizedAccessException("Media Strategy Intelligence artifact access denied.");

        var approved = await IntelligenceArtifactStore.ApproveRevisionAsync(
            dbContext,
            tenant,
            actor,
            artifactId,
            expectedVersion,
            artifact.ArtifactJson,
            timeProvider.GetUtcNow(),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return approved;
    }

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
            ?? throw new UnauthorizedAccessException("Media Strategy Intelligence Brief access denied.");
        if (brief.OwnerUserId != actorId)
            throw new UnauthorizedAccessException("Media Strategy Intelligence assignment denied.");
        var artifact = await IntelligenceArtifactStore.FindLatestAsync(
            dbContext,
            tenant,
            SubjectType,
            briefVersionId,
            MasterDataCodes.AgentTypes.MediaStrategy,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return artifact;
    }
}

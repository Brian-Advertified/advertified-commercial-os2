using System.Text.Json;
using Advertified.Commercial.Application.Intelligence;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Intelligence;

public sealed class MarketIntelligenceService(
    GovernanceDbContext dbContext,
    IMarketIntelligenceAgentClient agent,
    TimeProvider timeProvider) : IMarketIntelligenceService
{
    private const string SubjectType = "BriefVersion";
    private const string SchemaVersion = "market-intelligence.v1";
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
            ?? throw new UnauthorizedAccessException("Market Intelligence Brief access denied.");
        if (brief.OwnerUserId != actorId)
            throw new UnauthorizedAccessException("Market Intelligence assignment denied.");
        if (brief.Status is not (MasterDataCodes.LifecycleStatuses.Ready or MasterDataCodes.LifecycleStatuses.Approved))
            throw new InvalidOperationException("Market Intelligence requires a ready or approved Brief version.");

        var runId = Guid.NewGuid();
        var problem = brief.ToInput(tenant, actor, runId, runId);
        var approvedEvidence = await CommercialProblemReader.ReadApprovedEvidenceAsync(
            dbContext, problem, cancellationToken);
        var input = new MarketIntelligenceInput(problem, approvedEvidence);
        MarketIntelligenceValidator.ValidateInput(input);
        var proposal = await agent.AnalyseAsync(input, cancellationToken);
        MarketIntelligenceValidator.Validate(proposal, input);

        var draft = CreateDraft(brief, input, proposal);
        var saved = await IntelligenceArtifactStore.InsertDraftAsync(
            dbContext, tenant, actor, draft, timeProvider.GetUtcNow(), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return saved;
    }

    private static IntelligenceArtifactDraft CreateDraft(
        CommercialProblemSnapshot brief,
        MarketIntelligenceInput input,
        MarketIntelligenceAgentProposal proposal)
    {
        var artifactJson = JsonSerializer.Serialize(new
        {
            categorySituation = proposal.CategorySituation,
            findings = proposal.Findings,
            opportunities = proposal.Opportunities,
            strategicImplications = proposal.StrategicImplications,
            evidenceGaps = proposal.EvidenceGaps,
            rationale = proposal.Rationale,
        }, StoredJson);
        var inputHash = IntelligenceInputHash.Combine(
            brief.InputHash(), JsonSerializer.Serialize(input.ApprovedEvidence, StoredJson));
        var evidence = proposal.Findings
            .SelectMany((finding, index) => finding.EvidenceItemIds.Select(evidenceId =>
                new IntelligenceArtifactEvidenceInput(
                    $"artifact.findings[{index}]", finding.Classification,
                    evidenceId, null, finding.CommercialImplication)))
            .ToArray();
        return new IntelligenceArtifactDraft(
            SubjectType, brief.BriefVersionId, brief.Version,
            MasterDataCodes.AgentTypes.MarketIntelligence, SchemaVersion,
            artifactJson, proposal.Unknowns, proposal.Assumptions, inputHash,
            [proposal.Usage],
            [new IntelligenceArtifactDependencyInput(
                SubjectType, brief.BriefVersionId, brief.Version, "MARKET_INTELLIGENCE_INPUT")],
            evidence);
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
            ?? throw new UnauthorizedAccessException("Market Intelligence Brief access denied.");
        if (brief.OwnerUserId != actorId)
            throw new UnauthorizedAccessException("Market Intelligence assignment denied.");
        var artifact = await IntelligenceArtifactStore.FindLatestAsync(
            dbContext,
            tenant,
            SubjectType,
            briefVersionId,
            MasterDataCodes.AgentTypes.MarketIntelligence,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return artifact;
    }
}

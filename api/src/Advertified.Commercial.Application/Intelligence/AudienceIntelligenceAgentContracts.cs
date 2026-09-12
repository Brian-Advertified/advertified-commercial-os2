namespace Advertified.Commercial.Application.Intelligence;

public sealed record AudienceEvidenceFact(
    Guid? EvidenceItemId,
    string AudienceName,
    string? Language,
    string? LifeStage,
    string? LsmSem,
    string? LsmSemTaxonomy,
    string? LsmSemTaxonomyVersion,
    string? NeedState,
    string? BuyingContext,
    string? MessageContext,
    string? MomentContext)
{
    public Guid? BriefVersionId { get; init; }
}

public sealed record AudienceIntelligenceInput(
    CommercialProblemInput Problem,
    IReadOnlyList<AudienceEvidenceFact> Evidence,
    IReadOnlyList<ReferenceObservationFact> ReferenceEvidence);

public sealed record AudienceDefinitionProposal(
    string Name,
    string Description,
    string? NeedState,
    string? BuyingContext,
    IReadOnlyList<string> Geographies,
    string? Language,
    string? LifeStage,
    string? LsmSem,
    string? LsmSemTaxonomy,
    string? LsmSemTaxonomyVersion,
    string Classification,
    IReadOnlyList<string> Exclusions,
    IReadOnlyList<Guid> EvidenceItemIds,
    IReadOnlyList<Guid> ReferenceObservationIds,
    decimal? Confidence,
    bool IsTarget,
    bool LsmSemMandatory = false);

public sealed record AudienceAgentProposal(
    IReadOnlyList<AudienceDefinitionProposal> Audiences,
    string? TargetingRationale,
    string? PositioningStatement,
    IReadOnlyList<string> Unknowns,
    string Rationale,
    IntelligenceInvocationUsage Usage);

public interface IAudienceIntelligenceAgentClient
{
    Task<AudienceAgentProposal> ProposeAudiencesAsync(
        AudienceIntelligenceInput input,
        CancellationToken cancellationToken);
}

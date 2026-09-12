namespace Advertified.Commercial.Application.Intelligence;

public sealed record AudienceStrategyArtifact(
    IReadOnlyList<Guid> TargetAudienceIds,
    string? TargetingRationale,
    string? PositioningStatement,
    IReadOnlyList<AudienceSegmentArtifact> Segments);

public sealed record AudienceSegmentArtifact(
    Guid Id,
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
    bool LsmSemMandatory = false);

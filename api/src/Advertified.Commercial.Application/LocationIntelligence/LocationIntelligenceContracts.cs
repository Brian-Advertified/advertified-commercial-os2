using Advertified.Commercial.Application.Intelligence;

namespace Advertified.Commercial.Application.LocationIntelligence;

public sealed record LocationAudienceSegmentInput(
    Guid SegmentId,
    string Name,
    string Description,
    IReadOnlyList<string> Geographies,
    string Classification,
    IReadOnlyList<Guid> EvidenceItemIds,
    IReadOnlyList<Guid> ReferenceObservationIds,
    bool IsTarget);

public sealed record LocationPoiCategoryOption(
    string Code,
    string Label);

public sealed record LocationIntelligenceInput(
    CommercialProblemInput Problem,
    Guid AudienceArtifactId,
    long AudienceArtifactVersion,
    IReadOnlyList<LocationAudienceSegmentInput> TargetSegments,
    IReadOnlyList<ReferenceObservationFact> ReferenceEvidence,
    IReadOnlyList<LocationPoiCategoryOption> AvailablePoiCategories);

public sealed record PlaceResearchQueryProposal(
    string PoiCategory,
    string AnchorGeography,
    string Purpose,
    string Priority,
    string Classification,
    IReadOnlyList<Guid> ReferenceObservationIds);

public sealed record LocationResearchPlanProposal(
    IReadOnlyList<PlaceResearchQueryProposal> Queries,
    string Rationale,
    IReadOnlyList<string> EvidenceGaps,
    IReadOnlyList<string> Unknowns,
    IntelligenceInvocationUsage Usage);

public sealed record ResolvedLocationPlace(
    string PlaceId,
    string Query,
    string Purpose,
    string Name,
    string Address,
    decimal Latitude,
    decimal Longitude,
    string SourceLocator,
    string Attribution,
    string GeometryBasis);

public sealed record LocationOpportunityAreaProposal(
    string Name,
    string Geography,
    string Rationale,
    string Classification,
    IReadOnlyList<string> PlaceIds,
    IReadOnlyList<Guid> ReferenceObservationIds,
    decimal? Confidence,
    IReadOnlyList<string> EvidenceGaps);

public sealed record LocationResearchQueryArtifact(
    string PoiCategory,
    string AnchorGeography,
    string Purpose,
    string Priority,
    string Classification,
    IReadOnlyList<Guid> ReferenceObservationIds);

public sealed record ResolvedLocationPlaceArtifact(
    string PlaceId,
    string Query,
    string Purpose,
    string Name,
    string Address,
    decimal Latitude,
    decimal Longitude,
    string SourceLocator,
    string Attribution,
    string GeometryBasis);

public sealed record LocationOpportunityAreaArtifact(
    string Name,
    string Geography,
    string Rationale,
    string Classification,
    IReadOnlyList<string> PlaceIds,
    IReadOnlyList<Guid> ReferenceObservationIds,
    decimal? Confidence,
    IReadOnlyList<string> EvidenceGaps);

public sealed record LocationIntelligenceArtifact(
    string Summary,
    IReadOnlyList<LocationOpportunityAreaArtifact> Opportunities,
    IReadOnlyList<LocationResearchQueryArtifact> ResearchQueries,
    IReadOnlyList<ResolvedLocationPlaceArtifact> ResolvedPlaces,
    IReadOnlyList<string> EvidenceGaps);

public sealed record LocationIntelligenceProposal(
    string Summary,
    IReadOnlyList<LocationOpportunityAreaProposal> Opportunities,
    IReadOnlyList<PlaceResearchQueryProposal> ResearchQueries,
    IReadOnlyList<ResolvedLocationPlace> ResolvedPlaces,
    IReadOnlyList<string> EvidenceGaps,
    IReadOnlyList<string> Unknowns,
    IReadOnlyList<string> Assumptions,
    string Rationale,
    IntelligenceInvocationUsage Usage);

public interface ILocationIntelligenceAgentClient
{
    Task<LocationResearchPlanProposal> PlanResearchAsync(
        LocationIntelligenceInput input,
        CancellationToken cancellationToken);

    Task<LocationIntelligenceProposal> SynthesizeAsync(
        LocationIntelligenceInput input,
        LocationResearchPlanProposal plan,
        IReadOnlyList<ResolvedLocationPlace> resolvedPlaces,
        CancellationToken cancellationToken);
}

public interface ILocationIntelligenceService
{
    Task<IntelligenceArtifactView> AnalyseBriefAsync(
        Guid actorId,
        Guid tenantId,
        Guid briefVersionId,
        CancellationToken cancellationToken);

    Task<IntelligenceArtifactView?> GetLatestAsync(
        Guid actorId,
        Guid tenantId,
        Guid briefVersionId,
        CancellationToken cancellationToken);
}

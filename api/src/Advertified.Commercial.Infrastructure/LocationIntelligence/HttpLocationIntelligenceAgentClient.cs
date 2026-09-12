using Advertified.Commercial.Application.Intelligence;
using Advertified.Commercial.Application.LocationIntelligence;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Intelligence;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Infrastructure.LocationIntelligence;

public sealed class HttpLocationIntelligenceAgentClient(
    HttpClient httpClient,
    IOptions<AgentRuntimeOptions> options) : ILocationIntelligenceAgentClient
{
    private const string ResearchOperation = "LOCATION_RESEARCH_PLAN";
    private const string SynthesisOperation = "LOCATION_SYNTHESIS";

    public async Task<LocationResearchPlanProposal> PlanResearchAsync(
        LocationIntelligenceInput input,
        CancellationToken cancellationToken)
    {
        var agentCode = MasterDataCodes.AgentTypes.LocationIntelligence;
        var payload = new LocationResearchRequest(
            ResearchOperation,
            CreateInvocation(input, agentCode, ResearchOperation),
            ToLocationContext(input));
        var output = await AgentRuntimeHttpSupport.InvokeAsync<LocationResearchArtifact>(
            httpClient,
            options.Value,
            agentCode,
            payload,
            input.Problem.EvidenceItemIds,
            cancellationToken,
            ResearchOperation);
        EnsureCompleted(output.Status, "research plan");
        var artifact = output.Artifact
            ?? throw new InvalidOperationException("The Location Intelligence research plan is unavailable.");
        ValidateResearchArtifact(artifact, input);
        return new LocationResearchPlanProposal(
            artifact.Queries.Select(item => new PlaceResearchQueryProposal(
                item.PoiCategory,
                item.AnchorGeography,
                item.Purpose,
                item.Priority,
                item.Classification,
                item.ReferenceObservationIds)).ToArray(),
            artifact.Rationale,
            artifact.EvidenceGaps,
            output.Unknowns.Select(item => item.Question).ToArray(),
            IntelligenceUsage.FromRuntime(ResearchOperation, output.Usage));
    }

    public async Task<LocationIntelligenceProposal> SynthesizeAsync(
        LocationIntelligenceInput input,
        LocationResearchPlanProposal plan,
        IReadOnlyList<ResolvedLocationPlace> resolvedPlaces,
        CancellationToken cancellationToken)
    {
        var agentCode = MasterDataCodes.AgentTypes.LocationIntelligence;
        var research = new LocationResearchArtifact(
            plan.Queries.Select(ToResearchQuery).ToArray(),
            plan.Rationale,
            plan.EvidenceGaps.ToArray());
        var resolved = resolvedPlaces.Select(ToResolvedPlace).ToArray();
        var payload = new LocationSynthesisRequest(
            SynthesisOperation,
            CreateInvocation(input, agentCode, SynthesisOperation),
            new LocationSynthesisContext(
                input.Problem.BriefVersionId,
                input.AudienceArtifactId,
                input.AudienceArtifactVersion,
                input.Problem.ClientName,
                input.Problem.BusinessProblem,
                input.Problem.Objective,
                input.Problem.Geographies,
                input.Problem.MediaRequirements,
                input.Problem.Constraints,
                input.Problem.Conflicts,
                input.TargetSegments,
                input.ReferenceEvidence,
                input.AvailablePoiCategories,
                research,
                resolved));
        var output = await AgentRuntimeHttpSupport.InvokeAsync<LocationArtifact>(
            httpClient,
            options.Value,
            agentCode,
            payload,
            input.Problem.EvidenceItemIds,
            cancellationToken,
            SynthesisOperation);
        EnsureCompleted(output.Status, "synthesis");
        var artifact = output.Artifact
            ?? throw new InvalidOperationException("The Location Intelligence artifact is unavailable.");
        ValidateSynthesisArtifact(artifact, input, research, resolved);
        return new LocationIntelligenceProposal(
            artifact.Summary,
            artifact.Opportunities.Select(item => new LocationOpportunityAreaProposal(
                item.Name,
                item.Geography,
                item.Rationale,
                item.Classification,
                item.PlaceIds,
                item.ReferenceObservationIds,
                item.Confidence,
                item.EvidenceGaps)).ToArray(),
            artifact.ResearchQueries.Select(item => new PlaceResearchQueryProposal(
                item.PoiCategory,
                item.AnchorGeography,
                item.Purpose,
                item.Priority,
                item.Classification,
                item.ReferenceObservationIds)).ToArray(),
            artifact.ResolvedPlaces.Select(item => new ResolvedLocationPlace(
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
            artifact.EvidenceGaps,
            output.Unknowns.Select(item => item.Question).ToArray(),
            output.Assumptions.Select(item => item.Value).ToArray(),
            output.Rationale,
            IntelligenceUsage.FromRuntime(SynthesisOperation, output.Usage));
    }

    private AgentInvocationRequest CreateInvocation(
        LocationIntelligenceInput input,
        string agentCode,
        string operation) => AgentRuntimeHttpSupport.CreateInvocation(
            input.Problem.TenantId,
            input.Problem.ActorId,
            input.Problem.RunId,
            Guid.NewGuid(),
            input.Problem.CorrelationId,
            agentCode,
            [
                new AgentResourceReference(
                    "BriefVersion", input.Problem.BriefVersionId, input.Problem.BriefVersion),
                new AgentResourceReference(
                    "IntelligenceArtifact", input.AudienceArtifactId, input.AudienceArtifactVersion),
            ],
            input.Problem.EvidenceItemIds,
            options.Value,
            modelOperation: operation);

    private static LocationContext ToLocationContext(LocationIntelligenceInput input) => new(
        input.Problem.BriefVersionId,
        input.AudienceArtifactId,
        input.AudienceArtifactVersion,
        input.Problem.ClientName,
        input.Problem.BusinessProblem,
        input.Problem.Objective,
        input.Problem.Geographies,
        input.Problem.MediaRequirements,
        input.Problem.Constraints,
        input.Problem.Conflicts,
        input.TargetSegments,
        input.ReferenceEvidence,
        input.AvailablePoiCategories);

    private static LocationResearchQuery ToResearchQuery(PlaceResearchQueryProposal item) => new(
        item.PoiCategory,
        item.AnchorGeography,
        item.Purpose,
        item.Priority,
        item.Classification,
        item.ReferenceObservationIds);

    private static LocationResolvedPlace ToResolvedPlace(ResolvedLocationPlace item) => new(
        item.PlaceId,
        item.Query,
        item.Purpose,
        item.Name,
        item.Address,
        item.Latitude,
        item.Longitude,
        item.SourceLocator,
        item.Attribution,
        item.GeometryBasis);

    private static void ValidateResearchArtifact(
        LocationResearchArtifact artifact,
        LocationIntelligenceInput input)
    {
        if (artifact.Queries is null || artifact.Queries.Count > 8 ||
            string.IsNullOrWhiteSpace(artifact.Rationale) || artifact.EvidenceGaps is null)
            throw new InvalidOperationException("The Location Intelligence research plan is incomplete.");
        var references = input.ReferenceEvidence.ToDictionary(item => item.ObservationId);
        var availableGeographies = input.ReferenceEvidence.Select(item => item.GeographyName)
            .Concat(input.Problem.Geographies)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var availableCategories = input.AvailablePoiCategories.Select(item => item.Code)
            .ToHashSet(StringComparer.Ordinal);
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var query in artifact.Queries)
        {
            if (query is null)
                throw new InvalidOperationException("The Location Intelligence research plan is unsupported.");
            var cited = query.ReferenceObservationIds
                .Where(references.ContainsKey)
                .Select(id => references[id])
                .ToArray();
            var key = $"{query.PoiCategory.Trim()}|{query.AnchorGeography.Trim()}";
            if (string.IsNullOrWhiteSpace(query.PoiCategory) ||
                !availableCategories.Contains(query.PoiCategory) ||
                string.IsNullOrWhiteSpace(query.AnchorGeography) ||
                string.IsNullOrWhiteSpace(query.Purpose) || !unique.Add(key) ||
                query.ReferenceObservationIds.Any(item => !references.ContainsKey(item)) ||
                cited.Any(item => !string.Equals(
                    item.GeographyName,
                    query.AnchorGeography,
                    StringComparison.OrdinalIgnoreCase)) ||
                !availableGeographies.Contains(query.AnchorGeography) ||
                query.Priority is not (
                    MasterDataCodes.SpatialRequirementPriorities.Required or
                    MasterDataCodes.SpatialRequirementPriorities.Preferred) ||
                query.Classification is not (
                    MasterDataCodes.EvidenceClassifications.Inference or
                    MasterDataCodes.EvidenceClassifications.Hypothesis) ||
                (query.Classification == MasterDataCodes.EvidenceClassifications.Inference && cited.Length == 0))
                throw new InvalidOperationException("The Location Intelligence research plan is unsupported.");
        }
    }

    private static void ValidateSynthesisArtifact(
        LocationArtifact artifact,
        LocationIntelligenceInput input,
        LocationResearchArtifact research,
        IReadOnlyList<LocationResolvedPlace> resolved)
    {
        if (string.IsNullOrWhiteSpace(artifact.Summary) || artifact.Opportunities is null ||
            artifact.ResearchQueries is null || artifact.ResolvedPlaces is null || artifact.EvidenceGaps is null)
            throw new InvalidOperationException("The Location Intelligence artifact is incomplete.");
        if (!artifact.ResearchQueries.SequenceEqual(research.Queries) ||
            !artifact.ResolvedPlaces.SequenceEqual(resolved))
            throw new InvalidOperationException("Location Intelligence changed deterministic research facts.");
        if (research.EvidenceGaps.Any(item =>
                !artifact.EvidenceGaps.Contains(item, StringComparer.Ordinal)))
            throw new InvalidOperationException("Location Intelligence omitted deterministic research evidence gaps.");
        var places = resolved.Select(item => item.PlaceId).ToHashSet(StringComparer.Ordinal);
        var observationsById = input.ReferenceEvidence.ToDictionary(item => item.ObservationId);
        var observations = observationsById.Keys.ToHashSet();
        foreach (var area in artifact.Opportunities)
        {
            if (area is null)
                throw new InvalidOperationException("Location Intelligence returned an unsupported opportunity area.");
            var inferenceReferences = area.ReferenceObservationIds
                .Where(observationsById.ContainsKey)
                .Select(id => observationsById[id])
                .Where(item => item.ActivationPolicy is not ("SENSITIVE_CONTEXT_ONLY" or "AGGREGATE_PLANNING_ONLY"))
                .ToArray();
            if (string.IsNullOrWhiteSpace(area.Name) ||
                string.IsNullOrWhiteSpace(area.Geography) || string.IsNullOrWhiteSpace(area.Rationale) ||
                area.Classification is not (
                    MasterDataCodes.EvidenceClassifications.Inference or
                    MasterDataCodes.EvidenceClassifications.Hypothesis) ||
                area.Confidence is < 0 or > 1 ||
                (area.PlaceIds.Length == 0 && area.ReferenceObservationIds.Length == 0) ||
                area.PlaceIds.Any(item => !places.Contains(item)) ||
                area.ReferenceObservationIds.Any(item => !observations.Contains(item)) ||
                (area.Classification == MasterDataCodes.EvidenceClassifications.Inference && inferenceReferences.Length == 0) ||
                (inferenceReferences.Length == 0 && area.Confidence is not null))
                throw new InvalidOperationException("Location Intelligence returned an unsupported opportunity area.");
        }
    }

    private static void EnsureCompleted(string status, string stage)
    {
        if (status != MasterDataCodes.LifecycleStatuses.Completed)
            throw new InvalidOperationException($"Location Intelligence {stage} did not complete.");
    }

    private sealed record LocationResearchRequest(
        string Operation,
        AgentInvocationRequest Invocation,
        LocationContext Location);

    private sealed record LocationSynthesisRequest(
        string Operation,
        AgentInvocationRequest Invocation,
        LocationSynthesisContext Location);

    private record LocationContext(
        Guid BriefVersionId,
        Guid AudienceArtifactId,
        long AudienceArtifactVersion,
        string ClientName,
        string BusinessProblem,
        string Objective,
        IReadOnlyList<string> Geographies,
        IReadOnlyList<string> MediaRequirements,
        IReadOnlyList<string> Constraints,
        IReadOnlyList<CommercialProblemConflictInput> Conflicts,
        IReadOnlyList<LocationAudienceSegmentInput> TargetSegments,
        IReadOnlyList<ReferenceObservationFact> ReferenceEvidence,
        IReadOnlyList<LocationPoiCategoryOption> AvailablePoiCategories);

    private sealed record LocationSynthesisContext(
        Guid BriefVersionId,
        Guid AudienceArtifactId,
        long AudienceArtifactVersion,
        string ClientName,
        string BusinessProblem,
        string Objective,
        IReadOnlyList<string> Geographies,
        IReadOnlyList<string> MediaRequirements,
        IReadOnlyList<string> Constraints,
        IReadOnlyList<CommercialProblemConflictInput> Conflicts,
        IReadOnlyList<LocationAudienceSegmentInput> TargetSegments,
        IReadOnlyList<ReferenceObservationFact> ReferenceEvidence,
        IReadOnlyList<LocationPoiCategoryOption> AvailablePoiCategories,
        LocationResearchArtifact ResearchPlan,
        IReadOnlyList<LocationResolvedPlace> ResolvedPlaces)
        : LocationContext(
            BriefVersionId,
            AudienceArtifactId,
            AudienceArtifactVersion,
            ClientName,
            BusinessProblem,
            Objective,
            Geographies,
            MediaRequirements,
            Constraints,
            Conflicts,
            TargetSegments,
            ReferenceEvidence,
            AvailablePoiCategories);

    private sealed record LocationResearchArtifact(
        IReadOnlyList<LocationResearchQuery> Queries,
        string Rationale,
        IReadOnlyList<string> EvidenceGaps);

    private sealed record LocationResearchQuery(
        string PoiCategory,
        string AnchorGeography,
        string Purpose,
        string Priority,
        string Classification,
        IReadOnlyList<Guid> ReferenceObservationIds);

    private sealed record LocationResolvedPlace(
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

    private sealed class LocationArtifact
    {
        public required string Summary { get; init; }
        public required LocationOpportunityArea[] Opportunities { get; init; }
        public required LocationResearchQuery[] ResearchQueries { get; init; }
        public required LocationResolvedPlace[] ResolvedPlaces { get; init; }
        public required string[] EvidenceGaps { get; init; }
    }

    private sealed class LocationOpportunityArea
    {
        public required string Name { get; init; }
        public required string Geography { get; init; }
        public required string Rationale { get; init; }
        public required string Classification { get; init; }
        public required string[] PlaceIds { get; init; }
        public required Guid[] ReferenceObservationIds { get; init; }
        public required decimal? Confidence { get; init; }
        public required string[] EvidenceGaps { get; init; }
    }
}

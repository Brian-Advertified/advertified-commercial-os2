using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;

namespace Advertified.Commercial.Infrastructure.Planning;

public sealed partial class HttpPlanningAgentClient
{
    public async Task<InventoryIntelligenceAgentProposal> InterpretInventoryAsync(
        InventoryIntelligenceInput input,
        CancellationToken cancellationToken)
    {
        ValidateInventoryInput(input);
        var brief = input.Brief;
        var agentCode = MasterDataCodes.AgentTypes.InventoryIntelligence;
        var invocation = AgentRuntimeHttpSupport.CreateInvocation(
            brief.TenantId,
            brief.ActorId,
            brief.RunId,
            brief.RunId,
            brief.CorrelationId,
            agentCode,
            [
                new AgentResourceReference(
                    BriefVersionResourceType,
                    brief.BriefVersionId,
                    brief.BriefVersion),
                new AgentResourceReference(
                    "InventoryShortlistVersion",
                    input.ShortlistVersionId,
                    input.ShortlistVersion),
            ],
            brief.EvidenceItemIds,
            RuntimeSettings);
        var payload = new InventoryIntelligenceRequest(
            invocation,
            new InventoryIntelligenceContext(
                brief.BriefVersionId,
                input.ShortlistVersionId,
                input.Candidates.Select(ToRuntimeCandidate).ToArray()));
        var output =
            await AgentRuntimeHttpSupport.InvokeAsync<InventoryShortlistArtifact>(
                RuntimeHttpClient,
                RuntimeSettings,
                agentCode,
                payload,
                brief.EvidenceItemIds,
                cancellationToken);
        EnsureCompleted(output.Status);
        var artifact = output.Artifact
            ?? throw new InvalidOperationException(
                "The Inventory Intelligence artifact is unavailable.");
        var interpretations = ValidateInventoryArtifact(artifact, input.Candidates);
        return new InventoryIntelligenceAgentProposal(
            interpretations,
            output.Rationale,
            output.Usage.Provider,
            output.Usage.Model,
            output.Usage.IncrementalCostMinor,
            output.Usage.ProviderRequestId);
    }

    private static void ValidateInventoryInput(InventoryIntelligenceInput input)
    {
        if (input.ShortlistVersionId == Guid.Empty || input.ShortlistVersion <= 0 ||
            input.Candidates.Count == 0 ||
            input.Candidates.Select(item => item.CandidateId).Distinct().Count() !=
                input.Candidates.Count)
        {
            throw new ArgumentException(
                "Inventory Intelligence requires an exact shortlist and unique candidates.");
        }
        foreach (var candidate in input.Candidates)
        {
            if (candidate.CandidateId == Guid.Empty ||
                candidate.ProductVersionId == Guid.Empty ||
                string.IsNullOrWhiteSpace(candidate.Name))
            {
                throw new ArgumentException("The inventory candidate identity is invalid.");
            }
            var valid = candidate.IsEligible
                ? candidate.RateAmountMinor.HasValue &&
                  !string.IsNullOrWhiteSpace(candidate.Currency) &&
                  candidate.Score.HasValue &&
                  candidate.RejectionReason is null &&
                  candidate.RejectionDetail is null
                : !string.IsNullOrWhiteSpace(candidate.RejectionReason) &&
                  !string.IsNullOrWhiteSpace(candidate.RejectionDetail) &&
                  !candidate.Score.HasValue;
            if (!valid)
            {
                throw new ArgumentException("The inventory eligibility facts are invalid.");
            }
        }
    }

    private static InventoryCandidateInterpretationProposal[] ValidateInventoryArtifact(
        InventoryShortlistArtifact artifact,
        IReadOnlyList<InventoryIntelligenceCandidateInput> candidates)
    {
        if (artifact.Interpretations is null ||
            artifact.Interpretations.Length != candidates.Count)
        {
            throw new InvalidOperationException(
                "Inventory Intelligence did not explain every candidate.");
        }
        var expected = candidates.Select(item => item.CandidateId).ToHashSet();
        var returned = new HashSet<Guid>();
        foreach (var interpretation in artifact.Interpretations)
        {
            if (interpretation is null ||
                !expected.Contains(interpretation.CandidateId) ||
                !returned.Add(interpretation.CandidateId) ||
                string.IsNullOrWhiteSpace(interpretation.Rationale) ||
                interpretation.Rationale.Length > 1_000)
            {
                throw new InvalidOperationException(
                    "Inventory Intelligence returned an invalid candidate explanation.");
            }
        }
        if (!returned.SetEquals(expected))
        {
            throw new InvalidOperationException(
                "Inventory Intelligence changed the supplied candidate set.");
        }
        return artifact.Interpretations
            .Select(item => new InventoryCandidateInterpretationProposal(
                item.CandidateId,
                item.Rationale.Trim()))
            .ToArray();
    }

    private static InventoryCandidateContext ToRuntimeCandidate(
        InventoryIntelligenceCandidateInput candidate) => new(
            candidate.CandidateId,
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
            new InventoryAudienceFitContext(
                candidate.AudienceFit.LanguageScore,
                candidate.AudienceFit.LifeStageScore,
                candidate.AudienceFit.LsmSemScore,
                candidate.AudienceFit.EvidenceGaps,
                candidate.AudienceFit.MeasurementSource,
                candidate.AudienceFit.MeasurementPeriod,
                candidate.AudienceFit.Methodology,
                candidate.AudienceFit.TaxonomyName,
                candidate.AudienceFit.TaxonomyVersion,
                (candidate.AudienceFit.DeliveryMeasurements ?? [])
                    .Select(item => new InventoryDeliveryMeasurementContext(
                        item.MetricType, item.Value, item.Unit, item.Universe,
                        item.MeasurementSource, item.MeasurementPeriod,
                        item.Methodology, item.Limitations)).ToArray(),
                candidate.AudienceFit.DeliveryEvidenceGaps ?? []),
            candidate.Benchmark is null
                ? null
                : new InventoryBenchmarkContext(
                    candidate.Benchmark.PolicyVersion,
                    candidate.Benchmark.GeographyBasis,
                    candidate.Benchmark.CohortSize,
                    candidate.Benchmark.MedianMinor,
                    candidate.Benchmark.Percentile,
                    candidate.Benchmark.Position,
                    candidate.Benchmark.Confidence,
                    candidate.Benchmark.Exclusions));

    private sealed record InventoryIntelligenceRequest(
        AgentInvocationRequest Invocation,
        InventoryIntelligenceContext Inventory);

    private sealed record InventoryIntelligenceContext(
        Guid BriefVersionId,
        Guid ShortlistVersionId,
        IReadOnlyList<InventoryCandidateContext> Candidates);

    private sealed record InventoryCandidateContext(
        Guid CandidateId,
        Guid ProductVersionId,
        string Name,
        string Channel,
        string Geography,
        long? RateAmountMinor,
        string? Currency,
        bool IsEligible,
        string? RejectionReason,
        string? RejectionDetail,
        decimal? Score,
        InventoryAudienceFitContext AudienceFit,
        InventoryBenchmarkContext? Benchmark);

    private sealed record InventoryAudienceFitContext(
        decimal? LanguageScore,
        decimal? LifeStageScore,
        decimal? LsmSemScore,
        IReadOnlyList<string> EvidenceGaps,
        string? MeasurementSource,
        string? MeasurementPeriod,
        string? Methodology,
        string? TaxonomyName,
        string? TaxonomyVersion,
        IReadOnlyList<InventoryDeliveryMeasurementContext> DeliveryMeasurements,
        IReadOnlyList<string> DeliveryEvidenceGaps);

    private sealed record InventoryDeliveryMeasurementContext(
        string MetricType,
        decimal? Value,
        string? Unit,
        string? Universe,
        string? MeasurementSource,
        string? MeasurementPeriod,
        string? Methodology,
        string? Limitations);

    private sealed record InventoryBenchmarkContext(
        string PolicyVersion,
        string GeographyBasis,
        int CohortSize,
        long? MedianMinor,
        decimal? Percentile,
        string Position,
        decimal Confidence,
        IReadOnlyList<string> Exclusions);

    private sealed class InventoryShortlistArtifact
    {
        public required RuntimeInventoryInterpretation[] Interpretations { get; init; }
    }

    private sealed class RuntimeInventoryInterpretation
    {
        public required Guid CandidateId { get; init; }
        public required string Rationale { get; init; }
    }
}

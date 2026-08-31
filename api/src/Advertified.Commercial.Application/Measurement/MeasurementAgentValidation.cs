using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Application.Measurement;

public static class MeasurementAgentValidation
{
    private static readonly string[] ForbiddenConsequences =
    [
        "caused", "causes", "return on investment", "increase spend", "decrease spend",
        "reallocate budget", "book inventory", "publish campaign", "send report",
    ];

    public static void Validate(
        MeasurementAgentInput input,
        MeasurementAgentProposal proposal)
    {
        ValidateUsage(proposal);
        var metrics = input.EvidenceSets.SelectMany(item => item.Metrics).ToArray();
        var expectedMetricIds = metrics.Select(item => item.Id).ToHashSet();
        if (metrics.Length == 0 || expectedMetricIds.Count != metrics.Length ||
            proposal.Interpretation.Findings.Count == 0)
            throw new MeasurementAgentOutputRejectedException();
        ValidateInterpretation(input, proposal.Interpretation, expectedMetricIds);
        var bound = proposal.EvidenceBindings.SelectMany(item => item.MetricIds).ToArray();
        if (proposal.EvidenceBindings.Any(item =>
                string.IsNullOrWhiteSpace(item.FieldPath) || item.MetricIds.Count == 0) ||
            bound.Any(id => !expectedMetricIds.Contains(id)) ||
            !bound.ToHashSet().SetEquals(expectedMetricIds))
            throw new MeasurementAgentOutputRejectedException();
        if (string.IsNullOrWhiteSpace(proposal.Rationale))
            throw new MeasurementAgentOutputRejectedException();
    }

    private static void ValidateInterpretation(
        MeasurementAgentInput input,
        MeasurementInterpretationView interpretation,
        HashSet<Guid> expectedMetricIds)
    {
        if (interpretation.CausalityStatus !=
                MasterDataCodes.CausalityStatuses.NotEstablished ||
            string.IsNullOrWhiteSpace(interpretation.ExecutiveSummary) ||
            interpretation.LearningProposals.Count == 0 ||
            HasForbiddenContent(interpretation))
            throw new MeasurementAgentOutputRejectedException();
        var referenced = interpretation.Findings.SelectMany(item => item.MetricIds).ToArray();
        if (referenced.Length != expectedMetricIds.Count ||
            referenced.Distinct().Count() != referenced.Length ||
            !referenced.ToHashSet().SetEquals(expectedMetricIds) ||
            interpretation.Findings.Any(item =>
                item.CausalityStatus != MasterDataCodes.CausalityStatuses.NotEstablished ||
                string.IsNullOrWhiteSpace(item.Title) || string.IsNullOrWhiteSpace(item.Summary) ||
                item.MetricIds.Count == 0))
            throw new MeasurementAgentOutputRejectedException();
        var limitations = input.EvidenceSets.SelectMany(item => item.Limitations)
            .Distinct(StringComparer.Ordinal).ToArray();
        if (!interpretation.Limitations.SequenceEqual(limitations, StringComparer.Ordinal) ||
            interpretation.LearningProposals.Any(item =>
                !item.RequiresNewApproval || string.IsNullOrWhiteSpace(item.Text)))
            throw new MeasurementAgentOutputRejectedException();
    }

    private static void ValidateUsage(MeasurementAgentProposal proposal)
    {
        if (proposal.Provider != "deterministic" || proposal.Model != "fixture-v1" ||
            proposal.Units != 0 || proposal.ToolCalls != 0 ||
            proposal.IncrementalCostMinor != 0 || proposal.CacheStatus != "FIXTURE" ||
            proposal.ContractVersion != "1.0.0" || proposal.PromptVersion != "1.0.0")
            throw new MeasurementAgentOutputRejectedException();
    }

    private static bool HasForbiddenContent(MeasurementInterpretationView interpretation)
    {
        var text = string.Join(" ",
            new[] { interpretation.ExecutiveSummary }
                .Concat(interpretation.Findings.SelectMany(item => new[] { item.Title, item.Summary }))
                .Concat(interpretation.LearningProposals.Select(item => item.Text)));
        return ForbiddenConsequences.Any(term =>
            text.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
}

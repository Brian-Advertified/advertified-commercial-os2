using Advertified.Commercial.Application.Measurement;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Measurement;

public sealed class DeterministicMeasurementAgentClient : IMeasurementAgentClient
{
    public Task<MeasurementAgentProposal> InterpretAsync(
        MeasurementAgentInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var metrics = input.EvidenceSets.SelectMany(item => item.Metrics).ToArray();
        var metricIds = metrics.Select(item => item.Id).ToArray();
        var limitations = input.EvidenceSets.SelectMany(item => item.Limitations)
            .Distinct(StringComparer.Ordinal).ToArray();
        var interpretation = new MeasurementInterpretationView(
            "Reviewed campaign performance facts are presented with their approved " +
            "source quality, methodology and limitations.",
            metrics.Select(ToFinding).ToArray(),
            limitations,
            [new MeasurementLearningProposalView(
                "Use the reviewed facts as learning inputs; require a new human-approved " +
                "plan before any material campaign or spend change.",
                true)],
            MasterDataCodes.CausalityStatuses.NotEstablished);
        var proposal = new MeasurementAgentProposal(
            interpretation,
            [
                new("artifact.findings", metricIds),
                new("artifact.limitations", metricIds),
            ],
            ["Causality is not established by the approved measurement plan."],
            "The deterministic interpretation references every reviewed metric exactly once " +
            "and does not infer attribution, ROI or an optimisation action.",
            "deterministic", "fixture-v1", 0, 0, 0, "FIXTURE", "1.0.0", "1.0.0");
        MeasurementAgentValidation.Validate(input, proposal);
        return Task.FromResult(proposal);
    }

    private static MeasurementFindingView ToFinding(MeasurementMetricFactInput metric) => new(
        $"{metric.MetricType.Replace('_', ' ').ToLowerInvariant()} reported",
        "The approved source reports this metric for the supplied campaign period. " +
        "It is an observed fact and does not establish a causal effect.",
        [metric.Id],
        MasterDataCodes.CausalityStatuses.NotEstablished);
}

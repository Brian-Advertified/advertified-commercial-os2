using System.Text.Json;
using Advertified.Commercial.Application.Measurement;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Infrastructure.Measurement;

public sealed class HttpMeasurementAgentClient(
    HttpClient httpClient,
    IOptions<AgentRuntimeOptions> options) : IMeasurementAgentClient
{
    private const string ContractVersion = "1.0.0";

    public async Task<MeasurementAgentProposal> InterpretAsync(
        MeasurementAgentInput input,
        CancellationToken cancellationToken)
    {
        try
        {
            var metricIds = input.EvidenceSets.SelectMany(item => item.Metrics)
                .Select(item => item.Id).ToArray();
            var payload = new MeasurementRequest(
                AgentRuntimeHttpSupport.CreateInvocation(
                    input.TenantId, input.ActorId, input.RunId, input.StepId,
                    input.CorrelationId, MasterDataCodes.AgentTypes.Measurement,
                    ResourceReferences(input), metricIds),
                new MeasurementContext(
                    input.CampaignId, input.CampaignVersion, input.MeasurementPlan,
                    input.DeliveryProofs, input.EvidenceSets));
            var output = await AgentRuntimeHttpSupport.InvokeAsync<MeasurementInterpretationView>(
                httpClient, options.Value, MasterDataCodes.AgentTypes.Measurement,
                payload, metricIds, cancellationToken);
            if (output.Status != MasterDataCodes.LifecycleStatuses.Completed)
                throw new MeasurementAgentOutputRejectedException();
            var interpretation = output.Artifact
                ?? throw new MeasurementAgentOutputRejectedException();
            var proposal = new MeasurementAgentProposal(
                interpretation,
                output.EvidenceBindings.Select(item => new MeasurementEvidenceBinding(
                    item.FieldPath, item.EvidenceItemIds)).ToArray(),
                output.Unknowns.Select(item => item.Question).ToArray(),
                output.Rationale,
                output.Usage.Provider,
                output.Usage.Model,
                output.Usage.Units,
                output.Usage.ToolCalls,
                output.Usage.IncrementalCostMinor,
                output.Usage.CacheStatus,
                ContractVersion,
                ContractVersion);
            MeasurementAgentValidation.Validate(input, proposal);
            return proposal;
        }
        catch (JsonException exception)
        {
            throw new MeasurementAgentOutputRejectedException(exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new MeasurementAgentOutputRejectedException(exception);
        }
    }

    private static AgentResourceReference[] ResourceReferences(
        MeasurementAgentInput input) =>
        [
            new("Campaign", input.CampaignId, input.CampaignVersion),
            .. input.DeliveryProofs.Select(item =>
                new AgentResourceReference("DeliveryProof", item.Id, item.Version)),
            .. input.EvidenceSets.Select(item =>
                new AgentResourceReference("PerformanceEvidence", item.Id, item.Version)),
        ];

    private sealed record MeasurementRequest(
        AgentInvocationRequest Invocation,
        MeasurementContext Measurement);

    private sealed record MeasurementContext(
        Guid CampaignId,
        long CampaignVersion,
        IReadOnlyList<string> MeasurementPlan,
        IReadOnlyList<MeasurementProofInput> DeliveryProofs,
        IReadOnlyList<MeasurementEvidenceInput> EvidenceSets);
}

namespace Advertified.Commercial.Application.Measurement;

public sealed record MeasurementProofInput(Guid Id, long Version);

public sealed record MeasurementMetricFactInput(
    Guid Id,
    Guid EvidenceSetId,
    string MetricType,
    decimal Value,
    string Unit,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string SourceLocator);

public sealed record MeasurementEvidenceInput(
    Guid Id,
    long Version,
    string QualityStatus,
    string Methodology,
    IReadOnlyList<string> Limitations,
    IReadOnlyList<MeasurementMetricFactInput> Metrics);

public sealed record MeasurementAgentInput(
    Guid TenantId,
    Guid ActorId,
    Guid RunId,
    Guid StepId,
    Guid CorrelationId,
    Guid CampaignId,
    long CampaignVersion,
    IReadOnlyList<string> MeasurementPlan,
    IReadOnlyList<MeasurementProofInput> DeliveryProofs,
    IReadOnlyList<MeasurementEvidenceInput> EvidenceSets);

public sealed record MeasurementEvidenceBinding(
    string FieldPath,
    IReadOnlyList<Guid> MetricIds);

public sealed record MeasurementAgentProposal(
    MeasurementInterpretationView Interpretation,
    IReadOnlyList<MeasurementEvidenceBinding> EvidenceBindings,
    IReadOnlyList<string> Unknowns,
    string Rationale,
    string Provider,
    string Model,
    int Units,
    int ToolCalls,
    long IncrementalCostMinor,
    string CacheStatus,
    string ContractVersion,
    string PromptVersion,
    string? ProviderRequestId = null);

public interface IMeasurementAgentClient
{
    Task<MeasurementAgentProposal> InterpretAsync(
        MeasurementAgentInput input,
        CancellationToken cancellationToken);
}

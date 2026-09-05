using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Application.Measurement;

public sealed record GenerateMeasurementReportCommand(Guid ApproverUserId);
public sealed record ReviewMeasurementReportCommand(bool Approved, string Reason);

public sealed record MeasurementFindingView(
    string Title,
    string Summary,
    IReadOnlyList<Guid> MetricIds,
    string CausalityStatus);

public sealed record MeasurementLearningProposalView(
    string Text,
    bool RequiresNewApproval);

public sealed record MeasurementInterpretationView(
    string ExecutiveSummary,
    IReadOnlyList<MeasurementFindingView> Findings,
    IReadOnlyList<string> Limitations,
    IReadOnlyList<MeasurementLearningProposalView> LearningProposals,
    string CausalityStatus);

public sealed record MeasurementReportView(
    Guid Id,
    Guid CampaignId,
    int VersionNumber,
    long CampaignVersion,
    IReadOnlyList<string> MeasurementPlan,
    IReadOnlyList<PerformanceEvidenceView> Evidence,
    MeasurementInterpretationView Interpretation,
    string Status,
    Guid ApproverUserId,
    Guid GeneratedBy,
    DateTimeOffset GeneratedAtUtc,
    Guid? ReviewedBy,
    DateTimeOffset? ReviewedAtUtc,
    string? ReviewReason,
    long Version,
    DateTimeOffset UpdatedAtUtc);

public interface IMeasurementReportCommands
{
    Task<CommandResult<MeasurementReportView>> GenerateAsync(
        Guid campaignId,
        CommandEnvelope<GenerateMeasurementReportCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<MeasurementReportView>> ReviewAsync(
        Guid campaignId,
        Guid reportId,
        CommandEnvelope<ReviewMeasurementReportCommand> envelope,
        CancellationToken cancellationToken);
}

public interface IMeasurementReportReader
{
    Task<MeasurementReportPage> ListAsync(
        ActorId actorId, TenantId tenantId, int pageSize, Guid? cursor,
        CancellationToken cancellationToken);

    Task<MeasurementCampaignPage> ListCampaignsAsync(
        ActorId actorId, TenantId tenantId, int pageSize, Guid? cursor,
        CancellationToken cancellationToken);

    Task<MeasurementReportView> GetAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid reportId,
        CancellationToken cancellationToken);
}

public sealed class MeasurementReportBlockedException : Exception;
public sealed class MeasurementAgentOutputRejectedException : Exception
{
    public MeasurementAgentOutputRejectedException()
    {
    }

    public MeasurementAgentOutputRejectedException(Exception innerException)
        : base("The measurement agent output was rejected.", innerException)
    {
    }
}

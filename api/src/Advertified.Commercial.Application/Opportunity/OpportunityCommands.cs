using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Application.Opportunity;

public sealed record CreateOpportunityCommand(
    Guid ClientId,
    string Title,
    string SourceType,
    string? SourceRef,
    Guid OwnerUserId,
    long? ExpectedValueMinor,
    string? Currency,
    DateOnly? Deadline,
    string? ProblemSummary,
    string? ObjectiveSummary);

public sealed record UpdateOpportunityCommand(
    string Title,
    long? ExpectedValueMinor,
    string? Currency,
    DateOnly? Deadline,
    string? ProblemSummary,
    string? ObjectiveSummary);

public sealed record CandidateEvidenceCommand(
    string Locator,
    string ClaimType,
    string StructuredValueJson,
    string Excerpt,
    decimal Confidence);

public sealed record RegisterEvidenceSourceCommand(
    Guid OpportunityId,
    string Type,
    string Locator,
    string Title,
    string PolicyBasis,
    string? Content,
    Guid ReviewerUserId,
    IReadOnlyList<CandidateEvidenceCommand> Claims);

public sealed record ReviewEvidenceItemCommand(
    string Decision,
    string? StructuredValueJson,
    string? Reason);

public sealed record SubmitEvidenceCommand(
    IReadOnlyList<string> Gaps,
    Guid ApproverUserId);

public sealed record ApproveEvidenceSetCommand(string? Reason);

public sealed record StartQualificationCommand(string? Comment);

public sealed record QueueAgentRunCommand(Guid? ApproverUserId = null);

public sealed record ConfirmInterpretationCommand(string? Comment);

public sealed record SelectOpportunityAngleCommand(string? Reason);

public sealed record ResolveCriticObjectionCommand(
    string Resolution,
    string Reason);

public sealed record SubmitStrategyCommand(string? Comment);

public sealed record ApproveStrategyCommand(string? Reason);

public sealed record RejectStrategyCommand(string Reason);

public sealed record ManageAgentRunCommand(string? Reason);

public interface IOpportunityCommands
{
    Task<CommandResult<OpportunityView>> CreateAsync(
        CommandEnvelope<CreateOpportunityCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<EvidenceSourceView>> RegisterEvidenceSourceAsync(
        CommandEnvelope<RegisterEvidenceSourceCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<OpportunityView>> UpdateAsync(
        Guid opportunityId,
        CommandEnvelope<UpdateOpportunityCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<OpportunityView>> StartQualificationAsync(
        Guid opportunityId,
        CommandEnvelope<StartQualificationCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<EvidenceItemView>> ReviewEvidenceItemAsync(
        Guid itemId,
        CommandEnvelope<ReviewEvidenceItemCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<EvidenceSetView>> SubmitEvidenceAsync(
        Guid opportunityId,
        CommandEnvelope<SubmitEvidenceCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<EvidenceSetView>> ApproveEvidenceSetAsync(
        Guid evidenceSetId,
        CommandEnvelope<ApproveEvidenceSetCommand> envelope,
        CancellationToken cancellationToken);
}

public interface IOpportunityWorkflowCommands
{
    Task<CommandResult<AgentRunView>> QueueRunAsync(
        Guid opportunityId,
        string runKind,
        CommandEnvelope<QueueAgentRunCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<BusinessInterpretationView>> ConfirmInterpretationAsync(
        Guid interpretationId,
        CommandEnvelope<ConfirmInterpretationCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<OpportunityAngleView>> SelectAngleAsync(
        Guid angleId,
        CommandEnvelope<SelectOpportunityAngleCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<CriticObjectionView>> ResolveObjectionAsync(
        Guid objectionId,
        CommandEnvelope<ResolveCriticObjectionCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<StrategyVersionView>> SubmitStrategyAsync(
        Guid strategyId,
        CommandEnvelope<SubmitStrategyCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<StrategyVersionView>> ApproveStrategyAsync(
        Guid strategyId,
        CommandEnvelope<ApproveStrategyCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<StrategyVersionView>> RejectStrategyAsync(
        Guid strategyId,
        CommandEnvelope<RejectStrategyCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<AgentRunView>> ManageRunAsync(
        Guid runId,
        bool cancel,
        CommandEnvelope<ManageAgentRunCommand> envelope,
        CancellationToken cancellationToken);
}

using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Application.Proposal;

public sealed record ProposalOptionInput(
    Guid PlanVersionId,
    string Label,
    string Outcome);

public sealed record GenerateProposalCommand(
    string Title,
    IReadOnlyList<ProposalOptionInput> Options,
    string Terms,
    DateTimeOffset ExpiryAtUtc);

public sealed record ProposalOptionEdit(
    Guid OptionId,
    string Label,
    string Outcome);

public sealed record UpdateProposalCommand(
    string Title,
    string ExecutiveSummary,
    string Terms,
    DateTimeOffset ExpiryAtUtc,
    IReadOnlyList<ProposalOptionEdit> Options);

public sealed record ApproveProposalCommand(string? Reason);

public sealed record RenderProposalCommand;

public sealed record ShareProposalCommand(
    Guid RecipientUserId,
    string? Reason);

public sealed record SelectProposalOptionCommand(
    Guid OptionId,
    string? Reason);

public sealed record DeclineProposalCommand(string? Reason);

public sealed record RecordAutomatedProposalDeliveryCommand(
    Guid AutomationRunId,
    string RecipientEmail,
    string ProviderMessageId);

public interface IProposalCommands
{
    Task<CommandResult<ProposalVersionView>> GenerateAsync(
        Guid briefId,
        CommandEnvelope<GenerateProposalCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<ProposalVersionView>> UpdateAsync(
        Guid proposalVersionId,
        CommandEnvelope<UpdateProposalCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<ProposalVersionView>> ApproveAsync(
        Guid proposalVersionId,
        CommandEnvelope<ApproveProposalCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<ProposalVersionView>> RenderAsync(
        Guid proposalVersionId,
        CommandEnvelope<RenderProposalCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<ProposalVersionView>> ShareAsync(
        Guid proposalVersionId,
        CommandEnvelope<ShareProposalCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<ProposalVersionView>> RecordAutomatedDeliveryAsync(
        Guid proposalVersionId,
        CommandEnvelope<RecordAutomatedProposalDeliveryCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<ProposalVersionView>> SelectOptionAsync(
        Guid proposalVersionId,
        CommandEnvelope<SelectProposalOptionCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<ProposalVersionView>> DeclineAsync(
        Guid proposalVersionId,
        CommandEnvelope<DeclineProposalCommand> envelope,
        CancellationToken cancellationToken);
}

public interface IProposalReader
{
    Task<ProposalVersionView> GetAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid proposalVersionId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ApprovedPlanChoiceView>> ListApprovedPlansAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid briefId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ProposalRecipientView>> ListRecipientsAsync(
        ActorId actorId,
        TenantId tenantId,
        CancellationToken cancellationToken);

    Task<ProposalDocumentContent> GetDocumentAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid documentId,
        CancellationToken cancellationToken);
}

public sealed record ProposalRecipientView(
    Guid UserId,
    string DisplayName,
    string Email,
    string Role);

public sealed record ApprovedPlanChoiceView(
    Guid Id,
    Guid BriefVersionId,
    int VersionNumber,
    long TotalMinor,
    string Currency,
    IReadOnlyList<string> Channels,
    IReadOnlyList<ProposalRunningPeriodView> RunningPeriods,
    DateTimeOffset CreatedAtUtc);

public sealed record ProposalRunningPeriodView(
    string Channel,
    DateOnly Start,
    DateOnly End);

public sealed record ProposalOptionView(
    Guid Id,
    string Label,
    string Outcome,
    Guid PlanVersionId,
    int PlanVersionNumber,
    long BudgetMinor,
    string Currency,
    int DisplayOrder,
    IReadOnlyList<string> Channels,
    IReadOnlyList<ProposalRunningPeriodView> RunningPeriods,
    IReadOnlyList<string> InventoryNames);

public sealed record ProposalDocumentView(
    Guid Id,
    string MediaType,
    string ContentHash,
    long SizeBytes,
    DateTimeOffset CreatedAtUtc);

public sealed record ProposalDecisionView(
    string Decision,
    Guid? OptionId,
    string? Reason,
    Guid DecidedBy,
    DateTimeOffset DecidedAtUtc);

public sealed record ProposalVersionView(
    Guid Id,
    Guid BriefId,
    Guid BriefVersionId,
    int VersionNumber,
    string Title,
    string ExecutiveSummary,
    string Terms,
    DateTimeOffset ExpiryAtUtc,
    string Status,
    IReadOnlyList<ProposalOptionView> Options,
    ProposalDocumentView? Document,
    Guid? RecipientUserId,
    ProposalDecisionView? Decision,
    Guid CreatedBy,
    Guid? ApprovedBy,
    long Version,
    DateTimeOffset CreatedAtUtc);

public sealed record ProposalDocumentContent(
    Guid Id,
    string MediaType,
    string FileName,
    byte[] Content);

public sealed record ProposalNarrativeInput(
    string BriefObjective,
    IReadOnlyList<ProposalOptionNarrativeInput> Options);

public sealed record ProposalOptionNarrativeInput(
    string Label,
    string Outcome,
    long BudgetMinor,
    string Currency,
    IReadOnlyList<string> Channels);

public sealed record ProposalNarrative(
    string ExecutiveSummary,
    long IncrementalCostMinor);

public interface IProposalNarrativeClient
{
    Task<ProposalNarrative> CreateAsync(
        ProposalNarrativeInput input,
        CancellationToken cancellationToken);
}

public sealed record ProposalDeliveryRequest(
    Guid TenantId,
    Guid ProposalVersionId,
    Guid RecipientUserId,
    string RecipientEmail,
    string ProposalTitle);

public sealed record ProposalDeliveryReceipt(
    DateTimeOffset DeliveredAtUtc,
    long IncrementalCostMinor);

public interface IProposalDeliveryClient
{
    Task<ProposalDeliveryReceipt> DeliverAsync(
        ProposalDeliveryRequest request,
        CancellationToken cancellationToken);
}

public sealed class ProposalStaleException : Exception
{
    public ProposalStaleException() : base("A referenced approved plan changed.") { }
}

public sealed class ProposalDocumentRequiredException : Exception
{
    public ProposalDocumentRequiredException() : base("An approved rendered proposal is required.") { }
}

public sealed class ProposalExpiredException : Exception
{
    public ProposalExpiredException() : base("The proposal has expired.") { }
}

using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Proposal;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Foundation;
using Advertified.Commercial.Infrastructure.Planning;

namespace Advertified.Commercial.Infrastructure.Proposal;

public sealed partial class ProposalCommands(
    ProposalRecordStore store,
    PlanningRecordStore planningStore,
    CommandDispatcher dispatcher,
    IProposalNarrativeClient narrativeClient,
    IProposalDeliveryClient deliveryClient,
    ProposalPolicy proposalPolicy,
    TimeProvider timeProvider) : IProposalCommands
{
    public Task<CommandResult<ProposalVersionView>> GenerateAsync(
        Guid briefId, CommandEnvelope<GenerateProposalCommand> envelope,
        CancellationToken cancellationToken) => DispatchAsync(
            envelope, MasterDataReferences.Permissions.ProposalGenerate,
            token => GenerateOutcomeAsync(briefId, envelope, token), cancellationToken);

    public Task<CommandResult<ProposalVersionView>> UpdateAsync(
        Guid proposalVersionId, CommandEnvelope<UpdateProposalCommand> envelope,
        CancellationToken cancellationToken) => DispatchAsync(
            envelope, MasterDataReferences.Permissions.ProposalEdit,
            token => UpdateOutcomeAsync(proposalVersionId, envelope, token), cancellationToken);

    public Task<CommandResult<ProposalVersionView>> ApproveAsync(
        Guid proposalVersionId, CommandEnvelope<ApproveProposalCommand> envelope,
        CancellationToken cancellationToken) => DispatchAsync(
            envelope, MasterDataReferences.Permissions.ProposalApprove,
            token => ApproveOutcomeAsync(proposalVersionId, envelope, token), cancellationToken);

    public Task<CommandResult<ProposalVersionView>> RenderAsync(
        Guid proposalVersionId, CommandEnvelope<RenderProposalCommand> envelope,
        CancellationToken cancellationToken) => DispatchAsync(
            envelope, MasterDataReferences.Permissions.ProposalEdit,
            token => RenderOutcomeAsync(proposalVersionId, envelope, token), cancellationToken);

    public Task<CommandResult<ProposalVersionView>> ShareAsync(
        Guid proposalVersionId, CommandEnvelope<ShareProposalCommand> envelope,
        CancellationToken cancellationToken) => DispatchAsync(
            envelope, MasterDataReferences.Permissions.ProposalShare,
            token => ShareOutcomeAsync(proposalVersionId, envelope, token), cancellationToken);

    public Task<CommandResult<ProposalVersionView>> SelectOptionAsync(
        Guid proposalVersionId, CommandEnvelope<SelectProposalOptionCommand> envelope,
        CancellationToken cancellationToken) => DispatchAsync(
            envelope, MasterDataReferences.Permissions.ProposalDecide,
            token => SelectOutcomeAsync(proposalVersionId, envelope, token), cancellationToken);

    public Task<CommandResult<ProposalVersionView>> DeclineAsync(
        Guid proposalVersionId, CommandEnvelope<DeclineProposalCommand> envelope,
        CancellationToken cancellationToken) => DispatchAsync(
            envelope, MasterDataReferences.Permissions.ProposalDecide,
            token => DeclineOutcomeAsync(proposalVersionId, envelope, token), cancellationToken);

    private async Task<CommandResult<ProposalVersionView>> DispatchAsync<TCommand>(
        CommandEnvelope<TCommand> envelope,
        PermissionCode permission,
        Func<CancellationToken, Task<CommandOutcome>> execute,
        CancellationToken cancellationToken)
        where TCommand : notnull
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, permission, execute, cancellationToken);
        return CommandOutcomeFactory.ToResult<ProposalVersionView>(receipt);
    }
}

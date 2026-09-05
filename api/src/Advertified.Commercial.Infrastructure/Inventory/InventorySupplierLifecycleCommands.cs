using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Foundation;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed partial class InventorySupplierLifecycleCommands(
    InventorySupplierLifecycleStore store,
    CommandDispatcher dispatcher,
    IIdempotentCommandUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IInventorySupplierLifecycleCommands
{
    public async Task<CommandResult<SupplierClaimInvitationView>> IssueInvitationAsync(
        Guid supplierId,
        CommandEnvelope<IssueSupplierClaimInvitationCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, MasterDataReferences.Permissions.SupplierClaimManage,
            token => IssueInvitationOutcomeAsync(supplierId, envelope, token),
            cancellationToken);
        return CommandOutcomeFactory.ToResult<SupplierClaimInvitationView>(receipt);
    }

    public async Task<CommandResult<SupplierClaimInvitationView>> RevokeInvitationAsync(
        Guid invitationId,
        CommandEnvelope<RevokeSupplierClaimInvitationCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, MasterDataReferences.Permissions.SupplierClaimManage,
            token => RevokeInvitationOutcomeAsync(invitationId, envelope, token),
            cancellationToken);
        return CommandOutcomeFactory.ToResult<SupplierClaimInvitationView>(receipt);
    }

    public async Task<CommandResult<SupplierClaimInvitationView>> AcceptInvitationAsync(
        Guid invitationId,
        CommandEnvelope<AcceptSupplierClaimInvitationCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await unitOfWork.ExecuteOnceAsync(
            envelope,
            token => AcceptInvitationOutcomeAsync(invitationId, envelope, token),
            cancellationToken,
            token => SupplierClaimAcceptancePolicy.AuthorizeAsync(
                store, invitationId, envelope, timeProvider.GetUtcNow(), token));
        return CommandOutcomeFactory.ToResult<SupplierClaimInvitationView>(receipt);
    }

    public async Task<CommandResult<ProposalInventoryImpactView>> ResolveProposalImpactAsync(
        Guid impactId,
        CommandEnvelope<ResolveProposalInventoryImpactCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, MasterDataReferences.Permissions.ProposalEdit,
            token => ResolveProposalImpactOutcomeAsync(impactId, envelope, token),
            cancellationToken);
        return CommandOutcomeFactory.ToResult<ProposalInventoryImpactView>(receipt);
    }
}

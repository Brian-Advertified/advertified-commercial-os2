using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.CommercialSettings;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Foundation;

namespace Advertified.Commercial.Infrastructure.CommercialSettings;

public sealed class CommercialPolicyCommands(
    CommercialPolicyRecordStore store,
    CommandDispatcher dispatcher,
    TimeProvider timeProvider) : ICommercialPolicyCommands
{
    public async Task<CommandResult<CommercialPolicyView>> SaveAsync(
        CommandEnvelope<SaveCommercialPolicyCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope,
            MasterDataReferences.Permissions.CommercialSettingsManage,
            token => SaveOutcomeAsync(envelope, token),
            cancellationToken);
        return CommandOutcomeFactory.ToResult<CommercialPolicyView>(receipt);
    }

    private async Task<CommandOutcome> SaveOutcomeAsync(
        CommandEnvelope<SaveCommercialPolicyCommand> envelope,
        CancellationToken cancellationToken)
    {
        var policy = CommercialPolicyValidator.Validate(envelope.Command);
        await store.LockAsync(envelope.TenantId, cancellationToken);
        var current = await store.FindCurrentAsync(envelope.TenantId, cancellationToken);
        EnsureExpectedVersion(current, envelope.ExpectedVersion);
        var policyId = current?.PolicyId ?? Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var now = timeProvider.GetUtcNow();
        if (current is null)
        {
            await store.InsertFirstAsync(
                envelope.TenantId, policyId, versionId, envelope.ActorId,
                policy, now, cancellationToken);
        }
        else
        {
            await store.InsertNextAsync(
                envelope.TenantId, current, versionId, envelope.ActorId,
                policy, now, cancellationToken);
        }
        var view = (await store.FindCurrentAsync(envelope.TenantId, cancellationToken)
            ?? throw new InvalidOperationException("The commercial policy was not persisted."))
            .ToView();
        return CommandOutcomeFactory.Create(
            envelope,
            view,
            versionId,
            view.Version,
            MasterDataReferences.CommercialResourceTypes.CommercialPolicyVersion,
            MasterDataReferences.CommercialActions.CommercialPolicyVersionCreated,
            MasterDataReferences.CommercialEventTypes.CommercialPolicyVersionCreated,
            now);
    }

    private static void EnsureExpectedVersion(CommercialPolicyRow? current, long expected)
    {
        if (current is null ? expected != 0 : current.Version != expected)
        {
            throw new VersionConflictException();
        }
    }
}

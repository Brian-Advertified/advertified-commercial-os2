using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.Foundation;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed partial class InventoryCommands(
    InventoryRecordStore store,
    CommandDispatcher dispatcher,
    TimeProvider timeProvider,
    IOptions<InventoryProtectionOptions> protectionOptions) : IInventoryCommands
{
    private readonly int maximumSourceBytes = protectionOptions.Value.MaximumSourceBytes;

    public async Task<CommandResult<InventoryImportView>> CreateAsync(
        CommandEnvelope<CreateInventoryImportCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, Gate6Permissions.InventoryImport,
            token => CreateOutcomeAsync(envelope, token), cancellationToken);
        return CommandOutcomeFactory.ToResult<InventoryImportView>(receipt);
    }

    public async Task<CommandResult<InventoryImportView>> ExecuteAsync(
        Guid importId,
        CommandEnvelope<ExecuteInventoryImportCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, Gate6Permissions.InventoryImport,
            token => ExecuteOutcomeAsync(importId, envelope, token), cancellationToken);
        return CommandOutcomeFactory.ToResult<InventoryImportView>(receipt);
    }

    public async Task<CommandResult<InventoryCandidateView>> ReviewAsync(
        Guid candidateId,
        CommandEnvelope<ReviewInventoryCandidateCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, Gate6Permissions.InventoryReview,
            token => ReviewOutcomeAsync(candidateId, envelope, token), cancellationToken);
        return CommandOutcomeFactory.ToResult<InventoryCandidateView>(receipt);
    }

    public async Task<CommandResult<InventoryImportView>> PublishAsync(
        Guid importId,
        CommandEnvelope<PublishInventoryImportCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, Gate6Permissions.InventoryPublish,
            token => PublishOutcomeAsync(importId, envelope, token), cancellationToken);
        return CommandOutcomeFactory.ToResult<InventoryImportView>(receipt);
    }
}

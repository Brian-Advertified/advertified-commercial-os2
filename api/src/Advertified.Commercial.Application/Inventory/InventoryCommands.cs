using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Application.Inventory;

public sealed record InventorySourceFile(
    string FileName,
    string DeclaredMediaType,
    byte[] Content);

public sealed record CreateInventoryImportCommand(
    string SupplierName,
    InventorySourceFile Source);

public sealed record ExecuteInventoryImportCommand;

public sealed record ReviewInventoryCandidateCommand(
    string Decision,
    string? RejectionReason,
    string? Notes,
    InventoryCandidateValues? CorrectedValues);

public sealed record PublishInventoryImportCommand;

public sealed record InventoryCandidateValues(
    string? ProductCode,
    string? Name,
    string? Channel,
    string? ProductType,
    string? Geography,
    string? Address,
    decimal? Latitude,
    decimal? Longitude,
    string? RateType,
    string? Currency,
    long? RateAmountMinor,
    string? Availability,
    IReadOnlyDictionary<string, string>? Extension);

public interface IInventoryCommands
{
    Task<CommandResult<InventoryImportView>> CreateAsync(
        CommandEnvelope<CreateInventoryImportCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<InventoryImportView>> ExecuteAsync(
        Guid importId,
        CommandEnvelope<ExecuteInventoryImportCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<InventoryCandidateView>> ReviewAsync(
        Guid candidateId,
        CommandEnvelope<ReviewInventoryCandidateCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<InventoryImportView>> PublishAsync(
        Guid importId,
        CommandEnvelope<PublishInventoryImportCommand> envelope,
        CancellationToken cancellationToken);
}

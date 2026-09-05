using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Application.Commands;

public interface IIdempotentCommandUnitOfWork
{
    /// <summary>
    /// Authoritatively applies a command once. The implementation must check the tenant-scoped
    /// idempotency key and payload hash, execute the handler, and persist the canonical outcome,
    /// audit record, and outbox message in one transaction.
    /// </summary>
    Task<CommandReceipt> ExecuteOnceAsync<TCommand>(
        CommandEnvelope<TCommand> envelope,
        Func<CancellationToken, Task<CommandOutcome>> handler,
        CancellationToken cancellationToken,
        Func<CancellationToken, Task>? authorizeResource = null)
        where TCommand : notnull;
}

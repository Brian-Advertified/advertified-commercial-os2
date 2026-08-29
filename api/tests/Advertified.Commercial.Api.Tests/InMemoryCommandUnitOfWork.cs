using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Api.Tests;

internal sealed class InMemoryCommandUnitOfWork : IIdempotentCommandUnitOfWork, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<(TenantId, IdempotencyKey), StoredOutcome> _outcomes = [];

    public int ExecutionAttempts { get; private set; }

    public async Task<CommandReceipt> ExecuteOnceAsync<TCommand>(
        CommandEnvelope<TCommand> envelope,
        Func<CancellationToken, Task<CommandOutcome>> handler,
        CancellationToken cancellationToken)
        where TCommand : notnull
    {
        ExecutionAttempts++;
        await _gate.WaitAsync(cancellationToken);

        try
        {
            var key = (envelope.TenantId, envelope.IdempotencyKey);

            if (_outcomes.TryGetValue(key, out var stored))
            {
                if (stored.PayloadHash != envelope.PayloadHash)
                {
                    throw new InvalidOperationException(
                        "An idempotency key cannot be reused for a different payload.");
                }

                var replayAudit = stored.Outcome.Audit with
                {
                    AuditId = Guid.NewGuid(),
                    Action = new ActionCode("command.duplicate_received"),
                    OccurredAtUtc = envelope.RequestedAtUtc,
                };

                return new CommandReceipt(
                    CommandDisposition.Replayed,
                    stored.Outcome,
                    replayAudit);
            }

            var outcome = await handler(cancellationToken);
            _outcomes.Add(key, new StoredOutcome(envelope.PayloadHash, outcome));

            return new CommandReceipt(
                CommandDisposition.Applied,
                outcome,
                outcome.Audit);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _gate.Dispose();
    }

    private sealed record StoredOutcome(Sha256Digest PayloadHash, CommandOutcome Outcome);
}

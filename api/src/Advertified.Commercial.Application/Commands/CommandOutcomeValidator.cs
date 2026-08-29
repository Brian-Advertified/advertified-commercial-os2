using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Application.Commands;

internal static class CommandOutcomeValidator
{
    public static void Validate<TCommand>(
        CommandEnvelope<TCommand> envelope,
        CommandOutcome outcome)
        where TCommand : notnull
    {
        if (outcome.AggregateVersion != envelope.ExpectedVersion + 1)
        {
            throw new InvalidOperationException("The outcome does not advance the expected version once.");
        }

        if (outcome.Audit.TenantId != envelope.TenantId ||
            outcome.Audit.ActorId != envelope.ActorId ||
            outcome.Audit.CommandId != envelope.CommandId ||
            outcome.Audit.CorrelationId != envelope.CorrelationId)
        {
            throw new InvalidOperationException("The audit record is not correlated to the command.");
        }

        if (outcome.Outbox.TenantId != envelope.TenantId ||
            outcome.Outbox.CausationId != envelope.CommandId ||
            outcome.Outbox.CorrelationId != envelope.CorrelationId)
        {
            throw new InvalidOperationException("The outbox message is not correlated to the command.");
        }

        if (outcome.Audit.Resource != outcome.Outbox.Aggregate ||
            outcome.Audit.Resource.Version != outcome.AggregateVersion)
        {
            throw new InvalidOperationException("Audit and outbox consequences reference different results.");
        }
    }
}

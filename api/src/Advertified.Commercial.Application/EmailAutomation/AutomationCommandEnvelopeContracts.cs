using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Application.EmailAutomation;

public interface IAutomationCommandEnvelopeFactory
{
    CommandEnvelope<TCommand> Create<TCommand>(
        TenantId tenantId,
        ActorId actorId,
        Guid runId,
        string stage,
        long expectedVersion,
        TCommand command,
        CorrelationId correlationId)
        where TCommand : notnull;
}

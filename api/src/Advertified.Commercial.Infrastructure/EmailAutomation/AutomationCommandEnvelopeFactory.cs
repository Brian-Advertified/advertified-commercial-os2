using System.Text.Json;
using Advertified.Commercial.Application.EmailAutomation;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.Opportunity;

namespace Advertified.Commercial.Infrastructure.EmailAutomation;

public sealed class AutomationCommandEnvelopeFactory(TimeProvider timeProvider)
    : IAutomationCommandEnvelopeFactory
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public CommandEnvelope<TCommand> Create<TCommand>(
        TenantId tenantId,
        ActorId actorId,
        Guid runId,
        string stage,
        long expectedVersion,
        TCommand command,
        CorrelationId correlationId)
        where TCommand : notnull
    {
        var stageKey = OpportunityCommandSupport.Hash(
            string.Concat(runId.ToString("N"), ":", stage.Trim().ToLowerInvariant()));
        var payload = JsonSerializer.Serialize(command, Json);
        return new CommandEnvelope<TCommand>(
            tenantId,
            actorId,
            new CommandId(Guid.NewGuid()),
            correlationId,
            new IdempotencyKey(string.Concat("email-auto-", stageKey)),
            new Sha256Digest(OpportunityCommandSupport.Hash(payload)),
            expectedVersion,
            timeProvider.GetUtcNow(),
            command);
    }
}

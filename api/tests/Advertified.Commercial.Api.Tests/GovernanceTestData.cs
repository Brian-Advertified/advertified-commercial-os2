using System.Text.Json;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Api.Tests;

internal sealed record TestCommand(string Value);

internal static class GovernanceTestData
{
    public static readonly TenantId Tenant = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    public static readonly TenantId OtherTenant = new(Guid.Parse("22222222-2222-2222-2222-222222222222"));
    public static readonly ActorId Actor = new(Guid.Parse("33333333-3333-3333-3333-333333333333"));
    public static readonly ActorId OtherActor = new(Guid.Parse("44444444-4444-4444-4444-444444444444"));
    public static readonly PermissionCode Permission = new("brief.approve");

    public static CommandEnvelope<TestCommand> Envelope(
        TenantId? tenantId = null,
        Sha256Digest? payloadHash = null)
    {
        return new CommandEnvelope<TestCommand>(
            tenantId ?? Tenant,
            Actor,
            new CommandId(Guid.Parse("55555555-5555-5555-5555-555555555555")),
            new CorrelationId(Guid.Parse("66666666-6666-6666-6666-666666666666")),
            new IdempotencyKey("governance-command-fixture"),
            payloadHash ?? new Sha256Digest(new string('a', 64)),
            4,
            new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero),
            new TestCommand("apply"));
    }

    public static CommandOutcome Outcome(
        CommandEnvelope<TestCommand> envelope,
        CorrelationId? correlationId = null)
    {
        var resource = new ResourceReference(
            new ResourceTypeCode("test-resource"),
            Guid.Parse("77777777-7777-7777-7777-777777777777"),
            envelope.ExpectedVersion + 1);
        var correlation = correlationId ?? envelope.CorrelationId;

        return new CommandOutcome(
            JsonSerializer.SerializeToElement(new { accepted = true }),
            resource.Version,
            new AuditRecord(
                Guid.Parse("88888888-8888-8888-8888-888888888888"),
                envelope.TenantId,
                envelope.ActorId,
                envelope.CommandId,
                correlation,
                new ActionCode("test.applied"),
                resource,
                envelope.RequestedAtUtc),
            new OutboxMessage(
                Guid.Parse("99999999-9999-9999-9999-999999999999"),
                envelope.TenantId,
                envelope.CommandId,
                correlation,
                new EventTypeCode("TestApplied"),
                resource,
                JsonSerializer.SerializeToElement(new { resource.Version }),
                envelope.RequestedAtUtc));
    }
}

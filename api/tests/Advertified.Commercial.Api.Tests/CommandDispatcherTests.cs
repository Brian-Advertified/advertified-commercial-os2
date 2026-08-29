using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Security;
using Advertified.Commercial.Domain.Governance;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class CommandDispatcherTests
{
    [Fact]
    public async Task DuplicateCommandReturnsOneCanonicalOutcomeAndAuditsReplay()
    {
        using var unitOfWork = new InMemoryCommandUnitOfWork();
        var dispatcher = new CommandDispatcher(new AllowAuthorizer(), unitOfWork);
        var envelope = GovernanceTestData.Envelope();
        var handlerCalls = 0;

        Task<CommandOutcome> Handler(CancellationToken _)
        {
            handlerCalls++;
            return Task.FromResult(GovernanceTestData.Outcome(envelope));
        }

        var first = await dispatcher.DispatchAsync(
            envelope,
            GovernanceTestData.Permission,
            Handler);
        var second = await dispatcher.DispatchAsync(
            envelope,
            GovernanceTestData.Permission,
            Handler);

        Assert.Equal(1, handlerCalls);
        Assert.Equal(CommandDisposition.Applied, first.Disposition);
        Assert.Equal(CommandDisposition.Replayed, second.Disposition);
        Assert.Equal(first.Outcome, second.Outcome);
        Assert.Equal(envelope.CorrelationId, second.ReceiptAudit.CorrelationId);
        Assert.Equal("command.duplicate_received", second.ReceiptAudit.Action.Value);
    }

    [Fact]
    public async Task DenialOccursBeforeHandlerOrIdempotencyLookup()
    {
        using var unitOfWork = new InMemoryCommandUnitOfWork();
        var dispatcher = new CommandDispatcher(new DenyAuthorizer(), unitOfWork);
        var handlerCalled = false;

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            dispatcher.DispatchAsync(
                GovernanceTestData.Envelope(tenantId: GovernanceTestData.OtherTenant),
                GovernanceTestData.Permission,
                _ =>
                {
                    handlerCalled = true;
                    return Task.FromResult(
                        GovernanceTestData.Outcome(GovernanceTestData.Envelope()));
                }));

        Assert.Equal("Tenant access denied.", exception.Message);
        Assert.False(handlerCalled);
        Assert.Equal(0, unitOfWork.ExecutionAttempts);
    }

    [Fact]
    public async Task MismatchedAuditOrOutboxCorrelationCannotBeCommitted()
    {
        using var unitOfWork = new InMemoryCommandUnitOfWork();
        var dispatcher = new CommandDispatcher(new AllowAuthorizer(), unitOfWork);
        var envelope = GovernanceTestData.Envelope();
        var wrongCorrelation = new CorrelationId(Guid.NewGuid());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dispatcher.DispatchAsync(
                envelope,
                GovernanceTestData.Permission,
                _ => Task.FromResult(
                    GovernanceTestData.Outcome(envelope, wrongCorrelation))));
    }

    [Fact]
    public async Task IdempotencyKeyCannotBeReusedForDifferentPayload()
    {
        using var unitOfWork = new InMemoryCommandUnitOfWork();
        var dispatcher = new CommandDispatcher(new AllowAuthorizer(), unitOfWork);
        var first = GovernanceTestData.Envelope();
        var changed = GovernanceTestData.Envelope(
            payloadHash: new Sha256Digest(new string('b', 64)));

        await dispatcher.DispatchAsync(
            first,
            GovernanceTestData.Permission,
            _ => Task.FromResult(GovernanceTestData.Outcome(first)));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dispatcher.DispatchAsync(
                changed,
                GovernanceTestData.Permission,
                _ => Task.FromResult(GovernanceTestData.Outcome(changed))));
    }

    private sealed class AllowAuthorizer : ITenantAuthorizer
    {
        public Task<AuthorizationDecision> AuthorizeAsync(
            ActorId actorId,
            TenantId requestedTenantId,
            PermissionCode permission,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(AuthorizationDecision.Allowed);
        }
    }

    private sealed class DenyAuthorizer : ITenantAuthorizer
    {
        public Task<AuthorizationDecision> AuthorizeAsync(
            ActorId actorId,
            TenantId requestedTenantId,
            PermissionCode permission,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(AuthorizationDecision.Denied);
        }
    }
}

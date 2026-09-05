using Advertified.Commercial.Application.Security;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Application.Commands;

public sealed class CommandDispatcher(
    ITenantAuthorizer authorizer,
    IIdempotentCommandUnitOfWork unitOfWork)
{
    public Task<CommandReceipt> DispatchAsync<TCommand>(
        CommandEnvelope<TCommand> envelope,
        PermissionCode requiredPermission,
        Func<CancellationToken, Task<CommandOutcome>> handler)
        where TCommand : notnull =>
        DispatchAsync(envelope, requiredPermission, handler, CancellationToken.None);

    public async Task<CommandReceipt> DispatchAsync<TCommand>(
        CommandEnvelope<TCommand> envelope,
        PermissionCode requiredPermission,
        Func<CancellationToken, Task<CommandOutcome>> handler,
        CancellationToken cancellationToken,
        Func<CancellationToken, Task>? authorizeResource = null)
        where TCommand : notnull
    {
        var decision = await authorizer.AuthorizeAsync(
            envelope.ActorId,
            envelope.TenantId,
            requiredPermission,
            cancellationToken);

        if (!decision.IsAllowed)
        {
            throw new UnauthorizedAccessException("Tenant access denied.");
        }

        return await unitOfWork.ExecuteOnceAsync(
            envelope,
            async token =>
            {
                var outcome = await handler(token);
                CommandOutcomeValidator.Validate(envelope, outcome);
                return outcome;
            },
            cancellationToken,
            authorizeResource);
    }
}

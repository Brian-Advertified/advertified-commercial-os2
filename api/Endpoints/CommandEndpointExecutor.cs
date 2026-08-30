using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Identity;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Api.Endpoints;

internal static class CommandEndpointExecutor
{
    internal static Task<IResult> ExecuteOkAsync<TCommand, TResult>(
        Guid tenantId,
        TCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        TimeProvider timeProvider,
        bool requireVersion,
        Func<CommandEnvelope<TCommand>, CancellationToken, Task<CommandResult<TResult>>> execute,
        CancellationToken cancellationToken)
        where TCommand : notnull
        where TResult : notnull => ExecuteAsync(
            tenantId, command, context, identity, timeProvider, requireVersion,
            execute, result => Results.Ok(result.Data), cancellationToken);

    internal static async Task<IResult> ExecuteAsync<TCommand, TResult>(
        Guid tenantId,
        TCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        TimeProvider timeProvider,
        bool requireVersion,
        Func<CommandEnvelope<TCommand>, CancellationToken, Task<CommandResult<TResult>>> execute,
        Func<CommandResult<TResult>, IResult> response,
        CancellationToken cancellationToken)
        where TCommand : notnull
        where TResult : notnull
    {
        var result = await ExecuteResultAsync(
            tenantId, command, context, identity, timeProvider,
            requireVersion, execute, cancellationToken);
        return response(result);
    }

    internal static async Task<CommandResult<TResult>> ExecuteResultAsync<TCommand, TResult>(
        Guid tenantId,
        TCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        TimeProvider timeProvider,
        bool requireVersion,
        Func<CommandEnvelope<TCommand>, CancellationToken, Task<CommandResult<TResult>>> execute,
        CancellationToken cancellationToken)
        where TCommand : notnull
        where TResult : notnull
    {
        var envelope = CommandEnvelopeFactory.Create(
            context, new TenantId(tenantId), identity.ActorId,
            command, timeProvider, requireVersion);
        var result = await execute(envelope, cancellationToken);
        CommandEnvelopeFactory.SetEntityHeaders(
            context, result.Version, result.Replayed);
        return result;
    }
}

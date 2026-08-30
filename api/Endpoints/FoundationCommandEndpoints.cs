using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Identity;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Api.Endpoints;

public static class FoundationCommandEndpoints
{
    public static RouteGroupBuilder MapFoundationCommands(this RouteGroupBuilder group)
    {
        group.MapPut("/tenants/{tenantId:guid}", UpdateTenantAsync)
            .WithName("UpdateTenant")
            .Produces<TenantView>()
            .WithCommandProblems(requiresVersion: true);
        group.MapPut("/tenants/{tenantId:guid}/me", UpdateUserAsync)
            .WithName("UpdateCurrentUser")
            .Produces<CurrentUserView>()
            .WithCommandProblems(requiresVersion: true);
        group.MapPost("/tenants/{tenantId:guid}/client-accounts", CreateClientAccountAsync)
            .WithName("CreateClientAccount")
            .Produces<ClientAccountView>(StatusCodes.Status201Created)
            .WithCommandProblems(requiresVersion: false);
        group.MapPost("/tenants/{tenantId:guid}/agencies", CreateAgencyAsync)
            .WithName("CreateAgency")
            .Produces<AgencyView>(StatusCodes.Status201Created)
            .WithCommandProblems(requiresVersion: false);
        group.MapPost("/tenants/{tenantId:guid}/contacts", CreateContactAsync)
            .WithName("CreateContact")
            .Produces<ContactView>(StatusCodes.Status201Created)
            .WithCommandProblems(requiresVersion: false);
        return group;
    }

    private static Task<IResult> UpdateTenantAsync(
        Guid tenantId,
        UpdateTenantCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IIdentityFoundationCommands commands,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
        => CommandEndpointExecutor.ExecuteOkAsync(
            tenantId, command, context, identity, timeProvider,
            requireVersion: true, commands.UpdateTenantAsync, cancellationToken);

    private static Task<IResult> UpdateUserAsync(
        Guid tenantId,
        UpdateUserCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IIdentityFoundationCommands commands,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
        => CommandEndpointExecutor.ExecuteOkAsync(
            tenantId, command, context, identity, timeProvider,
            requireVersion: true, commands.UpdateUserAsync, cancellationToken);

    private static Task<IResult> CreateClientAccountAsync(
        Guid tenantId,
        CreateClientAccountCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IBusinessFoundationCommands commands,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        CreateAsync(
            tenantId,
            command,
            context,
            identity,
            timeProvider,
            commands.CreateClientAccountAsync,
            view => $"/api/v1/tenants/{tenantId}/client-accounts/{view.Id}",
            cancellationToken);

    private static Task<IResult> CreateAgencyAsync(
        Guid tenantId,
        CreateAgencyCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IBusinessFoundationCommands commands,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        CreateAsync(
            tenantId,
            command,
            context,
            identity,
            timeProvider,
            commands.CreateAgencyAsync,
            view => $"/api/v1/tenants/{tenantId}/agencies/{view.Id}",
            cancellationToken);

    private static Task<IResult> CreateContactAsync(
        Guid tenantId,
        CreateContactCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IBusinessFoundationCommands commands,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        CreateAsync(
            tenantId,
            command,
            context,
            identity,
            timeProvider,
            commands.CreateContactAsync,
            view => $"/api/v1/tenants/{tenantId}/contacts/{view.Id}",
            cancellationToken);

    private static Task<IResult> CreateAsync<TCommand, TResult>(
        Guid tenantId,
        TCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        TimeProvider timeProvider,
        Func<CommandEnvelope<TCommand>, CancellationToken, Task<CommandResult<TResult>>> execute,
        Func<TResult, string> location,
        CancellationToken cancellationToken)
        where TCommand : notnull
        where TResult : notnull
        => CommandEndpointExecutor.ExecuteAsync(
            tenantId, command, context, identity, timeProvider,
            requireVersion: false, execute,
            result => Results.Created(location(result.Data), result.Data),
            cancellationToken);
}

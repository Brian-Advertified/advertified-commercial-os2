using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Identity;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Api.Endpoints;

public static class FoundationQueryEndpoints
{
    public static RouteGroupBuilder MapFoundationQueries(this RouteGroupBuilder group)
    {
        group.MapGet("/tenants/{tenantId:guid}", GetTenantAsync)
            .WithName("GetTenant")
            .Produces<TenantView>()
            .WithQueryProblems();
        group.MapGet("/tenants/{tenantId:guid}/client-accounts", ListClientAccountsAsync)
            .WithName("ListClientAccounts")
            .Produces<CursorPage<ClientAccountView>>()
            .WithQueryProblems();
        group.MapGet("/tenants/{tenantId:guid}/memberships", ListMembershipsAsync)
            .WithName("ListMemberships")
            .Produces<CursorPage<MembershipView>>()
            .WithQueryProblems();
        group.MapGet("/tenants/{tenantId:guid}/agencies", ListAgenciesAsync)
            .WithName("ListAgencies")
            .Produces<CursorPage<AgencyView>>()
            .WithQueryProblems();
        group.MapGet("/tenants/{tenantId:guid}/contacts", ListContactsAsync)
            .WithName("ListContacts")
            .Produces<CursorPage<ContactView>>()
            .WithQueryProblems();
        return group;
    }

    private static async Task<IResult> GetTenantAsync(
        Guid tenantId,
        HttpContext context,
        ICurrentIdentity identity,
        ICommercialFoundationReader reader,
        CancellationToken cancellationToken)
    {
        var view = await reader.GetTenantAsync(
            identity.ActorId,
            new TenantId(tenantId),
            cancellationToken);
        CommandEnvelopeFactory.SetEntityHeaders(context, view.Version);
        return Results.Ok(view);
    }

    private static Task<CursorPage<ClientAccountView>> ListClientAccountsAsync(
        Guid tenantId,
        int? limit,
        string? cursor,
        ICurrentIdentity identity,
        ICommercialFoundationReader reader,
        CancellationToken cancellationToken) =>
        reader.ListClientAccountsAsync(
            identity.ActorId,
            new TenantId(tenantId),
            limit ?? 0,
            cursor,
            cancellationToken);

    private static Task<CursorPage<MembershipView>> ListMembershipsAsync(
        Guid tenantId,
        int? limit,
        string? cursor,
        ICurrentIdentity identity,
        ICommercialFoundationReader reader,
        CancellationToken cancellationToken) =>
        reader.ListMembershipsAsync(
            identity.ActorId,
            new TenantId(tenantId),
            limit ?? 0,
            cursor,
            cancellationToken);

    private static Task<CursorPage<AgencyView>> ListAgenciesAsync(
        Guid tenantId,
        int? limit,
        string? cursor,
        ICurrentIdentity identity,
        ICommercialFoundationReader reader,
        CancellationToken cancellationToken) =>
        reader.ListAgenciesAsync(
            identity.ActorId,
            new TenantId(tenantId),
            limit ?? 0,
            cursor,
            cancellationToken);

    private static Task<CursorPage<ContactView>> ListContactsAsync(
        Guid tenantId,
        int? limit,
        string? cursor,
        ICurrentIdentity identity,
        ICommercialFoundationReader reader,
        CancellationToken cancellationToken) =>
        reader.ListContactsAsync(
            identity.ActorId,
            new TenantId(tenantId),
            limit ?? 0,
            cursor,
            cancellationToken);
}

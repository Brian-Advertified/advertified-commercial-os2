using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Application.Security;

public sealed record TenantMembership(
    TenantId TenantId,
    ActorId ActorId,
    bool IsActive,
    IReadOnlySet<PermissionCode> Permissions);

public enum AuthorizationFailure
{
    None = 0,
    TenantAccessDenied = 1,
}

public sealed record AuthorizationDecision(bool IsAllowed, AuthorizationFailure Failure)
{
    public static AuthorizationDecision Allowed { get; } =
        new(true, AuthorizationFailure.None);

    public static AuthorizationDecision Denied { get; } =
        new(false, AuthorizationFailure.TenantAccessDenied);
}

public interface ITenantMembershipSource
{
    Task<TenantMembership?> FindAsync(
        ActorId actorId,
        TenantId requestedTenantId,
        CancellationToken cancellationToken);
}

public interface ITenantAuthorizer
{
    Task<AuthorizationDecision> AuthorizeAsync(
        ActorId actorId,
        TenantId requestedTenantId,
        PermissionCode permission,
        CancellationToken cancellationToken);
}

public sealed class TenantAuthorizer(ITenantMembershipSource membershipSource) : ITenantAuthorizer
{
    public async Task<AuthorizationDecision> AuthorizeAsync(
        ActorId actorId,
        TenantId requestedTenantId,
        PermissionCode permission,
        CancellationToken cancellationToken)
    {
        var membership = await membershipSource.FindAsync(
            actorId,
            requestedTenantId,
            cancellationToken);

        if (membership is null ||
            !membership.IsActive ||
            membership.ActorId != actorId ||
            membership.TenantId != requestedTenantId ||
            !membership.Permissions.Contains(permission))
        {
            return AuthorizationDecision.Denied;
        }

        return AuthorizationDecision.Allowed;
    }
}

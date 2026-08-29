using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Application.Identity;

public interface ICurrentIdentity
{
    UserId UserId { get; }

    ActorId ActorId { get; }

    bool IsServiceIdentity { get; }
}

public interface IIdentityWorkspaceReader
{
    Task<CurrentUserView> GetCurrentUserAsync(
        UserId userId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkspaceView>> ListWorkspacesAsync(
        UserId userId,
        CancellationToken cancellationToken);
}

public sealed record CurrentUserView(
    Guid Id,
    string Email,
    string DisplayName,
    string? Phone,
    bool MfaEnabled,
    long Version);

public sealed record WorkspaceView(
    Guid MembershipId,
    Guid TenantId,
    string Name,
    string Slug,
    string RoleCode,
    long Version);

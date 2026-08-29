using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Domain.Commercial;

public sealed class Membership
{
    private Membership()
    {
    }

    public Membership(
        MembershipId id,
        TenantId tenantId,
        UserId userId,
        RoleCode role,
        LifecycleStatusCode status,
        UserId? invitedBy,
        DateTimeOffset invitedAtUtc)
    {
        Id = id;
        TenantId = tenantId;
        UserId = userId;
        Role = role;
        Status = status;
        InvitedBy = invitedBy;
        InvitedAtUtc = CommercialValue.Utc(invitedAtUtc, nameof(invitedAtUtc));
        CreatedAtUtc = InvitedAtUtc;
        UpdatedAtUtc = InvitedAtUtc;
        Version = 1;
    }

    public MembershipId Id { get; private set; }

    public TenantId TenantId { get; private set; }

    public UserId UserId { get; private set; }

    public RoleCode Role { get; private set; }

    public LifecycleStatusCode Status { get; private set; }

    public UserId? InvitedBy { get; private set; }

    public DateTimeOffset InvitedAtUtc { get; private set; }

    public DateTimeOffset? AcceptedAtUtc { get; private set; }

    public long Version { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public void Accept(
        LifecycleStatusCode acceptedStatus,
        DateTimeOffset acceptedAtUtc,
        long expectedVersion)
    {
        Version = AggregateVersion.Next(Version, expectedVersion);
        Status = acceptedStatus;
        AcceptedAtUtc = CommercialValue.Utc(acceptedAtUtc, nameof(acceptedAtUtc));
        UpdatedAtUtc = AcceptedAtUtc.Value;
    }
}

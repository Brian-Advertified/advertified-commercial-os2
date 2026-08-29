using Advertified.Commercial.Application.Security;
using Advertified.Commercial.Domain.Governance;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class TenantAuthorizerTests
{
    [Fact]
    public async Task ExactActiveMembershipWithPermissionIsAllowed()
    {
        var membership = Membership(
            GovernanceTestData.Tenant,
            GovernanceTestData.Actor,
            isActive: true,
            new HashSet<PermissionCode> { GovernanceTestData.Permission });
        var authorizer = new TenantAuthorizer(new StubMembershipSource(membership));

        var decision = await authorizer.AuthorizeAsync(
            GovernanceTestData.Actor,
            GovernanceTestData.Tenant,
            GovernanceTestData.Permission,
            CancellationToken.None);

        Assert.Equal(AuthorizationDecision.Allowed, decision);
    }

    [Fact]
    public async Task MissingInactiveCrossTenantOrMissingPermissionIsDeniedIdentically()
    {
        var deniedMemberships = new TenantMembership?[]
        {
            null,
            Membership(
                GovernanceTestData.Tenant,
                GovernanceTestData.Actor,
                isActive: false,
                new HashSet<PermissionCode> { GovernanceTestData.Permission }),
            Membership(
                GovernanceTestData.OtherTenant,
                GovernanceTestData.Actor,
                isActive: true,
                new HashSet<PermissionCode> { GovernanceTestData.Permission }),
            Membership(
                GovernanceTestData.Tenant,
                GovernanceTestData.OtherActor,
                isActive: true,
                new HashSet<PermissionCode> { GovernanceTestData.Permission }),
            Membership(
                GovernanceTestData.Tenant,
                GovernanceTestData.Actor,
                isActive: true,
                new HashSet<PermissionCode>()),
        };

        foreach (var membership in deniedMemberships)
        {
            var authorizer = new TenantAuthorizer(new StubMembershipSource(membership));
            var decision = await authorizer.AuthorizeAsync(
                GovernanceTestData.Actor,
                GovernanceTestData.Tenant,
                GovernanceTestData.Permission,
                CancellationToken.None);

            Assert.Equal(AuthorizationDecision.Denied, decision);
        }
    }

    private static TenantMembership Membership(
        TenantId tenantId,
        ActorId actorId,
        bool isActive,
        IReadOnlySet<PermissionCode> permissions)
    {
        return new TenantMembership(tenantId, actorId, isActive, permissions);
    }

    private sealed class StubMembershipSource(TenantMembership? membership)
        : ITenantMembershipSource
    {
        public Task<TenantMembership?> FindAsync(
            ActorId actorId,
            TenantId requestedTenantId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(membership);
        }
    }
}

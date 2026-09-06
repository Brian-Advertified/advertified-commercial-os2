using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Onboarding;
using Advertified.Commercial.Application.Security;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Identity;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Onboarding;
using Advertified.Commercial.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class PublicIntakeRlsProvisioningTests
{
    private static readonly TenantId PlatformTenant =
        new(Guid.Parse("a6100000-0000-4000-8000-000000000001"));
    private static readonly UserId PlatformAdmin =
        new(Guid.Parse("a6100000-0000-4000-8000-000000000002"));

    [Fact]
    [Trait("Category", "Migration")]
    public async Task ProvisioningUsesRlsContextsAndRestoresPlatformAdmin()
    {
        await using var postgres = DisposablePostgres.Create(
            "advertified_public_intake_rls",
            "advertified_public_intake_rls",
            "advertified-public-intake-rls-local-only");
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DisposablePostgres.EnableRequiredExtensionsAsync(connectionString);
        await DisposableDatabaseRoles.ProvisionAsync(connectionString);
        await SeedPlatformAsync(connectionString);

        var options = new DbContextOptionsBuilder<GovernanceDbContext>()
            .UseNpgsql(connectionString).Options;
        await using var database = new GovernanceDbContext(options);
        await using var transaction =
            await database.Database.BeginTransactionAsync();
        await ApplicationDatabaseSession.SetAsync(
            database, PlatformAdmin, PlatformTenant, CancellationToken.None);

        var provisioner = new PublicIntakeProvisioner(
            new PublicIntakeStore(database));
        var now = new DateTimeOffset(2026, 9, 6, 16, 0, 0, TimeSpan.Zero);
        var cases = new[]
        {
            new ProvisioningCase(
                MasterDataCodes.PublicIntakeTypes.Advertiser,
                MasterDataCodes.Roles.AdvertiserAdmin,
                "advertiser@example.test",
                "Advertiser Example",
                ParticipantKind.Advertiser),
            new ProvisioningCase(
                MasterDataCodes.PublicIntakeTypes.Agency,
                MasterDataCodes.Roles.AgencyAdmin,
                "agency@example.test",
                "Agency Example",
                ParticipantKind.Agency),
            new ProvisioningCase(
                MasterDataCodes.PublicIntakeTypes.MediaOwner,
                MasterDataCodes.Roles.SupplierUser,
                "owner@example.test",
                "Media Owner Example",
                ParticipantKind.Supplier),
            new ProvisioningCase(
                MasterDataCodes.PublicIntakeTypes.Creator,
                MasterDataCodes.Roles.InfluencerRep,
                "creator@example.test",
                "Creator Example",
                ParticipantKind.Supplier),
        };

        foreach (var item in cases)
        {
            var row = NewIntake(item, now);
            var result = await provisioner.ProvisionAsync(
                row,
                new ActorId(PlatformAdmin.Value),
                PlatformTenant,
                item.Name + " (Pty) Ltd",
                item.Name,
                "https://example.test/",
                null,
                requireMfa: true,
                now,
                CancellationToken.None);

            await AssertPlatformContextAsync(database);
            await AssertProvisionedAsync(database, result, item);
            await ApplicationDatabaseSession.SetAsync(
                database, PlatformAdmin, PlatformTenant, CancellationToken.None);
        }

        await transaction.CommitAsync();
    }

    [Fact]
    [Trait("Category", "Migration")]
    public async Task CommandPathPersistsPlatformAuditAndReplaysWithoutDuplicateWorkspace()
    {
        await using var postgres = DisposablePostgres.Create(
            "advertified_public_intake_command",
            "advertified_public_intake_command",
            "advertified-public-intake-command-local-only");
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DisposablePostgres.EnableRequiredExtensionsAsync(connectionString);
        await DisposableDatabaseRoles.ProvisionAsync(connectionString);
        await SeedPlatformAsync(connectionString);

        var options = new DbContextOptionsBuilder<GovernanceDbContext>()
            .UseNpgsql(connectionString).Options;
        await using var database = new GovernanceDbContext(options);
        var store = new PublicIntakeStore(database);
        var time = new FixedTimeProvider();
        var authorizer = new TenantAuthorizer(
            new DatabaseTenantMembershipSource(database));
        var unitOfWork = new PersistedCommandUnitOfWork(database, time);
        var dispatcher = new CommandDispatcher(authorizer, unitOfWork);
        var commands = new PublicIntakeCommands(
            store,
            new PublicIntakeProvisioner(store),
            dispatcher,
            time);

        var submitted = await commands.SubmitAsync(
            new SubmitPublicIntakeRequest(
                MasterDataCodes.PublicIntakeTypes.Agency,
                "Agency Owner",
                "agency-owner@example.test",
                null,
                "Agency Command Example",
                "https://example.test/",
                "Agency owner",
                "Request governed production access."),
            CancellationToken.None);
        var envelope = new CommandEnvelope<ProvisionPublicIntakeCommand>(
            PlatformTenant,
            new ActorId(PlatformAdmin.Value),
            new CommandId(Guid.NewGuid()),
            new CorrelationId(Guid.NewGuid()),
            new IdempotencyKey("public-intake-provision-agency"),
            new Sha256Digest(new string('a', 64)),
            submitted.Version,
            time.GetUtcNow(),
            new ProvisionPublicIntakeCommand(
                "Agency Command Example (Pty) Ltd",
                "Agency Command Example",
                "https://example.test/",
                null,
                true,
                "Approved after platform onboarding review."));

        var first = await commands.ProvisionAsync(
            submitted.Id, envelope, CancellationToken.None);
        var replay = await commands.ProvisionAsync(
            submitted.Id, envelope, CancellationToken.None);

        Assert.False(first.Replayed);
        Assert.True(replay.Replayed);
        Assert.Equal(first.Data.ProvisionedTenantId, replay.Data.ProvisionedTenantId);
        Assert.Equal(first.Data.ProvisionedUserId, replay.Data.ProvisionedUserId);
        Assert.Equal(MasterDataCodes.LifecycleStatuses.Approved, first.Data.Status);

        await using var verification = new GovernanceDbContext(
            new DbContextOptionsBuilder<GovernanceDbContext>()
                .UseNpgsql(connectionString).Options);
        Assert.Equal(2, await verification.Tenants.CountAsync());
        Assert.Equal(1, await verification.IdempotencyRecords.CountAsync(item =>
            item.TenantId == PlatformTenant));
        Assert.Equal(1, await verification.OutboxMessages.CountAsync(item =>
            item.TenantId == PlatformTenant));
        Assert.Equal(2, await verification.AuditEvents.CountAsync(item =>
            item.TenantId == PlatformTenant));
        var persisted = await verification.Database.SqlQuery<PublicIntakePersistedState>($"""
            SELECT status_code AS "Status",
                provisioned_tenant_id AS "TenantId",
                provisioned_user_id AS "UserId"
            FROM governance.public_intake_requests
            WHERE id = {submitted.Id}
            """).SingleAsync();
        Assert.Equal(MasterDataCodes.LifecycleStatuses.Approved, persisted.Status);
        Assert.Equal(first.Data.ProvisionedTenantId, persisted.TenantId);
        Assert.Equal(first.Data.ProvisionedUserId, persisted.UserId);
    }

    private static PublicIntakeRow NewIntake(
        ProvisioningCase item,
        DateTimeOffset now) => new(
            Guid.NewGuid(),
            item.TypeCode,
            item.Name,
            item.Email,
            null,
            item.Name,
            "https://example.test/",
            null,
            "Production access request",
            MasterDataCodes.LifecycleStatuses.Pending,
            now,
            null,
            null,
            null,
            null,
            null,
            1);

    private static async Task AssertPlatformContextAsync(
        GovernanceDbContext database)
    {
        var context = await database.Database.SqlQuery<SessionContext>($"""
            SELECT commercial.current_user_id() AS "UserId",
                commercial.current_tenant_id() AS "TenantId"
            """).SingleAsync();
        Assert.Equal(PlatformAdmin.Value, context.UserId);
        Assert.Equal(PlatformTenant.Value, context.TenantId);
    }

    private static async Task AssertProvisionedAsync(
        GovernanceDbContext database,
        PublicIntakeProvisioningResult result,
        ProvisioningCase expected)
    {
        await ApplicationDatabaseSession.SetAsync(
            database,
            new UserId(result.UserId),
            new TenantId(result.TenantId),
            CancellationToken.None);

        var workspace = await database.Database.SqlQuery<WorkspaceState>($"""
            SELECT tenant.type_code AS "TenantType",
                membership.role_code AS "Role",
                tenant.status_code AS "TenantStatus",
                account.status_code AS "UserStatus"
            FROM commercial.tenants tenant
            JOIN commercial.memberships membership
              ON membership.tenant_id = tenant.id
             AND membership.user_id = {result.UserId}
            JOIN commercial.users account ON account.id = membership.user_id
            WHERE tenant.id = {result.TenantId}
            """).SingleAsync();
        Assert.Equal(ExpectedRoleTenantType(expected.TypeCode), workspace.TenantType);
        Assert.Equal(expected.Role, workspace.Role);
        Assert.Equal(MasterDataCodes.LifecycleStatuses.Active, workspace.TenantStatus);
        Assert.Equal(MasterDataCodes.LifecycleStatuses.Active, workspace.UserStatus);

        var participantCount = expected.Kind switch
        {
            ParticipantKind.Advertiser => await CountAsync(
                database,
                $"SELECT count(*)::integer AS \"Value\" FROM commercial.client_accounts WHERE tenant_id = {result.TenantId}"),
            ParticipantKind.Agency => await CountAsync(
                database,
                $"SELECT count(*)::integer AS \"Value\" FROM commercial.agencies WHERE tenant_id = {result.TenantId}"),
            ParticipantKind.Supplier => await CountAsync(
                database,
                $"SELECT count(*)::integer AS \"Value\" FROM commercial.inventory_suppliers WHERE tenant_id = {result.TenantId}"),
            _ => 0,
        };
        Assert.Equal(1, participantCount);

        if (expected.Kind == ParticipantKind.Supplier)
        {
            var role = await database.Database.SqlQuery<string>($"""
                SELECT role_code AS "Value"
                FROM commercial.inventory_supplier_memberships
                WHERE tenant_id = {result.TenantId}
                  AND user_id = {result.UserId}
                """).SingleAsync();
            Assert.Equal(expected.Role, role);
        }
    }

    private static async Task<int> CountAsync(
        GovernanceDbContext database,
        FormattableString query) =>
        await database.Database.SqlQuery<int>(query).SingleAsync();

    private static string ExpectedRoleTenantType(string typeCode) => typeCode switch
    {
        MasterDataCodes.PublicIntakeTypes.Advertiser => MasterDataCodes.TenantTypes.Advertiser,
        MasterDataCodes.PublicIntakeTypes.Agency => MasterDataCodes.TenantTypes.Agency,
        MasterDataCodes.PublicIntakeTypes.MediaOwner => MasterDataCodes.TenantTypes.Supplier,
        MasterDataCodes.PublicIntakeTypes.Creator => MasterDataCodes.TenantTypes.Creator,
        _ => throw new ArgumentOutOfRangeException(nameof(typeCode)),
    };

    private static async Task SeedPlatformAsync(string connectionString)
    {
        var options = new DbContextOptionsBuilder<GovernanceDbContext>()
            .UseNpgsql(connectionString).Options;
        await using var database = new GovernanceDbContext(options);
        await database.Database.MigrateAsync();
        await new MasterDataBootstrapper(database, TimeProvider.System).ApplyAsync();
        var now = DateTimeOffset.UtcNow;
        database.Tenants.Add(new Tenant(
            PlatformTenant,
            new TenantTypeCode(MasterDataCodes.TenantTypes.Platform),
            "Advertified Platform",
            "Advertified",
            new Slug("advertified-platform"),
            MasterDataReferences.LifecycleStatuses.Active,
            "Africa/Johannesburg",
            new CurrencyCode(MasterDataCodes.Currencies.Zar),
            new VatStatusCode(MasterDataCodes.VatStatuses.NotApplicable),
            null,
            "{}",
            now));
        database.Users.Add(new User(
            PlatformAdmin,
            new EmailAddress("platform-admin@example.test"),
            "Platform Admin",
            null,
            MasterDataReferences.LifecycleStatuses.Active,
            true,
            now));
        database.Memberships.Add(new Membership(
            new MembershipId(Guid.NewGuid()),
            PlatformTenant,
            PlatformAdmin,
            new RoleCode(MasterDataCodes.Roles.PlatformAdmin),
            MasterDataReferences.LifecycleStatuses.Active,
            null,
            now));
        await database.SaveChangesAsync();
    }

    private sealed record ProvisioningCase(
        string TypeCode,
        string Role,
        string Email,
        string Name,
        ParticipantKind Kind);

    private sealed record SessionContext(Guid UserId, Guid TenantId);
    private sealed record WorkspaceState(
        string TenantType,
        string Role,
        string TenantStatus,
        string UserStatus);
    private sealed record PublicIntakePersistedState(
        string Status,
        Guid? TenantId,
        Guid? UserId);

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(2026, 9, 6, 16, 30, 0, TimeSpan.Zero);
    }

    private enum ParticipantKind
    {
        Advertiser,
        Agency,
        Supplier,
    }
}

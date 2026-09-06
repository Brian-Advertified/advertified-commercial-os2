using System.Security.Cryptography;
using System.Text;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Identity;

internal static class UserLoginDirectory
{
    internal static async Task<Guid?> FindUserIdAsync(
        GovernanceDbContext database,
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        var value = await database.Database.SqlQuery<Guid>($"""
            SELECT user_id AS "Value"
            FROM governance.user_login_handles
            WHERE email_hash = {HashEmail(normalizedEmail)}
            """).SingleOrDefaultAsync(cancellationToken);
        return value == Guid.Empty ? null : value;
    }

    internal static Task<Guid> EnsureInvitedUserAsync(
        GovernanceDbContext database,
        ActorId inviter,
        TenantId tenantId,
        string normalizedEmail,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        EnsureUserAsync(
            database,
            inviter,
            tenantId,
            normalizedEmail,
            DisplayName(normalizedEmail),
            phone: null,
            requireMfa: true,
            now,
            cancellationToken);

    internal static Task<Guid> EnsureProvisionedUserAsync(
        GovernanceDbContext database,
        ActorId provisionedBy,
        TenantId provisioningTenantId,
        string normalizedEmail,
        string displayName,
        string? phone,
        bool requireMfa,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        EnsureUserAsync(
            database,
            provisionedBy,
            provisioningTenantId,
            normalizedEmail,
            displayName,
            phone,
            requireMfa,
            now,
            cancellationToken);

    internal static string HashEmail(string normalizedEmail) =>
        Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes(normalizedEmail.Trim().ToLowerInvariant())));

    private static async Task<Guid> EnsureUserAsync(
        GovernanceDbContext database,
        ActorId operatorId,
        TenantId operatorTenantId,
        string normalizedEmail,
        string displayName,
        string? phone,
        bool requireMfa,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var resolvedUserId = await FindUserIdAsync(
            database, normalizedEmail, cancellationToken);
        if (!resolvedUserId.HasValue)
        {
            await CreateCandidateAsync(
                database,
                operatorId,
                operatorTenantId,
                normalizedEmail,
                displayName,
                phone,
                requireMfa,
                now,
                cancellationToken);
            resolvedUserId = await FindUserIdAsync(
                database, normalizedEmail, cancellationToken);
        }

        var userId = resolvedUserId ?? throw new InvalidOperationException(
            "The user identity could not be prepared safely.");
        await EnsureActiveAndMfaAsync(
            database,
            operatorId,
            operatorTenantId,
            userId,
            requireMfa,
            now,
            cancellationToken);
        return userId;
    }

    private static async Task CreateCandidateAsync(
        GovernanceDbContext database,
        ActorId operatorId,
        TenantId operatorTenantId,
        string normalizedEmail,
        string displayName,
        string? phone,
        bool requireMfa,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var candidateId = Guid.NewGuid();
        await ApplicationDatabaseSession.SetAsync(
            database,
            new UserId(candidateId),
            operatorTenantId,
            cancellationToken);
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.users (
                id, email, display_name, phone, status_code, mfa_enabled,
                version, created_at_utc, updated_at_utc)
            VALUES (
                {candidateId}, {normalizedEmail}, {displayName}, {phone},
                {MasterDataCodes.LifecycleStatuses.Active}, {requireMfa},
                1, {now}, {now})
            ON CONFLICT (email) DO NOTHING
            """, cancellationToken);
        await RestoreOperatorAsync(
            database, operatorId, operatorTenantId, cancellationToken);
    }

    private static async Task EnsureActiveAndMfaAsync(
        GovernanceDbContext database,
        ActorId operatorId,
        TenantId operatorTenantId,
        Guid userId,
        bool requireMfa,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await ApplicationDatabaseSession.SetAsync(
            database,
            new UserId(userId),
            operatorTenantId,
            cancellationToken);
        var state = await database.Database.SqlQuery<UserLoginState>($"""
            SELECT status_code AS "Status", mfa_enabled AS "MfaEnabled"
            FROM commercial.users
            WHERE id = {userId}
            """).SingleOrDefaultAsync(cancellationToken);
        if (state is null || state.Status != MasterDataCodes.LifecycleStatuses.Active)
        {
            await RestoreOperatorAsync(
                database, operatorId, operatorTenantId, cancellationToken);
            throw new UnauthorizedAccessException("Identity access denied.");
        }
        if (requireMfa && !state.MfaEnabled)
        {
            await database.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE commercial.users
                SET mfa_enabled = TRUE, version = version + 1,
                    updated_at_utc = {now}
                WHERE id = {userId}
                """, cancellationToken);
        }
        await RestoreOperatorAsync(
            database, operatorId, operatorTenantId, cancellationToken);
    }

    private static Task RestoreOperatorAsync(
        GovernanceDbContext database,
        ActorId operatorId,
        TenantId operatorTenantId,
        CancellationToken cancellationToken) =>
        ApplicationDatabaseSession.SetAsync(
            database,
            new UserId(operatorId.Value),
            operatorTenantId,
            cancellationToken);

    private static string DisplayName(string email)
    {
        var localPart = email.Split('@', 2)[0];
        return localPart.Length <= 200 ? localPart : localPart[..200];
    }

    private sealed record UserLoginState(string Status, bool MfaEnabled);
}

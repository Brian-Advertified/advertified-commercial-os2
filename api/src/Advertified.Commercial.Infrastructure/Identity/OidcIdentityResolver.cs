using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Identity;

public sealed record OidcIdentityResolution(
    UserId UserId,
    ActorId ActorId,
    bool MfaRequired);

public sealed class OidcIdentityResolver(
    GovernanceDbContext database,
    TimeProvider timeProvider)
{
    public async Task<OidcIdentityResolution> ResolveAsync(
        string providerCode,
        string subject,
        string email,
        bool emailVerified,
        CancellationToken cancellationToken)
    {
        var provider = Required(providerCode, 50);
        var subjectHash = Hash(Required(subject, 2_000));
        var normalizedEmail = Required(email, 320);
        await using var transaction =
            await database.Database.BeginTransactionAsync(cancellationToken);
        var connection = await OpenConnectionAsync(cancellationToken);

        var existing = await FindBindingAsync(
            connection, provider, subjectHash, cancellationToken);
        if (existing is not null)
        {
            if (!existing.IsActive)
            {
                throw new UnauthorizedAccessException("Identity access denied.");
            }
            await RecordLoginAsync(connection, provider, subjectHash, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ToResolution(existing);
        }

        if (!emailVerified)
        {
            throw new UnauthorizedAccessException("Verified email is required for first sign in.");
        }
        var user = await FindUniqueUserAsync(connection, normalizedEmail, cancellationToken)
            ?? throw new UnauthorizedAccessException("Identity access denied.");
        await BindAsync(connection, provider, subjectHash, user.UserId, cancellationToken);
        existing = await FindBindingAsync(connection, provider, subjectHash, cancellationToken);
        if (existing is null || existing.UserId != user.UserId || !existing.IsActive)
        {
            throw new UnauthorizedAccessException("Identity binding could not be established safely.");
        }
        await RecordLoginAsync(connection, provider, subjectHash, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResolution(existing);
    }

    private async Task<BindingRow?> FindBindingAsync(
        DbConnection connection,
        string provider,
        string subjectHash,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT identity.user_id, user_record.mfa_enabled,
                user_record.status_code = @active_status AS is_active
            FROM commercial.external_identities identity
            JOIN commercial.users user_record ON user_record.id = identity.user_id
            WHERE identity.provider_code = @provider
              AND identity.subject_hash = @subject_hash;
            """;
        Add(command, "provider", provider);
        Add(command, "subject_hash", subjectHash);
        Add(command, "active_status", MasterDataCodes.LifecycleStatuses.Active);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new BindingRow(reader.GetGuid(0), reader.GetBoolean(1), reader.GetBoolean(2))
            : null;
    }

    private static async Task<BindingRow?> FindUniqueUserAsync(
        DbConnection connection,
        string email,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, mfa_enabled, TRUE AS is_active
            FROM commercial.users
            WHERE status_code = @active_status
              AND lower(email) = lower(@email)
            ORDER BY id
            LIMIT 2;
            """;
        Add(command, "active_status", MasterDataCodes.LifecycleStatuses.Active);
        Add(command, "email", email);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }
        var result = new BindingRow(reader.GetGuid(0), reader.GetBoolean(1), true);
        return await reader.ReadAsync(cancellationToken) ? null : result;
    }

    private async Task BindAsync(
        DbConnection connection,
        string provider,
        string subjectHash,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO commercial.external_identities (
                provider_code, subject_hash, user_id, created_at_utc, last_login_at_utc)
            VALUES (@provider, @subject_hash, @user_id, @now_utc, @now_utc)
            ON CONFLICT (provider_code, subject_hash) DO NOTHING;
            """;
        Add(command, "provider", provider);
        Add(command, "subject_hash", subjectHash);
        Add(command, "user_id", userId);
        Add(command, "now_utc", timeProvider.GetUtcNow());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task RecordLoginAsync(
        DbConnection connection,
        string provider,
        string subjectHash,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE commercial.external_identities
            SET last_login_at_utc = @now_utc
            WHERE provider_code = @provider AND subject_hash = @subject_hash;
            """;
        Add(command, "provider", provider);
        Add(command, "subject_hash", subjectHash);
        Add(command, "now_utc", timeProvider.GetUtcNow());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async ValueTask<DbConnection> OpenConnectionAsync(
        CancellationToken cancellationToken)
    {
        var connection = database.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }
        return connection;
    }

    private static OidcIdentityResolution ToResolution(BindingRow row) => new(
        new UserId(row.UserId),
        new ActorId(row.UserId),
        row.MfaRequired);

    private static void Add(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static string Hash(string value) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string Required(string value, int maximumLength)
    {
        var result = value.Trim();
        return result.Length is > 0 && result.Length <= maximumLength
            ? result
            : throw new UnauthorizedAccessException("Identity access denied.");
    }

    private sealed record BindingRow(Guid UserId, bool MfaRequired, bool IsActive);
}

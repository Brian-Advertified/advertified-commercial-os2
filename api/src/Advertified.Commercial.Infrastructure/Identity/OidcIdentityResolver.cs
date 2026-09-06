using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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
        var normalizedEmail = new EmailAddress(Required(email, 320)).Value;
        await using var transaction =
            await database.Database.BeginTransactionAsync(cancellationToken);
        await ApplicationDatabaseSession.SetAsync(
            database, null, null, cancellationToken);
        var connection = await OpenConnectionAsync(cancellationToken);

        var boundUserId = await FindBoundUserIdAsync(
            connection, provider, subjectHash, cancellationToken);
        if (boundUserId.HasValue)
        {
            var existing = await RequireActiveUserAsync(
                boundUserId.Value, cancellationToken);
            await RecordLoginAsync(connection, provider, subjectHash, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ToResolution(existing);
        }

        if (!emailVerified)
        {
            throw new UnauthorizedAccessException(
                "Verified email is required for first sign in.");
        }
        var userId = await UserLoginDirectory.FindUserIdAsync(
            database, normalizedEmail, cancellationToken)
            ?? throw new UnauthorizedAccessException("Identity access denied.");
        var user = await RequireActiveUserAsync(userId, cancellationToken);
        await BindAsync(connection, provider, subjectHash, user.UserId, cancellationToken);
        var established = await FindBoundUserIdAsync(
            connection, provider, subjectHash, cancellationToken);
        if (established != user.UserId)
        {
            throw new UnauthorizedAccessException(
                "Identity binding could not be established safely.");
        }
        await RecordLoginAsync(connection, provider, subjectHash, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResolution(user);
    }

    private async Task<BindingRow> RequireActiveUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        await ApplicationDatabaseSession.SetAsync(
            database, new UserId(userId), null, cancellationToken);
        var connection = await OpenConnectionAsync(cancellationToken);
        var user = await FindUserAsync(connection, userId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            throw new UnauthorizedAccessException("Identity access denied.");
        }
        return user;
    }

    private static async Task<Guid?> FindBoundUserIdAsync(
        DbConnection connection,
        string provider,
        string subjectHash,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT user_id
            FROM commercial.external_identities
            WHERE provider_code = @provider AND subject_hash = @subject_hash;
            """;
        Add(command, "provider", provider);
        Add(command, "subject_hash", subjectHash);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is Guid userId ? userId : null;
    }

    private static async Task<BindingRow?> FindUserAsync(
        DbConnection connection,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, mfa_enabled, status_code = @active_status AS is_active
            FROM commercial.users
            WHERE id = @user_id;
            """;
        Add(command, "active_status", MasterDataCodes.LifecycleStatuses.Active);
        Add(command, "user_id", userId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new BindingRow(reader.GetGuid(0), reader.GetBoolean(1), reader.GetBoolean(2))
            : null;
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
        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (PostgresException exception) when (
            exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new UnauthorizedAccessException("Identity access denied.", exception);
        }
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

using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Advertified.Commercial.Application.Identity;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Identity;

public sealed class PostgresBrowserSessionStore(
    GovernanceDbContext database,
    TimeProvider timeProvider) : IBrowserSessionStore
{
    private const int TokenBytes = 32;

    public async ValueTask<BrowserSessionHandle> CreateAsync(
        BrowserSessionIdentity identity,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        if (identity.ExpiresAtUtc <= now)
        {
            throw new InvalidOperationException("The browser session expiry must be in the future.");
        }

        var token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(TokenBytes));
        var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO commercial.browser_sessions (
                token_hash, user_id, actor_id, is_service_identity,
                created_at_utc, expires_at_utc, invalidated_at_utc)
            VALUES (
                @token_hash, @user_id, @actor_id, @is_service_identity,
                @created_at_utc, @expires_at_utc, NULL);
            """;
        AddParameter(command, "token_hash", Hash(token));
        AddParameter(command, "user_id", identity.UserId.Value);
        AddParameter(command, "actor_id", identity.ActorId.Value);
        AddParameter(command, "is_service_identity", identity.IsServiceIdentity);
        AddParameter(command, "created_at_utc", now);
        AddParameter(command, "expires_at_utc", identity.ExpiresAtUtc);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return new BrowserSessionHandle(token, identity);
    }

    public async ValueTask<BrowserSessionIdentity?> ResolveAsync(
        string token,
        CancellationToken cancellationToken)
    {
        var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT user_id, actor_id, is_service_identity, expires_at_utc
            FROM commercial.browser_sessions
            WHERE token_hash = @token_hash
              AND invalidated_at_utc IS NULL
              AND expires_at_utc > @now_utc;
            """;
        AddParameter(command, "token_hash", Hash(token));
        AddParameter(command, "now_utc", timeProvider.GetUtcNow());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new BrowserSessionIdentity(
            new UserId(reader.GetGuid(0)),
            new ActorId(reader.GetGuid(1)),
            reader.GetBoolean(2),
            reader.GetFieldValue<DateTimeOffset>(3));
    }

    public async ValueTask InvalidateAsync(
        string token,
        CancellationToken cancellationToken)
    {
        var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE commercial.browser_sessions
            SET invalidated_at_utc = COALESCE(invalidated_at_utc, @invalidated_at_utc)
            WHERE token_hash = @token_hash;
            """;
        AddParameter(command, "invalidated_at_utc", timeProvider.GetUtcNow());
        AddParameter(command, "token_hash", Hash(token));
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

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

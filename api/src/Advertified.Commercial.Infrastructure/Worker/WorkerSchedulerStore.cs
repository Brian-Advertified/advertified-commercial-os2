using System.Globalization;
using Npgsql;

namespace Advertified.Commercial.Infrastructure.Worker;

public sealed class WorkerSchedulerStore(string connectionString)
{
    public async Task<bool> CheckHealthAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand("SELECT 1", connection);
            return Convert.ToInt32(
                await command.ExecuteScalarAsync(cancellationToken),
                CultureInfo.InvariantCulture) == 1;
        }
        catch (NpgsqlException)
        {
            return false;
        }
    }

    public async Task<Guid?> NextOutboxTenantAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            "SELECT commercial.next_outbox_tenant()", connection);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is Guid value ? value : null;
    }

    public async Task<EmailWorkerClaim?> ClaimEmailAsync(
        Guid workerId,
        int leaseSeconds,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT tenant_id, inbound_email_id, owner_user_id, correlation_id, claim_token
            FROM commercial.claim_next_email_work(@worker_id, @lease_seconds)
            """,
            connection);
        command.Parameters.AddWithValue("worker_id", workerId);
        command.Parameters.AddWithValue("lease_seconds", leaseSeconds);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new EmailWorkerClaim(
                reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2),
                reader.GetGuid(3), reader.GetGuid(4))
            : null;
    }

    public async Task<bool> HeartbeatEmailAsync(
        Guid claimToken,
        int leaseSeconds,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            "SELECT commercial.heartbeat_email_work(@claim_token, @lease_seconds)",
            connection);
        command.Parameters.AddWithValue("claim_token", claimToken);
        command.Parameters.AddWithValue("lease_seconds", leaseSeconds);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    public async Task<string> CompleteEmailAsync(
        Guid claimToken,
        bool success,
        string? failureCode,
        int failureDelaySeconds,
        int maxAttempts,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT commercial.complete_email_work(
                @claim_token, @success, @failure_code,
                @failure_delay_seconds, @max_attempts)
            """,
            connection);
        command.Parameters.AddWithValue("claim_token", claimToken);
        command.Parameters.AddWithValue("success", success);
        command.Parameters.AddWithValue(
            "failure_code",
            failureCode is null ? DBNull.Value : failureCode);
        command.Parameters.AddWithValue("failure_delay_seconds", failureDelaySeconds);
        command.Parameters.AddWithValue("max_attempts", maxAttempts);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result as string ?? EmailWorkerCompletion.Fenced;
    }

    private async Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var role = new NpgsqlCommand("SET ROLE advertified_worker", connection);
        await role.ExecuteNonQueryAsync(cancellationToken);
        return connection;
    }
}

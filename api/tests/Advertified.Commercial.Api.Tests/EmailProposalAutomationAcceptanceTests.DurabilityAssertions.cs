using Advertified.Commercial.Domain.MasterData;
using Npgsql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class CanonicalPlanningAcceptanceTests
{
    private static async Task AssertProposalStatusAsync(
        string connectionString,
        Guid proposalId,
        string expectedStatus)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT status_code FROM commercial.proposal_versions " +
            "WHERE tenant_id = @tenantId AND id = @proposalId",
            connection);
        command.Parameters.AddWithValue("tenantId", TenantId);
        command.Parameters.AddWithValue("proposalId", proposalId);
        Assert.Equal(expectedStatus, await command.ExecuteScalarAsync());
    }

    private static async Task AssertDeliveryConsequencesAsync(
        string connectionString,
        Guid runId,
        bool accepted,
        bool sent)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT
                (SELECT count(*) FROM commercial.email_proposal_automation_runs
                 WHERE tenant_id = @tenantId AND id = @runId),
                (SELECT count(*) FROM commercial.proposal_versions proposal
                 JOIN commercial.email_proposal_automation_runs run
                   ON run.tenant_id = proposal.tenant_id
                  AND run.proposal_version_id = proposal.id
                 WHERE run.tenant_id = @tenantId AND run.id = @runId),
                (SELECT count(*) FROM commercial.audit_events
                 WHERE tenant_id = @tenantId AND resource_id = @runId
                   AND action_code = @requestedAction),
                (SELECT count(*) FROM commercial.outbox_messages
                 WHERE tenant_id = @tenantId AND aggregate_id = @runId
                   AND event_type_code = @requestedEvent),
                (SELECT count(*) FROM commercial.audit_events
                 WHERE tenant_id = @tenantId AND resource_id = @runId
                   AND action_code = @acceptedAction),
                (SELECT count(*) FROM commercial.outbox_messages
                 WHERE tenant_id = @tenantId AND aggregate_id = @runId
                   AND event_type_code = @acceptedEvent),
                (SELECT count(*) FROM commercial.audit_events
                 WHERE tenant_id = @tenantId AND resource_id = @runId
                   AND action_code = @sentAction),
                (SELECT count(*) FROM commercial.outbox_messages
                 WHERE tenant_id = @tenantId AND aggregate_id = @runId
                   AND event_type_code = @sentEvent)
            """,
            connection);
        command.Parameters.AddWithValue("tenantId", TenantId);
        command.Parameters.AddWithValue("runId", runId);
        command.Parameters.AddWithValue(
            "requestedAction",
            MasterDataCodes.CommercialActions.EmailAutomationDeliveryRequested);
        command.Parameters.AddWithValue(
            "requestedEvent",
            MasterDataCodes.CommercialEventTypes.EmailProposalDeliveryRequested);
        command.Parameters.AddWithValue(
            "acceptedAction",
            MasterDataCodes.CommercialActions.EmailAutomationDeliveryAccepted);
        command.Parameters.AddWithValue(
            "acceptedEvent",
            MasterDataCodes.CommercialEventTypes.EmailProposalDeliveryAccepted);
        command.Parameters.AddWithValue(
            "sentAction", MasterDataCodes.CommercialActions.EmailAutomationSent);
        command.Parameters.AddWithValue(
            "sentEvent", MasterDataCodes.CommercialEventTypes.EmailProposalAutomationSent);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1L, reader.GetInt64(0));
        Assert.Equal(1L, reader.GetInt64(1));
        Assert.Equal(1L, reader.GetInt64(2));
        Assert.Equal(1L, reader.GetInt64(3));
        var acceptedCount = accepted ? 1L : 0L;
        var sentCount = sent ? 1L : 0L;
        Assert.Equal(acceptedCount, reader.GetInt64(4));
        Assert.Equal(acceptedCount, reader.GetInt64(5));
        Assert.Equal(sentCount, reader.GetInt64(6));
        Assert.Equal(sentCount, reader.GetInt64(7));
    }

    private static async Task SetProposalStatusAsync(
        string connectionString,
        Guid proposalId,
        string status)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "UPDATE commercial.proposal_versions SET status_code = @status " +
            "WHERE tenant_id = @tenantId AND id = @proposalId",
            connection);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("tenantId", TenantId);
        command.Parameters.AddWithValue("proposalId", proposalId);
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }

    private static async Task ResetRunForConcurrentProcessingAsync(
        string connectionString,
        Guid inboundEmailId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            UPDATE commercial.email_proposal_automation_runs
            SET status_code = 'RECEIVED', failure_collection_code = NULL,
                failure_code = NULL, failure_message = NULL, version = version + 1
            WHERE tenant_id = @tenantId AND inbound_email_id = @inboundEmailId
              AND checkpoint_code = 'DOCUMENT_RENDERED'
              AND delivery_requested_at_utc IS NULL
            """,
            connection);
        command.Parameters.AddWithValue("tenantId", TenantId);
        command.Parameters.AddWithValue("inboundEmailId", inboundEmailId);
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }
}

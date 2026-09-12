using System.Text.Json;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Opportunity;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Opportunity;

public sealed partial class OpportunityCommands
{
    private async Task RejectExactDuplicateAsync(
        CommandEnvelope<CreateOpportunityCommand> envelope,
        string sourceType,
        string? sourceRef,
        CancellationToken cancellationToken)
    {
        if (sourceRef is null) return;
        // The command unit of work owns this ReadCommitted transaction. Lock before
        // reading so a concurrent creation is visible after its transaction commits.
        // JSON preserves field boundaries; hash collisions only serialize extra work.
        var identity = JsonSerializer.Serialize(new[]
        {
            "opportunity-source", envelope.TenantId.Value.ToString(),
            envelope.Command.ClientId.ToString(), sourceType, sourceRef,
        });
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            SELECT pg_advisory_xact_lock(hashtextextended({identity}, 0))
            """, cancellationToken);
        var exists = await store.DbContext.Database.SqlQuery<bool>($"""
            SELECT EXISTS (
                SELECT 1 FROM commercial.opportunities
                WHERE tenant_id = {envelope.TenantId.Value}
                  AND client_account_id = {envelope.Command.ClientId}
                  AND source_type_code = {sourceType}
                  AND source_ref COLLATE "C" = {sourceRef} COLLATE "C"
            ) AS "Value"
            """).SingleAsync(cancellationToken);
        if (exists) throw new DuplicateOpportunityException();
    }
}

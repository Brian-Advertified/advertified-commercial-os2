using System.Text.Json;
using Advertified.Commercial.Application.Brief;
using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Brief;

internal static class BriefInterpretationBinding
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    internal static async Task ValidateAsync(GovernanceDbContext db,
        CommandEnvelope<CreateBriefCommand> envelope, CancellationToken cancellationToken)
    {
        if (envelope.Command.InterpretationId is not { } id) return;
        var row = await db.Database.SqlQuery<BindingRow>($"""
            SELECT source.source_title AS "Title", source.source_content AS "Content",
                CASE WHEN step.status_code = {MasterDataCodes.LifecycleStatuses.Completed}
                    THEN step.output_json::text END AS "Output"
            FROM commercial.supplied_brief_interpretations source
            JOIN commercial.agent_run_steps step ON step.tenant_id = source.tenant_id AND step.run_id = source.id
            WHERE source.tenant_id = {envelope.TenantId.Value} AND source.actor_id = {envelope.ActorId.Value}
                AND source.id = {id}
            """).SingleOrDefaultAsync(cancellationToken)
            ?? throw new UnauthorizedAccessException("Supplied Brief interpretation access denied.");
        if (row.Title != envelope.Command.SourceTitle.Trim() || row.Content != envelope.Command.SourceContent)
            throw new ArgumentException("The Brief source must match the retained interpretation exactly.");
        var result = row.Output is null ? null : JsonSerializer.Deserialize<SuppliedBriefUnderstandingView>(
            row.Output, JsonOptions);
        if (result is null || result.RequiresHumanClarification)
            throw new InvalidOperationException("A validated, clarified Brief interpretation is required.");
    }

    private sealed record BindingRow(string Title, string Content, string? Output);
}

using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Domain.Governance;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Opportunity;

public sealed partial class OpportunityCommands
{
    private async Task InsertSourceAsync(
        CommandEnvelope<RegisterEvidenceSourceCommand> envelope,
        RegisterEvidenceSourceCommand command,
        CapturedSource captured,
        Guid sourceId,
        string hash,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var policy = OpportunityCommandSupport.Required(
            command.PolicyBasis, 100, nameof(command.PolicyBasis)).ToUpperInvariant();
        await OpportunityCommandSupport.EnsureCodeAsync(
            store.DbContext, MasterDataCodes.EvidenceSourceTypes.Collection, captured.Type, cancellationToken);
        await OpportunityCommandSupport.EnsureCodeAsync(
            store.DbContext, MasterDataCodes.EvidencePolicyBases.Collection, policy, cancellationToken);
        var objectKey = $"evidence/{hash[..2]}/{hash}";
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.evidence_sources (
                id, tenant_id, type_code, locator, title, content_hash, object_key,
                content_text, policy_code, capture_status_code, created_by,
                captured_at_utc, version)
            VALUES (
                {sourceId}, {envelope.TenantId.Value}, {captured.Type},
                {OpportunityCommandSupport.Required(command.Locator, 2048, nameof(command.Locator))},
                {OpportunityCommandSupport.Required(command.Title, 300, nameof(command.Title))},
                {hash}, {objectKey}, {captured.Content}, {policy}, {MasterDataCodes.LifecycleStatuses.Completed},
                {envelope.ActorId.Value}, {now}, 1)
            """, cancellationToken);
    }

    private async Task<bool> LinkSourceAsync(
        CommandEnvelope<RegisterEvidenceSourceCommand> envelope,
        Guid opportunityId,
        Guid sourceId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.opportunity_evidence_sources (
                tenant_id, opportunity_id, source_id, linked_by, linked_at_utc)
            VALUES (
                {envelope.TenantId.Value}, {opportunityId}, {sourceId},
                {envelope.ActorId.Value}, {now})
            ON CONFLICT DO NOTHING
            """, cancellationToken);
        return changed == 1;
    }

    private Task InsertClaimsAsync(
        CommandEnvelope<RegisterEvidenceSourceCommand> envelope,
        RegisterEvidenceSourceCommand command,
        CapturedSource captured,
        Guid sourceId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        OpportunityEvidenceBatchPersistence.InsertClaimsAsync(
            store.DbContext, envelope.TenantId, command.OpportunityId, sourceId,
            envelope.ActorId.Value, command.ReviewerUserId, captured.Claims, now,
            cancellationToken);
}

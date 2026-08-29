using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Constants;
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
            store.DbContext, "evidenceSourceTypes", captured.Type, cancellationToken);
        await OpportunityCommandSupport.EnsureCodeAsync(
            store.DbContext, "evidencePolicyBases", policy, cancellationToken);
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
                {hash}, {objectKey}, {captured.Content}, {policy}, {Gate4Statuses.Completed},
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

    private async Task InsertClaimsAsync(
        CommandEnvelope<RegisterEvidenceSourceCommand> envelope,
        RegisterEvidenceSourceCommand command,
        CapturedSource captured,
        Guid sourceId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (captured.Claims.Count == 0)
        {
            throw new ArgumentException("At least one candidate evidence claim is required.");
        }

        foreach (var claim in captured.Claims)
        {
            var claimType = OpportunityCommandSupport.Required(
                claim.ClaimType, 100, nameof(claim.ClaimType)).ToUpperInvariant();
            await OpportunityCommandSupport.EnsureCodeAsync(
                store.DbContext, "evidenceClaimTypes", claimType, cancellationToken);
            if (claim.Confidence is < 0 or > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(captured));
            }
            var itemId = Guid.NewGuid();
            var value = OpportunityCommandSupport.Json(
                claim.StructuredValueJson, nameof(claim.StructuredValueJson));
            await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO commercial.evidence_items (
                    id, tenant_id, opportunity_id, source_id, locator, claim_type_code,
                    original_value_json, excerpt, confidence, review_status_code,
                    created_by, version, created_at_utc, updated_at_utc)
                VALUES (
                    {itemId}, {envelope.TenantId.Value}, {command.OpportunityId}, {sourceId},
                    {OpportunityCommandSupport.Required(claim.Locator, 500, nameof(claim.Locator))},
                    {claimType}, {value}::jsonb,
                    {OpportunityCommandSupport.Required(claim.Excerpt, 2000, nameof(claim.Excerpt))},
                    {claim.Confidence}, {Gate4Statuses.Pending}, {envelope.ActorId.Value},
                    1, {now}, {now})
                """, cancellationToken);
            await OpportunityCommandSupport.CreateTaskAsync(
                store.DbContext, envelope.TenantId, command.OpportunityId,
                Gate4TaskTypes.EvidenceItemReview, "Review captured evidence",
                "Only reviewed source claims can support opportunity recommendations.",
                CommercialResourceTypes.EvidenceItem, itemId, 1, command.ReviewerUserId,
                now, cancellationToken);
        }
    }
}

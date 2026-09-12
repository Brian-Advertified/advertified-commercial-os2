using Advertified.Commercial.Application.Reporting;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Reporting;

public sealed class CommercialMemoryStore(GovernanceDbContext dbContext)
{
    internal async Task<CommercialMemoryData> ReadAsync(
        ActorId actorId,
        TenantId tenantId,
        CommercialMemoryQuery query,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await ApplicationDatabaseSession.SetAsync(
            dbContext, new UserId(actorId.Value), tenantId, cancellationToken);
        var exchanges = await ReadExchangesAsync(tenantId, query, cancellationToken);
        var bookings = await ReadBookingsAsync(tenantId, query, cancellationToken);
        var proofs = await ReadProofsAsync(tenantId, query, cancellationToken);
        var selections = await ReadSelectionsAsync(tenantId, query, cancellationToken);
        var approvals = await ReadProposalApprovalsAsync(tenantId, query, cancellationToken);
        var decisions = await ReadProposalDecisionsAsync(tenantId, query, cancellationToken);
        var performance = await ReadPerformanceAsync(tenantId, query, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(exchanges, bookings, proofs, selections, approvals, decisions, performance);
    }

    private Task<List<CommercialMemoryExchangeRow>> ReadExchangesAsync(
        TenantId tenantId,
        CommercialMemoryQuery query,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<CommercialMemoryExchangeRow>($"""
            SELECT rfq.id AS "RfqId", rfq.version AS "RfqVersion",
                rfq.sent_at_utc AS "SentAtUtc", rfq.supplier_tenant_id AS "SupplierTenantId",
                listing.channel_code AS "Channel",
                listing.amount_minor AS "ListedAmountMinor",
                listing.currency_code AS "ListedCurrency",
                COALESCE(response_stats.response_count, 0)::int AS "ResponseCount",
                response_stats.first_response_id AS "FirstResponseId",
                response_stats.first_response_version AS "FirstResponseVersion",
                response_stats.first_response_amount_minor AS "FirstResponseAmountMinor",
                response_stats.first_response_currency AS "FirstResponseCurrency",
                response_stats.first_submitted_at_utc AS "FirstSubmittedAtUtc",
                accepted.response_id AS "AcceptedResponseId",
                accepted.response_version AS "AcceptedResponseVersion",
                accepted.amount_minor AS "AcceptedAmountMinor",
                accepted.currency_code AS "AcceptedCurrency",
                accepted.submitted_at_utc AS "AcceptedResponseSubmittedAtUtc",
                accepted.accepted_at_utc AS "AcceptedAtUtc",
                booking.id AS "BookingId",
                booking.version AS "BookingVersion",
                booking.requested_at_utc AS "BookingRequestedAtUtc",
                booking.confirmed_at_utc AS "BookingConfirmedAtUtc"
            FROM commercial.marketplace_rfqs rfq
            JOIN commercial.marketplace_listing_versions listing
              ON listing.supplier_tenant_id = rfq.supplier_tenant_id
             AND listing.id = rfq.listing_version_id
            LEFT JOIN LATERAL (
                SELECT count(*)::int AS response_count,
                    (array_agg(response.id ORDER BY response.response_version))[1] AS first_response_id,
                    min(response.response_version)::int AS first_response_version,
                    (array_agg(response.amount_minor ORDER BY response.response_version))[1]
                        AS first_response_amount_minor,
                    (array_agg(response.currency_code ORDER BY response.response_version))[1]
                        AS first_response_currency,
                    min(response.submitted_at_utc) AS first_submitted_at_utc
                FROM commercial.marketplace_supplier_responses response
                WHERE response.rfq_id = rfq.id) response_stats ON true
            LEFT JOIN LATERAL (
                SELECT response.id AS response_id, response.response_version,
                    response.amount_minor, response.currency_code,
                    response.submitted_at_utc, acceptance.accepted_at_utc
                FROM commercial.marketplace_supplier_responses response
                JOIN commercial.marketplace_response_acceptances acceptance
                  ON acceptance.response_id = response.id
                WHERE response.rfq_id = rfq.id
                ORDER BY acceptance.accepted_at_utc DESC, response.response_version DESC
                LIMIT 1) accepted ON true
            LEFT JOIN commercial.bookings booking
              ON booking.buyer_tenant_id = rfq.buyer_tenant_id
             AND booking.supplier_tenant_id = rfq.supplier_tenant_id
             AND booking.accepted_marketplace_response_id = accepted.response_id
            WHERE rfq.buyer_tenant_id = {tenantId.Value}
              AND rfq.sent_at_utc IS NOT NULL
              AND ({query.From}::date IS NULL OR rfq.sent_at_utc >= {query.From}::date)
              AND ({query.To}::date IS NULL OR rfq.sent_at_utc < ({query.To}::date + 1))
              AND ({query.SupplierTenantId}::uuid IS NULL OR rfq.supplier_tenant_id = {query.SupplierTenantId})
              AND ({query.Channel}::text IS NULL OR listing.channel_code = {query.Channel})
            ORDER BY rfq.sent_at_utc, rfq.id
            """).ToListAsync(cancellationToken);

    private Task<List<CommercialMemoryBookingRow>> ReadBookingsAsync(
        TenantId tenantId,
        CommercialMemoryQuery query,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<CommercialMemoryBookingRow>($"""
            SELECT booking.id AS "Id", booking.version AS "Version",
                booking.supplier_tenant_id AS "SupplierTenantId",
                booking.channel_code AS "Channel", booking.requested_at_utc AS "RequestedAtUtc",
                booking.confirmed_at_utc AS "ConfirmedAtUtc"
            FROM commercial.bookings booking
            WHERE booking.buyer_tenant_id = {tenantId.Value}
              AND booking.requested_at_utc IS NOT NULL
              AND ({query.From}::date IS NULL OR booking.requested_at_utc >= {query.From}::date)
              AND ({query.To}::date IS NULL OR booking.requested_at_utc < ({query.To}::date + 1))
              AND ({query.SupplierTenantId}::uuid IS NULL OR booking.supplier_tenant_id = {query.SupplierTenantId})
              AND ({query.Channel}::text IS NULL OR booking.channel_code = {query.Channel})
            ORDER BY booking.requested_at_utc, booking.id
            """).ToListAsync(cancellationToken);

    private Task<List<CommercialMemoryProofRow>> ReadProofsAsync(
        TenantId tenantId,
        CommercialMemoryQuery query,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<CommercialMemoryProofRow>($"""
            SELECT proof.id AS "Id", proof.version AS "Version",
                proof.supplier_tenant_id AS "SupplierTenantId", booking.channel_code AS "Channel",
                proof.status_code AS "Status", proof.submitted_at_utc AS "SubmittedAtUtc",
                proof.reviewed_at_utc AS "ReviewedAtUtc"
            FROM commercial.delivery_proofs proof
            JOIN commercial.bookings booking
              ON booking.buyer_tenant_id = proof.buyer_tenant_id AND booking.id = proof.booking_id
            WHERE proof.buyer_tenant_id = {tenantId.Value}
              AND ({query.From}::date IS NULL OR proof.submitted_at_utc >= {query.From}::date)
              AND ({query.To}::date IS NULL OR proof.submitted_at_utc < ({query.To}::date + 1))
              AND ({query.SupplierTenantId}::uuid IS NULL OR proof.supplier_tenant_id = {query.SupplierTenantId})
              AND ({query.Channel}::text IS NULL OR booking.channel_code = {query.Channel})
            ORDER BY proof.submitted_at_utc, proof.id
            """).ToListAsync(cancellationToken);

    private Task<List<CommercialMemorySelectionRow>> ReadSelectionsAsync(
        TenantId tenantId,
        CommercialMemoryQuery query,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<CommercialMemorySelectionRow>($"""
            SELECT shortlist.id AS "ShortlistId", shortlist.version AS "ShortlistVersion",
                selection.is_selected AS "IsSelected",
                selection.selected_at_utc AS "SelectedAtUtc"
            FROM commercial.shortlist_selections selection
            JOIN commercial.inventory_shortlist_candidates candidate
              ON candidate.tenant_id = selection.tenant_id
             AND candidate.id = selection.shortlist_candidate_id
            JOIN commercial.inventory_shortlist_versions shortlist
              ON shortlist.tenant_id = candidate.tenant_id
             AND shortlist.id = candidate.shortlist_version_id
            WHERE selection.tenant_id = {tenantId.Value}
              AND ({query.From}::date IS NULL OR selection.selected_at_utc >= {query.From}::date)
              AND ({query.To}::date IS NULL OR selection.selected_at_utc < ({query.To}::date + 1))
              AND ({query.SupplierTenantId}::uuid IS NULL
                   OR candidate.inventory_tenant_id = {query.SupplierTenantId})
              AND ({query.Channel}::text IS NULL OR candidate.channel_code = {query.Channel})
            ORDER BY selection.selected_at_utc, shortlist.id, selection.id
            """).ToListAsync(cancellationToken);

    private Task<List<CommercialMemoryProposalApprovalRow>> ReadProposalApprovalsAsync(
        TenantId tenantId,
        CommercialMemoryQuery query,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<CommercialMemoryProposalApprovalRow>($"""
            SELECT proposal.id AS "ProposalId", proposal.version AS "ProposalVersion",
                proposal.approval_requested_at_utc AS "RequestedAtUtc",
                proposal.approved_at_utc AS "ApprovedAtUtc",
                proposal.approval_rejected_at_utc AS "RejectedAtUtc"
            FROM commercial.proposal_versions proposal
            WHERE proposal.tenant_id = {tenantId.Value}
              AND proposal.approval_requested_at_utc IS NOT NULL
              AND ({query.From}::date IS NULL OR proposal.approval_requested_at_utc >= {query.From}::date)
              AND ({query.To}::date IS NULL OR proposal.approval_requested_at_utc < ({query.To}::date + 1))
              AND EXISTS (
                  SELECT 1 FROM commercial.proposal_options option
                  JOIN commercial.media_plan_lines line
                    ON line.tenant_id = option.tenant_id
                   AND line.plan_version_id = option.plan_version_id
                  WHERE option.tenant_id = proposal.tenant_id
                    AND option.proposal_version_id = proposal.id
                    AND ({query.SupplierTenantId}::uuid IS NULL
                         OR line.inventory_tenant_id = {query.SupplierTenantId})
                    AND ({query.Channel}::text IS NULL OR line.channel_code = {query.Channel}))
            ORDER BY proposal.approval_requested_at_utc, proposal.id
            """).ToListAsync(cancellationToken);

    private Task<List<CommercialMemoryProposalDecisionRow>> ReadProposalDecisionsAsync(
        TenantId tenantId,
        CommercialMemoryQuery query,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<CommercialMemoryProposalDecisionRow>($"""
            SELECT proposal.id AS "ProposalId", proposal.version AS "ProposalVersion",
                proposal.shared_at_utc AS "SharedAtUtc",
                decision.decision_code AS "Decision",
                decision.decided_at_utc AS "DecidedAtUtc"
            FROM commercial.proposal_decisions decision
            JOIN commercial.proposal_versions proposal
              ON proposal.tenant_id = decision.tenant_id
             AND proposal.id = decision.proposal_version_id
            WHERE decision.tenant_id = {tenantId.Value}
              AND ({query.From}::date IS NULL OR decision.decided_at_utc >= {query.From}::date)
              AND ({query.To}::date IS NULL OR decision.decided_at_utc < ({query.To}::date + 1))
              AND EXISTS (
                  SELECT 1 FROM commercial.proposal_options option
                  JOIN commercial.media_plan_lines line
                    ON line.tenant_id = option.tenant_id
                   AND line.plan_version_id = option.plan_version_id
                  WHERE option.tenant_id = proposal.tenant_id
                    AND option.proposal_version_id = proposal.id
                    AND ({query.SupplierTenantId}::uuid IS NULL
                         OR line.inventory_tenant_id = {query.SupplierTenantId})
                    AND ({query.Channel}::text IS NULL OR line.channel_code = {query.Channel}))
            ORDER BY decision.decided_at_utc, decision.id
            """).ToListAsync(cancellationToken);

    private Task<List<CommercialMemoryPerformanceRow>> ReadPerformanceAsync(
        TenantId tenantId,
        CommercialMemoryQuery query,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<CommercialMemoryPerformanceRow>($"""
            SELECT metric.id AS "MetricId", metric.metric_type_code AS "MetricType",
                metric.value AS "Value", metric.unit_code AS "Unit",
                evidence.id AS "EvidenceId", evidence.version AS "EvidenceVersion",
                COALESCE(evidence.reviewed_at_utc, evidence.captured_at_utc) AS "ObservedAtUtc",
                evidence.quality_status_code AS "QualityStatus"
            FROM commercial.performance_metrics metric
            JOIN commercial.performance_evidence_sets evidence
              ON evidence.tenant_id = metric.tenant_id
             AND evidence.id = metric.evidence_set_id
            WHERE metric.tenant_id = {tenantId.Value}
              AND evidence.status_code = {MasterDataCodes.LifecycleStatuses.Approved}
              AND evidence.quality_status_code <> {MasterDataCodes.MeasurementQualityStatuses.Unusable}
              AND {query.SupplierTenantId}::uuid IS NULL
              AND {query.Channel}::text IS NULL
              AND ({query.From}::date IS NULL OR metric.period_end >= {query.From}::date)
              AND ({query.To}::date IS NULL OR metric.period_end <= {query.To}::date)
            ORDER BY metric.period_end, metric.id
            """).ToListAsync(cancellationToken);
}

internal sealed record CommercialMemoryData(
    IReadOnlyList<CommercialMemoryExchangeRow> Exchanges,
    IReadOnlyList<CommercialMemoryBookingRow> Bookings,
    IReadOnlyList<CommercialMemoryProofRow> Proofs,
    IReadOnlyList<CommercialMemorySelectionRow> Selections,
    IReadOnlyList<CommercialMemoryProposalApprovalRow> ProposalApprovals,
    IReadOnlyList<CommercialMemoryProposalDecisionRow> ProposalDecisions,
    IReadOnlyList<CommercialMemoryPerformanceRow> Performance);

internal sealed record CommercialMemoryExchangeRow(
    Guid RfqId,
    long RfqVersion,
    DateTimeOffset SentAtUtc,
    Guid SupplierTenantId,
    string Channel,
    long ListedAmountMinor,
    string ListedCurrency,
    int ResponseCount,
    Guid? FirstResponseId,
    int? FirstResponseVersion,
    long? FirstResponseAmountMinor,
    string? FirstResponseCurrency,
    DateTimeOffset? FirstSubmittedAtUtc,
    Guid? AcceptedResponseId,
    int? AcceptedResponseVersion,
    long? AcceptedAmountMinor,
    string? AcceptedCurrency,
    DateTimeOffset? AcceptedResponseSubmittedAtUtc,
    DateTimeOffset? AcceptedAtUtc,
    Guid? BookingId,
    long? BookingVersion,
    DateTimeOffset? BookingRequestedAtUtc,
    DateTimeOffset? BookingConfirmedAtUtc);

internal sealed record CommercialMemoryBookingRow(
    Guid Id,
    long Version,
    Guid SupplierTenantId,
    string Channel,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset? ConfirmedAtUtc);

internal sealed record CommercialMemoryProofRow(
    Guid Id,
    long Version,
    Guid SupplierTenantId,
    string Channel,
    string Status,
    DateTimeOffset SubmittedAtUtc,
    DateTimeOffset? ReviewedAtUtc);

internal sealed record CommercialMemorySelectionRow(
    Guid ShortlistId,
    long ShortlistVersion,
    bool IsSelected,
    DateTimeOffset SelectedAtUtc);

internal sealed record CommercialMemoryProposalApprovalRow(
    Guid ProposalId,
    long ProposalVersion,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset? ApprovedAtUtc,
    DateTimeOffset? RejectedAtUtc);

internal sealed record CommercialMemoryProposalDecisionRow(
    Guid ProposalId,
    long ProposalVersion,
    DateTimeOffset? SharedAtUtc,
    string Decision,
    DateTimeOffset DecidedAtUtc);

internal sealed record CommercialMemoryPerformanceRow(
    Guid MetricId,
    string MetricType,
    decimal Value,
    string Unit,
    Guid EvidenceId,
    long EvidenceVersion,
    DateTimeOffset ObservedAtUtc,
    string QualityStatus);

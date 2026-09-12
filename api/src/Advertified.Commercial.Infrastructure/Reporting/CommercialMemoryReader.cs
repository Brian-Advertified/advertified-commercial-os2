using Advertified.Commercial.Application.Reporting;
using Advertified.Commercial.Application.Security;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Reporting;

public sealed class CommercialMemoryReader(
    CommercialMemoryStore store,
    ITenantAuthorizer authorizer,
    TimeProvider timeProvider) : ICommercialMemoryReader
{
    private const int MaximumSourceReferences = 100;

    public async Task<CommercialMemoryView> GetAsync(
        ActorId actorId,
        TenantId tenantId,
        CommercialMemoryQuery query,
        CancellationToken cancellationToken)
    {
        Validate(query);
        var decision = await authorizer.AuthorizeAsync(
            actorId, tenantId, MasterDataReferences.Permissions.MeasurementReportView,
            cancellationToken);
        if (!decision.IsAllowed)
            throw new UnauthorizedAccessException("Commercial Memory access denied.");
        var normalized = query with
        {
            Channel = string.IsNullOrWhiteSpace(query.Channel)
                ? null
                : query.Channel.Trim().ToUpperInvariant(),
        };
        var data = await store.ReadAsync(actorId, tenantId, normalized, cancellationToken);
        return new(
            timeProvider.GetUtcNow(),
            new CommercialMemoryCohortView(
                tenantId.Value, normalized.From, normalized.To,
                normalized.SupplierTenantId, normalized.Channel),
            BuildMetrics(data, normalized));
    }

    private static void Validate(CommercialMemoryQuery query)
    {
        if (query.From.HasValue && query.To.HasValue && query.To < query.From)
            throw new ArgumentException("The Commercial Memory end date must be on or after the start date.");
        if (query.Channel is { Length: > 100 })
            throw new ArgumentException("The Commercial Memory channel filter is invalid.");
    }

    private static CommercialMemoryMetricView[] BuildMetrics(
        CommercialMemoryData data,
        CommercialMemoryQuery query) =>
    [
        DurationMetric(
            "supplier_response_time_hours",
            data.Exchanges.Where(item => item.FirstSubmittedAtUtc.HasValue)
                .Select(item => new DurationSample(
                    Hours(item.SentAtUtc, item.FirstSubmittedAtUtc!.Value),
                    Source(MasterDataCodes.CommercialResourceTypes.MarketplaceRfq,
                        item.RfqId, item.RfqVersion, item.SentAtUtc))).ToArray()),
        AverageMetric(
            "supplier_quote_versions_per_rfq",
            data.Exchanges.Where(item => item.ResponseCount > 0)
                .Select(item => new DecimalSample(
                    item.ResponseCount,
                    Source(MasterDataCodes.CommercialResourceTypes.MarketplaceRfq,
                        item.RfqId, item.RfqVersion, item.SentAtUtc))).ToArray(),
            MasterDataCodes.MeasurementUnits.Count),
        ListedToFirstQuoteVariance(data.Exchanges),
        FirstToAcceptedQuoteVariance(data.Exchanges),
        RateMetric(
            "supplier_quote_acceptance_rate_percent",
            data.Exchanges.Where(item => item.ResponseCount > 0)
                .Select(item => new BoolSample(
                    item.AcceptedResponseId.HasValue,
                    Source(MasterDataCodes.CommercialResourceTypes.MarketplaceRfq,
                        item.RfqId, item.RfqVersion, item.SentAtUtc))).ToArray()),
        DurationMetric(
            "accepted_quote_to_accept_hours",
            data.Exchanges.Where(item => item.AcceptedResponseId.HasValue &&
                                         item.AcceptedResponseSubmittedAtUtc.HasValue &&
                                         item.AcceptedAtUtc.HasValue)
                .Select(item => new DurationSample(
                    Hours(item.AcceptedResponseSubmittedAtUtc!.Value, item.AcceptedAtUtc!.Value),
                    Source(MasterDataCodes.CommercialResourceTypes.MarketplaceSupplierResponse,
                        item.AcceptedResponseId!.Value,
                        item.AcceptedResponseVersion ?? 1,
                        item.AcceptedResponseSubmittedAtUtc.Value))).ToArray()),
        RateMetric(
            "accepted_quote_booking_conversion_percent",
            data.Exchanges.Where(item => item.AcceptedResponseId.HasValue)
                .Select(item => new BoolSample(
                    item.BookingRequestedAtUtc.HasValue,
                    Source(MasterDataCodes.CommercialResourceTypes.MarketplaceSupplierResponse,
                        item.AcceptedResponseId!.Value,
                        item.AcceptedResponseVersion ?? 1,
                        item.AcceptedAtUtc ?? item.AcceptedResponseSubmittedAtUtc!.Value))).ToArray()),
        DurationMetric(
            "accepted_quote_to_booking_request_hours",
            data.Exchanges.Where(item => item.AcceptedAtUtc.HasValue && item.BookingRequestedAtUtc.HasValue)
                .Select(item => new DurationSample(
                    Hours(item.AcceptedAtUtc!.Value, item.BookingRequestedAtUtc!.Value),
                    Source(MasterDataCodes.CommercialResourceTypes.Booking,
                        item.BookingId!.Value, item.BookingVersion ?? 1,
                        item.BookingRequestedAtUtc.Value))).ToArray()),
        RateMetric(
            "accepted_quote_booking_confirmation_rate_percent",
            data.Exchanges.Where(item => item.AcceptedResponseId.HasValue)
                .Select(item => new BoolSample(
                    item.BookingConfirmedAtUtc.HasValue,
                    Source(MasterDataCodes.CommercialResourceTypes.MarketplaceSupplierResponse,
                        item.AcceptedResponseId!.Value,
                        item.AcceptedResponseVersion ?? 1,
                        item.AcceptedAtUtc ?? item.AcceptedResponseSubmittedAtUtc!.Value))).ToArray()),
        RateMetric(
            "booking_confirmation_rate_percent",
            data.Bookings.Select(item => new BoolSample(
                item.ConfirmedAtUtc.HasValue,
                Source(MasterDataCodes.CommercialResourceTypes.Booking,
                    item.Id, item.Version, item.RequestedAtUtc))).ToArray()),
        DurationMetric(
            "booking_confirmation_time_hours",
            data.Bookings.Where(item => item.ConfirmedAtUtc.HasValue)
                .Select(item => new DurationSample(
                    Hours(item.RequestedAtUtc, item.ConfirmedAtUtc!.Value),
                    Source(MasterDataCodes.CommercialResourceTypes.Booking,
                        item.Id, item.Version, item.RequestedAtUtc))).ToArray()),
        RateMetric(
            "delivery_proof_approval_rate_percent",
            data.Proofs.Where(item => item.ReviewedAtUtc.HasValue)
                .Select(item => new BoolSample(
                    item.Status == MasterDataCodes.LifecycleStatuses.Approved,
                    Source(MasterDataCodes.CommercialResourceTypes.DeliveryProof,
                        item.Id, item.Version, item.SubmittedAtUtc))).ToArray()),
        DurationMetric(
            "delivery_proof_review_time_hours",
            data.Proofs.Where(item => item.ReviewedAtUtc.HasValue)
                .Select(item => new DurationSample(
                    Hours(item.SubmittedAtUtc, item.ReviewedAtUtc!.Value),
                    Source(MasterDataCodes.CommercialResourceTypes.DeliveryProof,
                        item.Id, item.Version, item.SubmittedAtUtc))).ToArray()),
        RateMetric(
            "shortlist_candidate_selection_rate_percent",
            data.Selections.Select(item => new BoolSample(
                item.IsSelected,
                Source(MasterDataCodes.CommercialResourceTypes.InventoryShortlistVersion,
                    item.ShortlistId, item.ShortlistVersion, item.SelectedAtUtc))).ToArray()),
        RateMetric(
            "proposal_approval_resolution_rate_percent",
            data.ProposalApprovals.Select(item => new BoolSample(
                item.ApprovedAtUtc.HasValue || item.RejectedAtUtc.HasValue,
                Source(MasterDataCodes.CommercialResourceTypes.ProposalVersion,
                    item.ProposalId, item.ProposalVersion, item.RequestedAtUtc))).ToArray()),
        RateMetric(
            "proposal_approval_success_rate_percent",
            data.ProposalApprovals.Where(item =>
                    item.ApprovedAtUtc.HasValue || item.RejectedAtUtc.HasValue)
                .Select(item => new BoolSample(
                    item.ApprovedAtUtc.HasValue,
                    Source(MasterDataCodes.CommercialResourceTypes.ProposalVersion,
                        item.ProposalId, item.ProposalVersion,
                        ApprovalResolutionAt(item)))).ToArray()),
        DurationMetric(
            "proposal_approval_resolution_time_hours",
            data.ProposalApprovals.Where(item =>
                    item.ApprovedAtUtc.HasValue || item.RejectedAtUtc.HasValue)
                .Select(item => new DurationSample(
                    Hours(item.RequestedAtUtc, ApprovalResolutionAt(item)),
                    Source(MasterDataCodes.CommercialResourceTypes.ProposalVersion,
                        item.ProposalId, item.ProposalVersion,
                        ApprovalResolutionAt(item)))).ToArray()),
        RateMetric(
            "proposal_decision_selection_rate_percent",
            data.ProposalDecisions.Select(item => new BoolSample(
                item.Decision == MasterDataCodes.LifecycleStatuses.Selected,
                Source(MasterDataCodes.CommercialResourceTypes.ProposalVersion,
                    item.ProposalId, item.ProposalVersion, item.DecidedAtUtc))).ToArray()),
        DurationMetric(
            "proposal_share_to_decision_hours",
            data.ProposalDecisions.Where(item => item.SharedAtUtc.HasValue)
                .Select(item => new DurationSample(
                    Hours(item.SharedAtUtc!.Value, item.DecidedAtUtc),
                    Source(MasterDataCodes.CommercialResourceTypes.ProposalVersion,
                        item.ProposalId, item.ProposalVersion, item.DecidedAtUtc))).ToArray()),
        UnsupportedCancellationMetric(),
        ..PerformanceMetrics(data.Performance, query),
    ];

    private static DateTimeOffset ApprovalResolutionAt(
        CommercialMemoryProposalApprovalRow row) =>
        row.ApprovedAtUtc ?? row.RejectedAtUtc ??
        throw new InvalidOperationException("Proposal approval has no resolution timestamp.");

    private static CommercialMemoryMetricView UnsupportedCancellationMetric() => new(
        "booking_cancellation_rate_percent",
        null,
        MasterDataCodes.MeasurementUnits.Percent,
        0,
        false,
        "The canonical Booking lifecycle does not define a cancellation transition or cancellation event. Cancellation behavior is therefore unknown and is not inferred from missing confirmations.",
        []);

    private static CommercialMemoryMetricView[] PerformanceMetrics(
        IReadOnlyList<CommercialMemoryPerformanceRow> rows,
        CommercialMemoryQuery query)
    {
        if (query.SupplierTenantId.HasValue || !string.IsNullOrWhiteSpace(query.Channel))
        {
            return
            [
                new CommercialMemoryMetricView(
                    "campaign_performance_observations_count",
                    null,
                    MasterDataCodes.MeasurementUnits.Count,
                    0,
                    false,
                    "Campaign-level performance evidence is not safely attributable to an individual supplier or channel cohort, so no outcome benchmark is produced for this filtered view.",
                    []),
            ];
        }
        return rows.GroupBy(item => (item.MetricType, item.Unit))
            .OrderBy(group => group.Key.MetricType, StringComparer.Ordinal)
            .Select(group =>
            {
                var samples = group.Select(item => new DecimalSample(
                    item.Value,
                    Source(MasterDataCodes.CommercialResourceTypes.PerformanceEvidence,
                        item.EvidenceId, item.EvidenceVersion, item.ObservedAtUtc))).ToArray();
                var limited = group.Count(item =>
                    item.QualityStatus == MasterDataCodes.MeasurementQualityStatuses.Limited);
                var limitation = limited == 0
                    ? null
                    : $"{limited} observation{(limited == 1 ? " uses" : "s use")} LIMITED measurement-quality evidence.";
                return Metric(
                    "measured_" + group.Key.MetricType.ToLowerInvariant() + "_average",
                    samples.Average(item => item.Value),
                    group.Key.Unit,
                    samples.Length,
                    samples.Select(item => item.Source),
                    limitation);
            }).ToArray();
    }

    private static CommercialMemoryMetricView ListedToFirstQuoteVariance(
        IReadOnlyList<CommercialMemoryExchangeRow> exchanges)
    {
        var candidates = exchanges.Where(item => item.FirstResponseAmountMinor.HasValue).ToArray();
        var samples = candidates.Where(item =>
                item.ListedAmountMinor > 0 &&
                string.Equals(item.ListedCurrency, item.FirstResponseCurrency, StringComparison.Ordinal))
            .Select(item => new DecimalSample(
                PercentVariance(item.ListedAmountMinor, item.FirstResponseAmountMinor!.Value),
                Source(MasterDataCodes.CommercialResourceTypes.MarketplaceSupplierResponse,
                    item.FirstResponseId!.Value, item.FirstResponseVersion ?? 1,
                    item.FirstSubmittedAtUtc!.Value))).ToArray();
        return VarianceMetric(
            "listed_to_first_quote_variance_percent", candidates.Length, samples);
    }

    private static CommercialMemoryMetricView FirstToAcceptedQuoteVariance(
        IReadOnlyList<CommercialMemoryExchangeRow> exchanges)
    {
        var candidates = exchanges.Where(item =>
            item.FirstResponseAmountMinor.HasValue && item.AcceptedAmountMinor.HasValue).ToArray();
        var samples = candidates.Where(item =>
                item.FirstResponseAmountMinor > 0 &&
                string.Equals(item.FirstResponseCurrency, item.AcceptedCurrency, StringComparison.Ordinal))
            .Select(item => new DecimalSample(
                PercentVariance(item.FirstResponseAmountMinor!.Value, item.AcceptedAmountMinor!.Value),
                Source(MasterDataCodes.CommercialResourceTypes.MarketplaceSupplierResponse,
                    item.AcceptedResponseId!.Value, item.AcceptedResponseVersion ?? 1,
                    item.AcceptedResponseSubmittedAtUtc!.Value))).ToArray();
        return VarianceMetric(
            "first_to_accepted_quote_variance_percent", candidates.Length, samples);
    }

    private static CommercialMemoryMetricView VarianceMetric(
        string code,
        int candidateCount,
        IReadOnlyList<DecimalSample> samples)
    {
        var excluded = candidateCount - samples.Count;
        var limitation = excluded == 0
            ? null
            : $"{excluded} observation{(excluded == 1 ? " was" : "s were")} excluded because currency differed or the comparison basis was zero.";
        return Metric(
            code,
            samples.Count == 0 ? null : samples.Average(item => item.Value),
            MasterDataCodes.MeasurementUnits.Percent,
            samples.Count,
            samples.Select(item => item.Source),
            limitation);
    }

    private static decimal PercentVariance(long basis, long value) =>
        decimal.Round(100m * (value - basis) / basis, 4);

    private static CommercialMemoryMetricView DurationMetric(
        string code,
        IReadOnlyList<DurationSample> samples) =>
        Metric(code, samples.Count == 0 ? null : samples.Average(item => item.Value),
            MasterDataCodes.MeasurementUnits.Hours, samples.Count, samples.Select(item => item.Source));

    private static CommercialMemoryMetricView AverageMetric(
        string code,
        IReadOnlyList<DecimalSample> samples,
        string unit) =>
        Metric(code, samples.Count == 0 ? null : samples.Average(item => item.Value),
            unit, samples.Count, samples.Select(item => item.Source));

    private static CommercialMemoryMetricView RateMetric(
        string code,
        IReadOnlyList<BoolSample> samples) =>
        Metric(code,
            samples.Count == 0 ? null :
                decimal.Round(100m * samples.Count(item => item.Value) / samples.Count, 4),
            MasterDataCodes.MeasurementUnits.Percent, samples.Count, samples.Select(item => item.Source));

    private static CommercialMemoryMetricView Metric(
        string code,
        decimal? value,
        string unit,
        int sampleSize,
        IEnumerable<CommercialMemorySourceView> sources,
        string? additionalLimitation = null)
    {
        var sourceRows = sources.Take(MaximumSourceReferences).ToArray();
        var limitations = new List<string>();
        if (!string.IsNullOrWhiteSpace(additionalLimitation))
            limitations.Add(additionalLimitation);
        if (sampleSize == 0)
            limitations.Add("No canonical transaction records match this cohort and period.");
        else if (sampleSize == 1)
            limitations.Add("One observation is retained as history but is not presented as a robust benchmark.");
        if (sampleSize > sourceRows.Length)
            limitations.Add($"Source references are bounded to {MaximumSourceReferences} of {sampleSize} observations.");
        return new(code, value, unit, sampleSize, sampleSize > 1,
            limitations.Count == 0 ? null : string.Join(" ", limitations), sourceRows);
    }

    private static decimal Hours(DateTimeOffset start, DateTimeOffset end) =>
        end < start
            ? throw new InvalidOperationException("Canonical commercial timestamps are inconsistent.")
            : decimal.Round((decimal)(end - start).TotalHours, 4);

    private static CommercialMemorySourceView Source(
        string resourceType,
        Guid resourceId,
        long version,
        DateTimeOffset occurredAtUtc) =>
        new(resourceType, resourceId, version, occurredAtUtc);

    private sealed record DurationSample(decimal Value, CommercialMemorySourceView Source);
    private sealed record DecimalSample(decimal Value, CommercialMemorySourceView Source);
    private sealed record BoolSample(bool Value, CommercialMemorySourceView Source);
}

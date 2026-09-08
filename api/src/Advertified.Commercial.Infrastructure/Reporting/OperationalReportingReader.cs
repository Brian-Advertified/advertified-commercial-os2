using Advertified.Commercial.Application.Reporting;
using Advertified.Commercial.Application.Security;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Reporting;

public sealed class OperationalReportingReader(
    OperationalReportingStore store,
    ITenantAuthorizer authorizer,
    TimeProvider timeProvider) : IOperationalReportingReader
{
    public async Task<OperationalReportingView> GetAsync(
        ActorId actorId,
        TenantId tenantId,
        ReportingQuery query,
        CancellationToken cancellationToken)
    {
        if (query.From.HasValue && query.To.HasValue && query.To < query.From)
            throw new ArgumentException("The reporting end date must be on or after the start date.");
        var decision = await authorizer.AuthorizeAsync(
            actorId, tenantId, MasterDataReferences.Permissions.MeasurementReportView,
            cancellationToken);
        if (!decision.IsAllowed)
            throw new UnauthorizedAccessException("Reporting access denied.");
        var normalized = query with
        {
            Channel = Normalize(query.Channel),
            Status = Normalize(query.Status),
        };
        var data = await store.ReadAsync(actorId, tenantId, normalized, cancellationToken);
        return new OperationalReportingView(
            timeProvider.GetUtcNow(),
            new ReportingMetricView(
                data.Metrics.Opportunities, data.Metrics.Briefs,
                data.Metrics.ApprovedAudiences, data.Metrics.PendingApprovals,
                data.Metrics.ApprovedPlans, data.Metrics.InventoryRecommendations,
                data.Metrics.SelectedInventory, data.Metrics.PublishedListings,
                data.Metrics.Proposals, data.Metrics.Bookings, data.Metrics.Campaigns,
                data.Metrics.DeliveryEvidenceItems, data.Metrics.MeasurementReports),
            data.Channels.Select(item => new ReportingChannelSpendView(
                item.Channel, item.PlanCount, item.SupplierCostMinor,
                item.ClientPriceMinor, item.FeesMinor, item.VatMinor, item.Currency)).ToArray(),
            new ReportingCommercialTotalsView(
                data.Commercial.BookingCount, data.Commercial.SupplierCostMinor,
                data.Commercial.MarkupMinor, data.Commercial.CommissionMinor,
                data.Commercial.ManagementFeeMinor, data.Commercial.FeesMinor,
                data.Commercial.VatMinor, data.Commercial.ClientTotalMinor,
                data.Commercial.Currency),
            data.Statuses.Select(item => new ReportingStatusView(
                item.Area, item.Status, item.Count, item.OldestAgeDays)).ToArray(),
            data.Exceptions.Select(item => new ReportingExceptionView(
                item.Code, item.Label, item.Count)).ToArray(),
            new ReportingDimensionsView(
                Dimensions(data, "client-dimension"), Dimensions(data, "campaign-dimension"),
                Dimensions(data, "supplier-dimension"), Dimensions(data, "user-dimension")));
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static ReportingDimensionView[] Dimensions(
        OperationalReportingData data,
        string kind) => data.Dimensions.Where(item => item.Kind == kind)
        .Select(item => new ReportingDimensionView(item.Id, item.Label)).ToArray();
}

using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Application.Reporting;

public sealed record ReportingQuery(
    DateOnly? From,
    DateOnly? To,
    Guid? ClientAccountId,
    Guid? CampaignId,
    string? Channel,
    Guid? SupplierId,
    string? Status,
    Guid? OwnerUserId,
    Guid? ReviewerUserId);

public sealed record ReportingMetricView(
    int Opportunities,
    int Briefs,
    int ApprovedAudiences,
    int PendingApprovals,
    int ApprovedPlans,
    int InventoryRecommendations,
    int SelectedInventory,
    int PublishedListings,
    int Proposals,
    int Bookings,
    int Campaigns,
    int DeliveryEvidenceItems,
    int MeasurementReports);

public sealed record ReportingChannelSpendView(
    string Channel,
    int PlanCount,
    long SupplierCostMinor,
    long ClientPriceMinor,
    long FeesMinor,
    long VatMinor,
    string Currency);

public sealed record ReportingCommercialTotalsView(
    int BookingCount,
    long SupplierCostMinor,
    long MarkupMinor,
    long CommissionMinor,
    long ManagementFeeMinor,
    long FeesMinor,
    long VatMinor,
    long ClientTotalMinor,
    string Currency);

public sealed record ReportingStatusView(
    string Area,
    string Status,
    int Count,
    int OldestAgeDays);

public sealed record ReportingExceptionView(
    string Code,
    string Label,
    int Count);

public sealed record ReportingDimensionView(Guid Id, string Label);

public sealed record ReportingDimensionsView(
    IReadOnlyList<ReportingDimensionView> Clients,
    IReadOnlyList<ReportingDimensionView> Campaigns,
    IReadOnlyList<ReportingDimensionView> Suppliers,
    IReadOnlyList<ReportingDimensionView> Users);

public sealed record OperationalReportingView(
    DateTimeOffset GeneratedAtUtc,
    ReportingMetricView Metrics,
    IReadOnlyList<ReportingChannelSpendView> ChannelSpend,
    ReportingCommercialTotalsView CommercialTotals,
    IReadOnlyList<ReportingStatusView> Statuses,
    IReadOnlyList<ReportingExceptionView> Exceptions,
    ReportingDimensionsView Dimensions);

public interface IOperationalReportingReader
{
    Task<OperationalReportingView> GetAsync(
        ActorId actorId,
        TenantId tenantId,
        ReportingQuery query,
        CancellationToken cancellationToken);
}

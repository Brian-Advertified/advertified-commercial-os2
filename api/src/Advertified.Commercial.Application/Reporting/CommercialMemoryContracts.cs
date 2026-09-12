using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Application.Reporting;

public sealed record CommercialMemoryQuery(
    DateOnly? From,
    DateOnly? To,
    Guid? SupplierTenantId,
    string? Channel);

public sealed record CommercialMemoryCohortView(
    Guid TenantId,
    DateOnly? From,
    DateOnly? To,
    Guid? SupplierTenantId,
    string? Channel);

public sealed record CommercialMemorySourceView(
    string ResourceType,
    Guid ResourceId,
    long Version,
    DateTimeOffset OccurredAtUtc);

public sealed record CommercialMemoryMetricView(
    string Code,
    decimal? Value,
    string Unit,
    int SampleSize,
    bool IsRobustBenchmark,
    string? Limitation,
    IReadOnlyList<CommercialMemorySourceView> Sources);

public sealed record CommercialMemoryView(
    DateTimeOffset GeneratedAtUtc,
    CommercialMemoryCohortView Cohort,
    IReadOnlyList<CommercialMemoryMetricView> Metrics);

public interface ICommercialMemoryReader
{
    Task<CommercialMemoryView> GetAsync(
        ActorId actorId,
        TenantId tenantId,
        CommercialMemoryQuery query,
        CancellationToken cancellationToken);
}

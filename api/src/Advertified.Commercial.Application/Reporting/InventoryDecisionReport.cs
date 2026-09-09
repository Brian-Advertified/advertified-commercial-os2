using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Application.Reporting;

public sealed record InventoryDecisionView(
    Guid EventId,
    Guid? PreviousEventId,
    Guid ProductId,
    Guid ProductVersionId,
    Guid? PreviousProductVersionId,
    string ProductName,
    bool IsSelected,
    bool? WasSelected,
    bool PresentInCurrentShortlist,
    bool? AgentInterpreted,
    DateTimeOffset DecidedAtUtc,
    Guid? ActorId,
    string? Reason,
    Guid? BriefVersionId,
    Guid? ShortlistVersionId);

public sealed record InventoryDecisionReportView(
    IReadOnlyList<InventoryDecisionView> Items,
    bool HasMore,
    bool SupplierSafe,
    string? NextCursor);

public interface IInventoryDecisionReader
{
    Task<InventoryDecisionReportView> ReadAsync(
        ActorId actorId, TenantId tenantId, Guid? briefVersionId,
        Guid? inventoryProductId, string? cursor,
        CancellationToken cancellationToken);
}

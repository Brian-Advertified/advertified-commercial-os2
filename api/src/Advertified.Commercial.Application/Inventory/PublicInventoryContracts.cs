namespace Advertified.Commercial.Application.Inventory;

public sealed record PublicMediaOwnerView(Guid Id, string Name, string? LogoUrl);

public sealed record PublicInventoryChannelView(
    string Channel,
    int Count,
    IReadOnlyList<PublicMediaOwnerView> Owners);

public sealed record PublicInventorySummaryView(
    int TotalCount,
    IReadOnlyList<PublicInventoryChannelView> Channels);

public interface IPublicInventorySummaryReader
{
    Task<PublicInventorySummaryView> GetAsync(CancellationToken cancellationToken);
}

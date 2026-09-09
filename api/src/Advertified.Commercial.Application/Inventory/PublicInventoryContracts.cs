namespace Advertified.Commercial.Application.Inventory;

public sealed record PublicMediaUnitView(string Id, string Name, string? LogoUrl);

public sealed record PublicInventoryChannelView(
    string Channel,
    int Count,
    string CountBasis,
    IReadOnlyList<PublicMediaUnitView> Units);

public sealed record PublicInventorySummaryView(
    int TotalCount,
    IReadOnlyList<PublicInventoryChannelView> Channels);

public interface IPublicInventorySummaryReader
{
    Task<PublicInventorySummaryView> GetAsync(CancellationToken cancellationToken);
}

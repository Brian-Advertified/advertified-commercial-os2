using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.MasterData;

public static class MasterDataReadiness
{
    private static readonly MasterDataRegistry Registry = MasterDataRegistryReader.Read();
    private static readonly HashSet<string> RequiredCollections = Registry.Collections
        .Select(item => item.Code).ToHashSet(StringComparer.Ordinal);
    private static readonly HashSet<(string Collection, string Code)> RequiredItems = Registry.Collections
        .SelectMany(collection => collection.Items.Select(item => (collection.Code, item.Code))).ToHashSet();

    public static async Task<bool> IsReadyAsync(GovernanceDbContext database, CancellationToken cancellationToken)
    {
        var currentCollections = await database.MasterDataSets.AsNoTracking()
            .Where(item => item.RegistryVersion == Registry.RegistryVersion)
            .Select(item => item.Code).ToListAsync(cancellationToken);
        if (!RequiredCollections.IsSubsetOf(currentCollections)) return false;
        var items = await database.MasterDataItems.AsNoTracking()
            .Select(item => new { item.CollectionCode, item.Code }).ToListAsync(cancellationToken);
        // Inactive governed records are valid; readiness verifies presence, not activation authority.
        return RequiredItems.IsSubsetOf(items.Select(item => (item.CollectionCode, item.Code)));
    }
}

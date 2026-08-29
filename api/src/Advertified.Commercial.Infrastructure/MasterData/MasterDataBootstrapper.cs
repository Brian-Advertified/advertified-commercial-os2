using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.MasterData;

public sealed record MasterDataBootstrapResult(int CollectionCount, int ItemCount);

public sealed class MasterDataBootstrapper(
    GovernanceDbContext dbContext,
    TimeProvider timeProvider)
{
    public async Task<MasterDataBootstrapResult> ApplyAsync(
        CancellationToken cancellationToken = default)
    {
        var registry = MasterDataRegistryReader.Read();
        var changedAtUtc = timeProvider.GetUtcNow();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var collections = await dbContext.MasterDataSets
            .ToDictionaryAsync(item => item.Code, cancellationToken);
        var items = await dbContext.MasterDataItems
            .ToDictionaryAsync(item => (item.CollectionCode, item.Code), cancellationToken);

        foreach (var registryCollection in registry.Collections)
        {
            UpsertCollection(registry, registryCollection, changedAtUtc, collections);

            foreach (var registryItem in registryCollection.Items)
            {
                UpsertItem(
                    registry,
                    registryCollection.Code,
                    registryItem,
                    changedAtUtc,
                    items);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new MasterDataBootstrapResult(
            registry.Collections.Count,
            registry.Collections.Sum(collection => collection.Items.Count));
    }

    private void UpsertCollection(
        MasterDataRegistry registry,
        MasterDataRegistryCollection registryCollection,
        DateTimeOffset changedAtUtc,
        Dictionary<string, MasterDataSet> collections)
    {
        if (collections.TryGetValue(registryCollection.Code, out var collection))
        {
            collection.Refresh(registry.RegistryVersion, registry.EffectiveFrom, changedAtUtc);
            return;
        }

        dbContext.MasterDataSets.Add(new MasterDataSet(
            registryCollection.Code,
            registry.RegistryVersion,
            registry.EffectiveFrom,
            changedAtUtc));
    }

    private void UpsertItem(
        MasterDataRegistry registry,
        string collectionCode,
        MasterDataRegistryItem registryItem,
        DateTimeOffset changedAtUtc,
        Dictionary<(string CollectionCode, string Code), MasterDataItem> items)
    {
        var key = (collectionCode, registryItem.Code);

        if (items.TryGetValue(key, out var item))
        {
            item.Refresh(
                registryItem.DisplayLabel,
                registryItem.IsActive,
                registryItem.SortOrder,
                registryItem.MetadataJson,
                registry.EffectiveFrom,
                changedAtUtc);
            return;
        }

        dbContext.MasterDataItems.Add(new MasterDataItem(
            collectionCode,
            registryItem.Code,
            registryItem.DisplayLabel,
            registryItem.IsActive,
            registryItem.SortOrder,
            registryItem.MetadataJson,
            registry.EffectiveFrom,
            changedAtUtc));
    }
}

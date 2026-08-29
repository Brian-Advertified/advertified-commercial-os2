namespace Advertified.Commercial.Infrastructure.MasterData;

public sealed class MasterDataSet
{
    private MasterDataSet()
    {
    }

    internal MasterDataSet(
        string code,
        string registryVersion,
        DateOnly effectiveFrom,
        DateTimeOffset updatedAtUtc)
    {
        Code = code;
        RegistryVersion = registryVersion;
        EffectiveFrom = effectiveFrom;
        UpdatedAtUtc = updatedAtUtc;
    }

    public string Code { get; private set; } = string.Empty;

    public string RegistryVersion { get; private set; } = string.Empty;

    public DateOnly EffectiveFrom { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    internal void Refresh(
        string registryVersion,
        DateOnly effectiveFrom,
        DateTimeOffset updatedAtUtc)
    {
        RegistryVersion = registryVersion;
        EffectiveFrom = effectiveFrom;
        UpdatedAtUtc = updatedAtUtc;
    }
}

public sealed class MasterDataItem
{
    private MasterDataItem()
    {
    }

    internal MasterDataItem(
        string collectionCode,
        string code,
        string displayLabel,
        bool isActive,
        int sortOrder,
        string metadataJson,
        DateOnly effectiveFrom,
        DateTimeOffset changedAtUtc)
    {
        CollectionCode = collectionCode;
        Code = code;
        Refresh(displayLabel, isActive, sortOrder, metadataJson, effectiveFrom, changedAtUtc);
        CreatedAtUtc = changedAtUtc;
    }

    public string CollectionCode { get; private set; } = string.Empty;

    public string Code { get; private set; } = string.Empty;

    public string DisplayLabel { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public int SortOrder { get; private set; }

    public string MetadataJson { get; private set; } = "{}";

    public DateOnly EffectiveFrom { get; private set; }

    public DateOnly? EffectiveTo { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    internal void Refresh(
        string displayLabel,
        bool isActive,
        int sortOrder,
        string metadataJson,
        DateOnly effectiveFrom,
        DateTimeOffset changedAtUtc)
    {
        DisplayLabel = displayLabel;
        IsActive = isActive;
        SortOrder = sortOrder;
        MetadataJson = metadataJson;
        EffectiveFrom = effectiveFrom;
        UpdatedAtUtc = changedAtUtc;
    }
}

public sealed class MasterDataItemHistory
{
    private MasterDataItemHistory()
    {
    }

    public long Id { get; private set; }

    public string CollectionCode { get; private set; } = string.Empty;

    public string ItemCode { get; private set; } = string.Empty;

    public string Operation { get; private set; } = string.Empty;

    public string SnapshotJson { get; private set; } = "{}";

    public DateTimeOffset ChangedAtUtc { get; private set; }
}

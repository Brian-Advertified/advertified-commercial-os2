using System.Text.Json;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;

namespace Advertified.Commercial.Infrastructure.Planning;

internal sealed record CampaignModeDefinition(
    string Code,
    string[] AllowedChannels,
    bool ImmutableAfterSelection);

public sealed class CampaignModePolicy
{
    private const string AllowedChannelsField = "allowedChannels";
    private const string ImmutableAfterSelectionField = "immutableAfterSelection";

    private static readonly HashSet<string> OohChannels =
    [
        MasterDataCodes.Channels.Ooh,
        MasterDataCodes.Channels.Dooh,
    ];

    private readonly Dictionary<string, CampaignModeDefinition> definitions;
    private readonly string[] activeChannels;

    private CampaignModePolicy(
        Dictionary<string, CampaignModeDefinition> definitions,
        string[] activeChannels)
    {
        this.definitions = definitions;
        this.activeChannels = activeChannels;
    }

    public static CampaignModePolicy Load()
    {
        var registry = MasterDataRegistryReader.Read();
        var modes = registry.Collections.Single(collection =>
            collection.Code == MasterDataCodes.CampaignModes.Collection);
        var channels = registry.Collections.Single(collection =>
            collection.Code == MasterDataCodes.Channels.Collection).Items
            .Where(item => item.IsActive)
            .OrderBy(item => item.SortOrder)
            .Select(item => item.Code)
            .ToArray();
        var definitions = modes.Items
            .Where(item => item.IsActive)
            .Select(ToDefinition)
            .ToDictionary(item => item.Code, StringComparer.Ordinal);
        Validate(definitions, channels);
        return new CampaignModePolicy(definitions, channels);
    }

    internal CampaignModeDefinition Require(string mode)
    {
        var normalized = mode.Trim().ToUpperInvariant();
        return definitions.TryGetValue(normalized, out var definition)
            ? definition
            : throw new ArgumentException("Choose a supported campaign mode.", nameof(mode));
    }

    internal string[] AllowedChannels(string mode)
    {
        var definition = Require(mode);
        return definition.AllowedChannels.Length == 0
            ? activeChannels.ToArray()
            : definition.AllowedChannels.ToArray();
    }

    internal string[] FilterAvailableChannels(
        string mode,
        IReadOnlyList<string> availableChannels)
    {
        var definition = Require(mode);
        var filtered = definition.AllowedChannels.Length == 0
            ? availableChannels
            : availableChannels.Where(definition.AllowedChannels.Contains).ToArray();
        return filtered.Distinct(StringComparer.Ordinal)
            .OrderBy(channel => Array.IndexOf(activeChannels, channel))
            .ToArray();
    }

    internal void EnsureAllocations(
        string mode,
        IReadOnlyList<MediaAllocationView> allocations)
    {
        var definition = Require(mode);
        if (definition.AllowedChannels.Length > 0 && allocations.Any(allocation =>
                !definition.AllowedChannels.Contains(
                    allocation.Channel, StringComparer.Ordinal)))
        {
            throw new CampaignModeLockedException();
        }
    }

    internal bool IsOohOnly(string mode) =>
        string.Equals(
            Require(mode).Code,
            MasterDataCodes.CampaignModes.OohOnly,
            StringComparison.Ordinal);

    private static CampaignModeDefinition ToDefinition(MasterDataRegistryItem item)
    {
        using var metadata = JsonDocument.Parse(item.MetadataJson);
        var root = metadata.RootElement;
        if (!root.TryGetProperty(AllowedChannelsField, out var allowedChannels) ||
            allowedChannels.ValueKind != JsonValueKind.Array ||
            !root.TryGetProperty(ImmutableAfterSelectionField, out var immutable) ||
            immutable.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new InvalidOperationException(
                $"Campaign mode {item.Code} has invalid metadata: {item.MetadataJson}");
        }
        return new CampaignModeDefinition(
            item.Code,
            allowedChannels.EnumerateArray()
                .Select(value => value.GetString())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Cast<string>()
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            immutable.GetBoolean());
    }

    private static void Validate(
        IReadOnlyDictionary<string, CampaignModeDefinition> definitions,
        IReadOnlyCollection<string> channels)
    {
        if (!definitions.TryGetValue(MasterDataCodes.CampaignModes.OohOnly, out var oohOnly) ||
            !definitions.TryGetValue(MasterDataCodes.CampaignModes.FullCampaign, out var full) ||
            !oohOnly.ImmutableAfterSelection ||
            !full.ImmutableAfterSelection ||
            oohOnly.AllowedChannels.Length == 0 ||
            oohOnly.AllowedChannels.Any(channel => !OohChannels.Contains(channel)) ||
            full.AllowedChannels.Length != 0 ||
            definitions.Values.SelectMany(value => value.AllowedChannels)
                .Any(channel => !channels.Contains(channel, StringComparer.Ordinal)))
        {
            throw new InvalidOperationException("The canonical campaign-mode policy is invalid.");
        }
    }
}

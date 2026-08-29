using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Advertified.Commercial.Domain.Constants;

namespace Advertified.Commercial.Infrastructure.MasterData;

internal static class MasterDataRegistryReader
{
    private const string ResourceName = "Advertified.MasterData.Registry.json";

    public static MasterDataRegistry Read()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("The governed master-data registry is not embedded.");
        var registry = JsonSerializer.Deserialize<RegistryDocument>(stream)
            ?? throw new InvalidOperationException("The governed master-data registry is invalid.");

        if (!DateOnly.TryParse(registry.EffectiveFrom, out var effectiveFrom))
        {
            throw new InvalidOperationException("The registry effective date is invalid.");
        }

        var collections = registry.Collections.Select(pair =>
            new MasterDataRegistryCollection(
                pair.Key,
                pair.Value.Select(item =>
                    new MasterDataRegistryItem(
                        item.Code,
                        item.DisplayLabel,
                        item.IsActive,
                        item.SortOrder,
                        item.Metadata?.GetRawText() ?? "{}"))
                    .ToArray()))
            .ToArray();

        ValidatePermissionMappings(collections);

        return new MasterDataRegistry(
            registry.RegistryVersion,
            effectiveFrom,
            collections);
    }

    private static void ValidatePermissionMappings(
        IReadOnlyList<MasterDataRegistryCollection> collections)
    {
        var roles = RequireCollection(collections, MasterDataConstants.RoleCollection)
            .Items.Select(item => item.Code)
            .ToHashSet(StringComparer.Ordinal);
        var permissions = RequireCollection(
            collections,
            MasterDataConstants.PermissionCollection);

        foreach (var permission in permissions.Items)
        {
            var assignedRoles = PermissionRoleMetadata.ReadRoles(permission.MetadataJson);
            if (assignedRoles.Count == 0 || !assignedRoles.IsSubsetOf(roles))
            {
                throw new InvalidOperationException(
                    "A permission references an unknown or empty role mapping.");
            }
        }
    }

    private static MasterDataRegistryCollection RequireCollection(
        IReadOnlyList<MasterDataRegistryCollection> collections,
        string collectionCode)
    {
        return collections.SingleOrDefault(item => item.Code == collectionCode)
            ?? throw new InvalidOperationException(
                $"The governed {collectionCode} collection is missing.");
    }

    private sealed record RegistryDocument(
        [property: JsonPropertyName("registryVersion")] string RegistryVersion,
        [property: JsonPropertyName("effectiveFrom")] string EffectiveFrom,
        [property: JsonPropertyName("collections")]
        Dictionary<string, RegistryItem[]> Collections);

    private sealed record RegistryItem(
        [property: JsonPropertyName("code")] string Code,
        [property: JsonPropertyName("displayLabel")] string DisplayLabel,
        [property: JsonPropertyName("isActive")] bool IsActive,
        [property: JsonPropertyName("sortOrder")] int SortOrder,
        [property: JsonPropertyName("metadata")] JsonElement? Metadata);
}

internal sealed record MasterDataRegistry(
    string RegistryVersion,
    DateOnly EffectiveFrom,
    IReadOnlyList<MasterDataRegistryCollection> Collections);

internal sealed record MasterDataRegistryCollection(
    string Code,
    IReadOnlyList<MasterDataRegistryItem> Items);

internal sealed record MasterDataRegistryItem(
    string Code,
    string DisplayLabel,
    bool IsActive,
    int SortOrder,
    string MetadataJson);

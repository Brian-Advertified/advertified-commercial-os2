using System.Text.Json;

namespace Advertified.Commercial.Infrastructure.MasterData;

internal static class PermissionRoleMetadata
{
    private const string RolesProperty = "roles";

    public static IReadOnlySet<string> ReadRoles(string metadataJson)
    {
        using var document = JsonDocument.Parse(metadataJson);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            root.EnumerateObject().Any(property => property.Name != RolesProperty) ||
            !root.TryGetProperty(RolesProperty, out var rolesElement) ||
            rolesElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Permission metadata is invalid.");
        }

        var roles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var roleElement in rolesElement.EnumerateArray())
        {
            var role = roleElement.GetString();
            if (string.IsNullOrWhiteSpace(role) || !roles.Add(role))
            {
                throw new InvalidOperationException("Permission role mapping is invalid.");
            }
        }

        return roles;
    }
}

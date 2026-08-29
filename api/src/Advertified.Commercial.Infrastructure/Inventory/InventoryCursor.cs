using System.Text;
using System.Text.Json;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal sealed record InventoryCursorValue(string Name, Guid Id);

internal static class InventoryCursor
{
    internal static string Encode(string name, Guid id)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new InventoryCursorValue(name, id));
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    internal static InventoryCursorValue? Decode(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }
        try
        {
            var normalized = cursor.Replace('-', '+').Replace('_', '/');
            normalized = normalized.PadRight(normalized.Length + (4 - normalized.Length % 4) % 4, '=');
            return JsonSerializer.Deserialize<InventoryCursorValue>(
                Convert.FromBase64String(normalized));
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            throw new ArgumentException("The inventory cursor is invalid.", nameof(cursor));
        }
    }
}

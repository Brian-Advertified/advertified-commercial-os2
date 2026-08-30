using System.Text.Json;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal sealed record InventoryCursorValue(string Name, Guid Id);
internal sealed record InventoryCandidateCursorValue(int RowNumber, Guid Id);

internal static class InventoryCursor
{
    internal static string Encode(string name, Guid id) =>
        InventoryCursorCodec.Encode(new InventoryCursorValue(name, id));

    internal static InventoryCursorValue? Decode(string? cursor)
    {
        var value = InventoryCursorCodec.Decode<InventoryCursorValue>(
            cursor, "The inventory cursor is invalid.");
        return value is null || value.Id != Guid.Empty && !string.IsNullOrWhiteSpace(value.Name)
            ? value
            : throw new ArgumentException("The inventory cursor is invalid.", nameof(cursor));
    }
}

internal static class InventoryCandidateCursor
{
    internal static string Encode(int rowNumber, Guid id) =>
        InventoryCursorCodec.Encode(new InventoryCandidateCursorValue(rowNumber, id));

    internal static InventoryCandidateCursorValue? Decode(string? cursor)
    {
        var value = InventoryCursorCodec.Decode<InventoryCandidateCursorValue>(
            cursor, "The inventory candidate cursor is invalid.");
        return value is null || value.RowNumber > 0 && value.Id != Guid.Empty
            ? value
            : throw new ArgumentException(
                "The inventory candidate cursor is invalid.", nameof(cursor));
    }
}

internal static class InventoryCursorCodec
{
    private const int MaximumEncodedLength = 1_024;

    internal static string Encode<T>(T value)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    internal static T? Decode<T>(string? cursor, string error)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(cursor)) return null;
        if (cursor.Length > MaximumEncodedLength)
        {
            throw new ArgumentException(error, nameof(cursor));
        }
        try
        {
            var normalized = cursor.Replace('-', '+').Replace('_', '/');
            normalized = normalized.PadRight(
                normalized.Length + (4 - normalized.Length % 4) % 4, '=');
            return JsonSerializer.Deserialize<T>(Convert.FromBase64String(normalized))
                ?? throw new JsonException();
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            throw new ArgumentException(error, nameof(cursor), exception);
        }
    }
}

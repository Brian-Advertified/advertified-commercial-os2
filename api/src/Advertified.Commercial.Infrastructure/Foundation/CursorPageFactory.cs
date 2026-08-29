using System.Globalization;
using System.Text;
using Advertified.Commercial.Application.Foundation;

namespace Advertified.Commercial.Infrastructure.Foundation;

internal static class CursorPageFactory
{
    private const int DefaultLimit = 25;
    private const int MaximumLimit = 100;
    private const string CursorVersion = "v1";

    public static (int Limit, int Offset) Parse(int limit, string? cursor)
    {
        var effectiveLimit = limit == 0 ? DefaultLimit : limit;
        if (effectiveLimit is < 1 or > MaximumLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit),
                $"The page size must be between 1 and {MaximumLimit}.");
        }

        return (effectiveLimit, Decode(cursor));
    }

    public static CursorPage<T> Create<T>(IReadOnlyList<T> rows, int limit, int offset)
    {
        var hasMore = rows.Count > limit;
        var items = hasMore ? rows.Take(limit).ToArray() : rows;
        return new CursorPage<T>(items, hasMore ? Encode(offset + limit) : null);
    }

    private static int Decode(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return 0;
        }

        try
        {
            var bytes = Convert.FromBase64String(cursor);
            var value = Encoding.UTF8.GetString(bytes).Split(':', 2);
            if (value.Length == 2 && value[0] == CursorVersion &&
                int.TryParse(value[1], NumberStyles.None, CultureInfo.InvariantCulture, out var offset) &&
                offset >= 0)
            {
                return offset;
            }
        }
        catch (FormatException)
        {
        }

        throw new ArgumentException("The cursor is invalid.", nameof(cursor));
    }

    private static string Encode(int offset)
    {
        var value = $"{CursorVersion}:{offset.ToString(CultureInfo.InvariantCulture)}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
    }
}

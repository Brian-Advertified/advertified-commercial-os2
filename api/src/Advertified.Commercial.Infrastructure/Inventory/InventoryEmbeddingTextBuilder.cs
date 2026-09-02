using System.Text.RegularExpressions;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static partial class InventoryEmbeddingTextBuilder
{
    private const int MaximumTextLength = 8_000;

    internal static string Build(InventoryCommands.EmbeddingProductRow product)
    {
        var fields = new[]
        {
            ("product_code", product.ProductCode),
            ("name", product.Name),
            ("channel", product.Channel),
            ("product_type", product.ProductType),
            ("geography", product.Geography),
            ("description", product.Description),
        };
        var canonical = string.Join('\n', fields
            .Where(item => !string.IsNullOrWhiteSpace(item.Item2))
            .Select(item => $"{item.Item1}:{Sanitize(item.Item2!)}"));
        return canonical.Length <= MaximumTextLength
            ? canonical
            : canonical[..MaximumTextLength];
    }

    private static string Sanitize(string value)
    {
        var sanitized = EmailPattern().Replace(value, "[redacted-email]");
        sanitized = UrlPattern().Replace(sanitized, "[redacted-url]");
        sanitized = LongNumberPattern().Replace(sanitized, "[redacted-number]");
        sanitized = ControlPattern().Replace(sanitized, " ");
        return WhitespacePattern().Replace(sanitized, " ").Trim();
    }

    [GeneratedRegex(@"\b[^\s@]+@[^\s@]+\.[^\s@]+\b", RegexOptions.IgnoreCase)]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"\bhttps?://\S+", RegexOptions.IgnoreCase)]
    private static partial Regex UrlPattern();

    [GeneratedRegex(@"\b\d{7,}\b")]
    private static partial Regex LongNumberPattern();

    [GeneratedRegex(@"[\p{Cc}\p{Cf}]")]
    private static partial Regex ControlPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}

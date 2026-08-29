using System.Net.Mail;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Advertified.Commercial.Domain.Commercial;

public readonly record struct EmailAddress
{
    public EmailAddress(string value)
    {
        var normalized = CommercialValue.Required(value, 320, nameof(value)).ToLowerInvariant();

        try
        {
            var parsed = new MailAddress(normalized);
            if (!string.Equals(parsed.Address, normalized, StringComparison.OrdinalIgnoreCase))
            {
                throw new FormatException();
            }
        }
        catch (FormatException)
        {
            throw new ArgumentException("A valid email address is required.", nameof(value));
        }

        Value = normalized;
    }

    public string Value { get; }
}

public readonly record struct Slug
{
    private static readonly Regex Pattern = new(
        "^[a-z0-9]+(?:-[a-z0-9]+)*$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public Slug(string value)
    {
        var normalized = CommercialValue.Required(value, 100, nameof(value)).ToLowerInvariant();
        if (!Pattern.IsMatch(normalized))
        {
            throw new ArgumentException(
                "A slug may contain lowercase letters, numbers and single hyphens.",
                nameof(value));
        }

        Value = normalized;
    }

    public string Value { get; }
}

internal static class CommercialValue
{
    public static string Required(string value, int maximumLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();

        return normalized.Length > maximumLength
            ? throw new ArgumentOutOfRangeException(parameterName, $"Value cannot exceed {maximumLength} characters.")
            : normalized;
    }

    public static string? Optional(string? value, int maximumLength, string parameterName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : Required(value, maximumLength, parameterName);
    }

    public static string JsonObject(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        try
        {
            using var document = JsonDocument.Parse(value);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException();
            }

            return document.RootElement.GetRawText();
        }
        catch (JsonException)
        {
            throw new ArgumentException("A valid JSON object is required.", parameterName);
        }
    }

    public static string? Website(string? value, string parameterName)
    {
        var normalized = Optional(value, 2048, parameterName);
        if (normalized is null)
        {
            return null;
        }

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new ArgumentException("A valid HTTP or HTTPS website is required.", parameterName);
        }

        return uri.AbsoluteUri;
    }

    public static DateTimeOffset Utc(DateTimeOffset value, string parameterName)
    {
        return value.Offset == TimeSpan.Zero
            ? value
            : throw new ArgumentException("The timestamp must be UTC.", parameterName);
    }
}

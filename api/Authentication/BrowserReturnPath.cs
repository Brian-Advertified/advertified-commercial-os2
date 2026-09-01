namespace Advertified.Commercial.Api.Authentication;

internal static class BrowserReturnPath
{
    internal const string DefaultPath = "/workspaces";

    internal static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DefaultPath;
        }
        var candidate = value.Trim();
        return candidate.StartsWith('/', StringComparison.Ordinal) &&
            !candidate.StartsWith("//", StringComparison.Ordinal) &&
            !candidate.Contains('\\') &&
            !candidate.Contains('\r') &&
            !candidate.Contains('\n')
            ? candidate
            : DefaultPath;
    }
}

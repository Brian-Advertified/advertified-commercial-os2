namespace Advertified.Commercial.Infrastructure.Brief;

internal sealed record ExtractedField(
    string Path,
    string Value,
    string Kind,
    decimal Confidence,
    string Excerpt,
    string SourceLocator);

internal sealed record ModeDecision(
    string? Code,
    decimal Confidence,
    string Rationale,
    string Kind,
    string Excerpt,
    string SourceLocator);

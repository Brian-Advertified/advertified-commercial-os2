using MigraDoc;
using PdfSharp.Fonts;

namespace Advertified.Commercial.Infrastructure.Proposal;

internal sealed class ProposalFontResolver : IFontResolver
{
    internal const string FamilyName = "Advertified Open Sans";
    private const string RegularFace = "advertified-opensans-regular";
    private const string BoldFace = "advertified-opensans-bold";
    private static readonly Lock Sync = new();
    private static readonly Lazy<Dictionary<string, byte[]>> Fonts = new(LoadFonts);

    internal static void EnsureConfigured()
    {
        lock (Sync)
        {
            if (GlobalFontSettings.FontResolver is null)
            {
                GlobalFontSettings.FontResolver = new ProposalFontResolver();
            }
            else if (GlobalFontSettings.FontResolver is not ProposalFontResolver)
            {
                throw new InvalidOperationException(
                    "PDF font resolving was configured by another component before the proposal renderer.");
            }
            PredefinedFontsAndChars.ErrorFontName = FamilyName;
            PredefinedFontsAndChars.Bullets.Level1FontName = FamilyName;
            PredefinedFontsAndChars.Bullets.Level2FontName = FamilyName;
            PredefinedFontsAndChars.Bullets.Level3FontName = FamilyName;
        }
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic)
    {
        if (!string.Equals(familyName, FamilyName, StringComparison.OrdinalIgnoreCase)) return null;
        return new FontResolverInfo(bold ? BoldFace : RegularFace, false, italic);
    }

    public byte[]? GetFont(string faceName) =>
        Fonts.Value.TryGetValue(faceName, out var bytes) ? bytes : null;

    private static Dictionary<string, byte[]> LoadFonts()
    {
        var regular = FindFont("OpenSans-Regular.ttf", "OpenSans.ttf");
        var bold = FindFont("OpenSans-Bold.ttf", "OpenSans-SemiBold.ttf") ?? regular;
        if (regular is null)
        {
            throw new InvalidOperationException(
                "Proposal PDF font assets are missing from the application output. " +
                "Restore the Uno.Fonts.OpenSans package before generating proposals.");
        }
        return new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [RegularFace] = File.ReadAllBytes(regular),
            [BoldFace] = File.ReadAllBytes(bold!),
        };
    }

    private static string? FindFont(params string[] names)
    {
        foreach (var name in names)
        {
            var match = Directory.EnumerateFiles(
                    AppContext.BaseDirectory, name, SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal)
                .FirstOrDefault();
            if (match is not null) return match;
        }
        return null;
    }
}

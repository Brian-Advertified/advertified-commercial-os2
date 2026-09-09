using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal sealed record CanonicalInventoryOutletIdentity(
    string Id, string Name, string Basis, string? SourceLocator);

internal static partial class InventoryOutletIdentity
{
    private const string ExplicitCodeBasis = "SOURCE_OUTLET_CODE";
    private const string ExplicitNameBasis = "SOURCE_OUTLET_NAME";
    private const string TitlePatternBasis = "SOURCE_TITLE_PATTERN";

    internal static CanonicalInventoryOutletIdentity? Resolve(
        InventoryCandidateValues values, string fallbackLocator)
    {
        if (values.Channel is not (MasterDataCodes.Channels.Radio or
            MasterDataCodes.Channels.Tv or MasterDataCodes.Channels.Print)) return null;
        if (values.OutletIdentity is { } supplied)
            return Explicit(values.Channel, supplied, fallbackLocator);
        var name = TitleName(values.Channel, values.Name);
        return name is null ? null : new(
            StableId(values.Channel, name), name, TitlePatternBasis, fallbackLocator);
    }

    private static CanonicalInventoryOutletIdentity Explicit(
        string channel, InventoryOutletIdentityValues supplied, string fallbackLocator)
    {
        var name = supplied.Name.Trim();
        if (name.Length is < 1 or > 500)
            throw new InventoryPublishBlockedException();
        var code = supplied.Code?.Trim();
        if (code?.Length > 200)
            throw new InventoryPublishBlockedException();
        var identity = string.IsNullOrWhiteSpace(code) ? name : code;
        return new(StableId(channel, identity), name,
            string.IsNullOrWhiteSpace(code) ? ExplicitNameBasis : ExplicitCodeBasis,
            supplied.SourceLocator?.Trim() ?? fallbackLocator);
    }

    internal static string? TitleName(string channel, string? productName)
    {
        if (string.IsNullOrWhiteSpace(productName)) return null;
        var name = productName.Trim();
        if (channel != MasterDataCodes.Channels.Print && Bundle().IsMatch(name)) return null;
        var separator = name.IndexOf(" — ", StringComparison.Ordinal);
        var daypart = Daypart().Match(name);
        var spot = SpotSuffix().Match(name);
        if (separator >= 0) name = name[..separator];
        else if (daypart.Success) name = name[..daypart.Index].TrimEnd(' ', '-');
        else if (spot.Success) name = name[..spot.Index];
        else return null;
        name = SpotSuffix().Replace(name, string.Empty).Trim();
        if (channel == MasterDataCodes.Channels.Tv)
            name = ChannelNumber().Replace(name, string.Empty).Trim();
        return name.Length is > 0 and <= 500 && !name.Contains(',') ? name : null;
    }

    internal static string StableId(string channel, string identity)
    {
        var key = channel + ":" + identity.Trim().ToUpperInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
    }

    [GeneratedRegex(@"\s+(?:MONDAY(?:[-_]FRIDAY)?|SATURDAY|SUNDAY)\s+\d", RegexOptions.IgnoreCase)]
    private static partial Regex Daypart();
    [GeneratedRegex(@"\s+\d+[- ]second\s+(?:spot|commercial)$", RegexOptions.IgnoreCase)]
    private static partial Regex SpotSuffix();
    [GeneratedRegex(@"\s+channel\s+\d+$", RegexOptions.IgnoreCase)]
    private static partial Regex ChannelNumber();
    [GeneratedRegex(@"\b(?:package|simulcast|banner|CPM|CPV)\b|\+", RegexOptions.IgnoreCase)]
    private static partial Regex Bundle();
}

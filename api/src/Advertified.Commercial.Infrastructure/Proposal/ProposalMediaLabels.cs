using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;
using System.Text.RegularExpressions;

namespace Advertified.Commercial.Infrastructure.Proposal;

// Client presentation content; canonical commercial codes remain unchanged.
internal static class ProposalMediaLabels
{
    private static readonly IReadOnlyDictionary<string, string> Labels =
        MasterDataRegistryReader.Read().Collections
            .Single(item => item.Code == MasterDataCodes.Channels.Collection).Items
            .ToDictionary(item => item.Code, item => item.DisplayLabel, StringComparer.Ordinal);

    internal static string Channel(string code) => code switch
    {
        MasterDataCodes.Channels.Ooh => "Outdoor advertising",
        MasterDataCodes.Channels.Dooh => "Digital screens",
        _ => Labels.GetValueOrDefault(code, code),
    };

    internal static bool NarrativeIncludesChannel(string narrative, string code) => Regex.IsMatch(
        narrative,
        @"(?<![\p{L}\p{N}_])(?:" + Regex.Escape(code) + "|" + Regex.Escape(Channel(code)) +
        @")(?![\p{L}\p{N}_])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static string ClientText(string value) => Regex.Replace(
        value, @"\b(?:DOOH|OOH)\b", match => Channel(match.Value));
}

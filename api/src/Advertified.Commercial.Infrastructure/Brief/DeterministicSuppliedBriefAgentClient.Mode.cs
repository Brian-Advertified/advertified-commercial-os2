using System.Text.RegularExpressions;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Brief;

public sealed partial class DeterministicSuppliedBriefAgentClient
{
    private ModeDecision ResolveMode(
        string content,
        string[] channels,
        Dictionary<string, string> clarifications)
    {
        var clarification = ClarifiedMode(clarifications);
        if (clarification is not null) return clarification;

        var scopeTerm = policy.ScopeClarificationTerms.FirstOrDefault(term =>
            ContainsTerm(content, term));
        if (scopeTerm is not null)
        {
            return UnresolvedMode(
                FindEvidenceExcerpt(content, policy.ScopeClarificationTerms),
                $"The requested '{scopeTerm}' service is not a governed OOH/DOOH channel and its campaign scope requires clarification.");
        }

        var explicitMedia = ExtractLabel(content, policy.MediaLabels);
        var onlyOoh = channels.Length > 0 && channels.All(IsOohChannel);
        var hasNonOoh = channels.Any(channel => !IsOohChannel(channel));
        if (explicitMedia is not null && hasNonOoh)
        {
            return FullCampaign(explicitMedia.Excerpt, explicitMedia.SourceLocator, true);
        }
        if (explicitMedia is not null && onlyOoh)
        {
            return HasNonExclusiveLanguage(explicitMedia.Value) &&
                !ExplicitOohScope().IsMatch(explicitMedia.Value)
                ? UnresolvedMode(explicitMedia.Excerpt,
                    "The supplied wording does not restrict the campaign to OOH/DOOH.")
                : OohOnly(explicitMedia.Excerpt, explicitMedia.SourceLocator, true);
        }

        var normalized = content.ToLowerInvariant();
        var hasOoh = policy.OohTerms.Any(term => ContainsTerm(normalized, term));
        var hasFull = policy.FullCampaignTerms.Any(term => ContainsTerm(normalized, term)) ||
            hasNonOoh;
        if (hasFull)
        {
            return FullCampaign(FindEvidenceExcerpt(content, policy.FullCampaignTerms),
                SourceLocator, false);
        }

        var directOoh = FindDirectOohInstruction(content);
        if (directOoh is not null) return OohOnly(directOoh, SourceLocator, true);
        if (hasOoh || onlyOoh)
        {
            var evidence = FindEvidenceExcerpt(content, policy.OohTerms);
            return HasNonExclusiveLanguage(evidence)
                ? UnresolvedMode(evidence,
                    "The supplied wording suggests OOH/DOOH but does not restrict the campaign to it.")
                : OohOnly(evidence, SourceLocator, false);
        }
        return UnresolvedMode(FirstSentence(content),
            "The supplied requirement does not establish whether media is OOH-only or unrestricted.");
    }

    private static ModeDecision? ClarifiedMode(
        Dictionary<string, string> clarifications)
    {
        if (!clarifications.TryGetValue(ModePath, out var selected)) return null;
        var normalized = selected.Trim().ToUpperInvariant();
        return normalized is MasterDataCodes.CampaignModes.OohOnly or
            MasterDataCodes.CampaignModes.FullCampaign
            ? new ModeDecision(
                normalized, 1m,
                "The campaign mode was supplied to resolve an unclear requirement.",
                MasterDataCodes.EvidenceClassifications.Fact,
                selected,
                "clarification:brief")
            : null;
    }

    private string? FindDirectOohInstruction(string content) =>
        Lines(content)
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.Length > 0 &&
                policy.OohTerms.Any(term => ContainsTerm(line, term)) &&
                DirectMediaRequest().IsMatch(line) &&
                (!HasNonExclusiveLanguage(line) || ExplicitOohScope().IsMatch(line)));

    private static bool IsOohChannel(string channel) => channel is
        MasterDataCodes.Channels.Ooh or MasterDataCodes.Channels.Dooh;

    private static bool HasNonExclusiveLanguage(string value) =>
        NonExclusiveLanguage().IsMatch(value);

    private static ModeDecision OohOnly(string excerpt, string source, bool direct) =>
        new(
            MasterDataCodes.CampaignModes.OohOnly,
            direct ? 1m : 0.95m,
            direct
                ? "The supplied media requirement identifies only OOH/DOOH media."
                : "The supplied requirement indicates an OOH/DOOH-only campaign.",
            direct
                ? MasterDataCodes.EvidenceClassifications.Fact
                : MasterDataCodes.EvidenceClassifications.Inference,
            excerpt,
            source);

    private static ModeDecision FullCampaign(string excerpt, string source, bool direct) =>
        new(
            MasterDataCodes.CampaignModes.FullCampaign,
            direct ? 1m : 0.95m,
            direct
                ? "The supplied media requirement includes at least one channel beyond OOH/DOOH."
                : "The supplied requirement indicates media beyond OOH/DOOH.",
            direct
                ? MasterDataCodes.EvidenceClassifications.Fact
                : MasterDataCodes.EvidenceClassifications.Inference,
            excerpt,
            source);

    private static ModeDecision UnresolvedMode(string excerpt, string rationale) =>
        new(
            null,
            0m,
            rationale,
            MasterDataCodes.EvidenceClassifications.Hypothesis,
            excerpt,
            SourceLocator);

    [GeneratedRegex(
        @"\b(only|please|kindly|share|looking\s+for|need|needs|seeking|require|requires|request|targeting|identify|opportunit(?:y|ies)|sites?|branding|media|formats?)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DirectMediaRequest();

    [GeneratedRegex(
        @"\bout[- ]of[- ]home\s+(?:media\s+)?(?:opportunit(?:y|ies)|campaign|advertising)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExplicitOohScope();

    [GeneratedRegex(
        @"\b(prefer|preferred|preference|consider|including|such\s+as|for\s+example|e\.g\.|at\s+least|open\s+to)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NonExclusiveLanguage();
}

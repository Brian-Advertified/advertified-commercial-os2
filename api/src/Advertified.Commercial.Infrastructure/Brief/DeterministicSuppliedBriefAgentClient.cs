using System.Text.RegularExpressions;
using Advertified.Commercial.Application.Brief;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Brief;

public sealed partial class DeterministicSuppliedBriefAgentClient(
    SuppliedBriefAgentPolicy policy) : ISuppliedBriefAgentClient
{
    private readonly SuppliedBriefBudgetParser budgetParser = new(policy);

    private const string ClientPath = "clientName";
    private const string ProblemPath = "businessProblem";
    private const string ObjectivePath = "objective";
    private const string AudiencePath = "audiences";
    private const string GeographyPath = "geographies";
    private const string TimingPath = "timing";
    private const string BudgetPath = "budget";
    private const string ModePath = "campaignMode";
    private const string MeasurementPath = MasterDataCodes.AgentTypes.Measurement;
    private const string ConstraintsPath = "constraints";
    private const string SourceLocator = "supplied:brief";

    public Task<SuppliedBriefUnderstandingView> UnderstandAsync(
        SuppliedBriefAgentInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var title = Required(input.SourceTitle, 300, nameof(input.SourceTitle));
        var content = Required(input.SourceContent, 262_144, nameof(input.SourceContent));
        var clarifications = input.Clarifications
            .GroupBy(item => item.FieldPath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last().Value.Trim(),
                StringComparer.OrdinalIgnoreCase);
        var fields = ExtractFields(content, clarifications);
        var channels = DetectChannels(content, clarifications);
        var mode = ResolveMode(content, channels, clarifications);
        var budgetField = fields.GetValueOrDefault(BudgetPath);
        var budgetFromClarification = budgetField?.SourceLocator == "clarification:brief";
        var budget = budgetParser.Parse(
            budgetFromClarification ? budgetField?.Value : content,
            budgetFromClarification);
        var questions = BuildQuestions(fields, mode, budget);
        var unknowns = questions.Select(question => new BriefUnknownInput(
            question.FieldPath, question.Question, question.IsBlocking)).ToArray();
        var evidence = BuildEvidence(fields, mode);
        var assumptions = fields.Values
            .Where(value => value.Kind == MasterDataCodes.EvidenceClassifications.Inference)
            .Select(value => new BriefAssumptionInput(
                value.Path,
                value.Value,
                "Planning depends on this interpretation.",
                "Confirm only if the interpretation is not what the sender intended."))
            .ToArray();
        var objective = fields.GetValueOrDefault(ObjectivePath)?.Value ?? string.Empty;
        var problem = fields.GetValueOrDefault(ProblemPath)?.Value
            ?? (objective.Length == 0
                ? string.Empty
                : $"The supplied request requires a campaign response to: {objective}");
        var draft = new SuppliedBriefDraftView(
            problem,
            objective,
            SplitValues(fields.GetValueOrDefault(AudiencePath)?.Value),
            SplitValues(fields.GetValueOrDefault(GeographyPath)?.Value),
            fields.GetValueOrDefault(TimingPath)?.Value ?? string.Empty,
            budget?.AmountMinor,
            budget is null,
            budget?.Currency,
            ParseVatStatus(content),
            null,
            channels,
            SplitValues(fields.GetValueOrDefault(ConstraintsPath)?.Value),
            SplitValues(fields.GetValueOrDefault(MeasurementPath)?.Value),
            evidence.Where(item => item.Kind != MasterDataCodes.EvidenceClassifications.Inference)
                .Select(item => item.Excerpt).Distinct(StringComparer.Ordinal).ToArray(),
            unknowns,
            assumptions,
            Array.Empty<BriefConflictInput>());
        return Task.FromResult(new SuppliedBriefUnderstandingView(
            fields.GetValueOrDefault(ClientPath)?.Value,
            title,
            mode.Code,
            mode.Confidence,
            questions.Any(question => question.IsBlocking),
            mode.Rationale,
            draft,
            questions,
            evidence,
            new SuppliedBriefAgentUsageView(
                "deterministic",
                policy.Version,
                policy.Version,
                "NOT_REQUESTED",
                0,
                0)));
    }

    private Dictionary<string, ExtractedField> ExtractFields(
        string content,
        Dictionary<string, string> clarifications)
    {
        var fields = new Dictionary<string, ExtractedField>(StringComparer.OrdinalIgnoreCase);
        Add(fields, ClientPath, ExtractLabel(content, policy.ClientLabels), clarifications);
        Add(fields, ProblemPath, ExtractLabel(content, policy.ProblemLabels), clarifications);
        Add(fields, ObjectivePath,
            ExtractLabel(content, policy.ObjectiveLabels) ?? ExtractObjective(content),
            clarifications);
        Add(fields, AudiencePath,
            ExtractLabel(content, policy.AudienceLabels) ?? ExtractAfterCue(content, "target"),
            clarifications);
        Add(fields, GeographyPath,
            ExtractLabel(content, policy.GeographyLabels) ?? ExtractAfterCue(content, " in "),
            clarifications);
        Add(fields, TimingPath,
            ExtractLabel(content, policy.TimingLabels) ?? ExtractTiming(content),
            clarifications);
        Add(fields, BudgetPath, ExtractBudgetText(content), clarifications);
        Add(fields, MeasurementPath, ExtractLabel(content, policy.MeasurementLabels), clarifications);
        Add(fields, ConstraintsPath, ExtractLabel(content, policy.ConstraintLabels), clarifications);
        return fields;
    }

    private static void Add(
        Dictionary<string, ExtractedField> fields,
        string path,
        ExtractedField? extracted,
        Dictionary<string, string> clarifications)
    {
        if (clarifications.TryGetValue(path, out var clarified) &&
            !string.IsNullOrWhiteSpace(clarified))
        {
            fields[path] = new ExtractedField(
                path, clarified, MasterDataCodes.EvidenceClassifications.Fact,
                1m, clarified, "clarification:brief");
            return;
        }
        if (extracted is not null)
        {
            fields[path] = extracted with { Path = path };
        }
    }

    private string[] DetectChannels(
        string content,
        Dictionary<string, string> clarifications)
    {
        var corpus = clarifications.TryGetValue("mediaRequirements", out var clarified)
            ? $"{content}\n{clarified}"
            : content;
        var normalized = corpus.ToLowerInvariant();
        return policy.ChannelTerms
            .Where(pair => pair.Value.Any(term => ContainsTerm(normalized, term)))
            .Select(pair => pair.Key)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private ModeDecision ResolveMode(
        string content,
        string[] channels,
        Dictionary<string, string> clarifications)
    {
        if (clarifications.TryGetValue(ModePath, out var selected))
        {
            var normalized = selected.Trim().ToUpperInvariant();
            if (normalized is MasterDataCodes.CampaignModes.OohOnly or
                MasterDataCodes.CampaignModes.FullCampaign)
            {
                return new ModeDecision(
                    normalized, 1m,
                    "The campaign mode was supplied to resolve an unclear requirement.",
                    MasterDataCodes.EvidenceClassifications.Fact,
                    selected,
                    "clarification:brief");
            }
        }
        var normalizedContent = content.ToLowerInvariant();
        var hasOohSignal = policy.OohTerms.Any(term => ContainsTerm(normalizedContent, term));
        var hasFullSignal = policy.FullCampaignTerms.Any(term => ContainsTerm(normalizedContent, term)) ||
            channels.Any(channel => channel is not (
                MasterDataCodes.Channels.Ooh or MasterDataCodes.Channels.Dooh));
        if (hasFullSignal)
        {
            return new ModeDecision(
                MasterDataCodes.CampaignModes.FullCampaign,
                0.95m,
                "The supplied requirement explicitly includes media beyond OOH/DOOH.",
                MasterDataCodes.EvidenceClassifications.Inference,
                FindEvidenceExcerpt(content, policy.FullCampaignTerms),
                SourceLocator);
        }
        if (hasOohSignal || channels.Length > 0 && channels.All(channel => channel is
                MasterDataCodes.Channels.Ooh or MasterDataCodes.Channels.Dooh))
        {
            return new ModeDecision(
                MasterDataCodes.CampaignModes.OohOnly,
                0.95m,
                "The supplied requirement identifies only OOH/DOOH media.",
                MasterDataCodes.EvidenceClassifications.Inference,
                FindEvidenceExcerpt(content, policy.OohTerms),
                SourceLocator);
        }
        return new ModeDecision(
            null,
            0m,
            "The supplied requirement does not establish whether media is OOH-only or unrestricted.",
            MasterDataCodes.EvidenceClassifications.Hypothesis,
            FirstSentence(content),
            SourceLocator);
    }

    private SuppliedBriefQuestionView[] BuildQuestions(
        Dictionary<string, ExtractedField> fields,
        ModeDecision mode,
        ParsedSuppliedBriefBudget? budget)
    {
        var questions = new List<SuppliedBriefQuestionView>();
        RequireField(questions, fields, ClientPath, "Which client or brand is this campaign for?");
        RequireField(questions, fields, ObjectivePath, "What must the campaign achieve?");
        RequireField(questions, fields, AudiencePath, "Who must the campaign reach?");
        RequireField(questions, fields, GeographyPath, "Where must the campaign run?");
        RequireField(questions, fields, TimingPath, "When must the campaign run?");
        if (budget is null)
        {
            questions.Add(new SuppliedBriefQuestionView(
                BudgetPath, "What budget is available for media?", true, Array.Empty<string>()));
        }
        if (mode.Code is null || mode.Confidence < policy.MinimumModeConfidence)
        {
            questions.Add(new SuppliedBriefQuestionView(
                ModePath,
                "Should this use only OOH/DOOH, or may the plan use other media too?",
                true,
                [MasterDataCodes.CampaignModes.OohOnly,
                    MasterDataCodes.CampaignModes.FullCampaign]));
        }
        return questions.ToArray();
    }

    private static void RequireField(
        List<SuppliedBriefQuestionView> questions,
        Dictionary<string, ExtractedField> fields,
        string path,
        string question)
    {
        if (!fields.TryGetValue(path, out var value) || string.IsNullOrWhiteSpace(value.Value))
        {
            questions.Add(new SuppliedBriefQuestionView(
                path, question, true, Array.Empty<string>()));
        }
    }

    private static SuppliedBriefEvidenceView[] BuildEvidence(
        Dictionary<string, ExtractedField> fields,
        ModeDecision mode)
    {
        var evidence = fields.Values.Select(value => new SuppliedBriefEvidenceView(
            value.Path, value.Kind, value.Excerpt, value.Confidence, value.SourceLocator)).ToList();
        evidence.Add(new SuppliedBriefEvidenceView(
            ModePath, mode.Kind, mode.Excerpt, mode.Confidence, mode.SourceLocator));
        return evidence.ToArray();
    }

    private static ExtractedField? ExtractLabel(string content, IReadOnlyList<string> labels)
    {
        foreach (var line in Lines(content))
        {
            foreach (var label in labels)
            {
                var match = Regex.Match(
                    line,
                    $"^\\s*{Regex.Escape(label)}\\s*[:=-]\\s*(?<value>.+)$",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                if (match.Success)
                {
                    return SourceField(match.Groups["value"].Value.Trim(), line, 0.98m);
                }
            }
        }
        return null;
    }

    private static ExtractedField? ExtractObjective(string content)
    {
        var sentence = Sentences(content).FirstOrDefault(value =>
            Regex.IsMatch(value, "\\b(need|needs|want|wants|goal|objective|looking for)\\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
        return sentence is null ? null : InferredField(sentence, sentence, 0.72m);
    }

    private static ExtractedField? ExtractAfterCue(string content, string cue)
    {
        var sentence = Sentences(content).FirstOrDefault(value =>
            value.Contains(cue, StringComparison.OrdinalIgnoreCase));
        if (sentence is null) return null;
        var index = sentence.IndexOf(cue, StringComparison.OrdinalIgnoreCase);
        var value = sentence[(index + cue.Length)..].Trim(' ', '.', ';', ':');
        return value.Length == 0 ? null : InferredField(value, sentence, 0.62m);
    }

    private static ExtractedField? ExtractTiming(string content)
    {
        var sentence = Sentences(content).FirstOrDefault(value =>
            NumericDate().IsMatch(value) || DateRangeCue().IsMatch(value));
        return sentence is null ? null : InferredField(sentence, sentence, 0.70m);
    }

    private ExtractedField? ExtractBudgetText(string content)
    {
        var evidence = budgetParser.ExtractEvidence(content);
        return evidence is null ? null : SourceField(evidence, evidence, 0.98m);
    }

    private static string? ParseVatStatus(string content)
    {
        if (Regex.IsMatch(content, "\\b(including|inclusive|incl\\.?)\\s+vat\\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return MasterDataCodes.VatStatuses.Registered;
        }
        return null;
    }

    private static string[] SplitValues(string? value) => string.IsNullOrWhiteSpace(value)
        ? Array.Empty<string>()
        : value.Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    private static string[] Lines(string content) =>
        content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

    private static IEnumerable<string> Sentences(string content) =>
        SentenceBreak().Split(content).Select(value => value.Trim())
            .Where(value => value.Length > 0);

    private static bool ContainsTerm(string content, string term) =>
        Regex.IsMatch(content, $"(?<![a-z0-9]){Regex.Escape(term)}(?![a-z0-9])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static string FindEvidenceExcerpt(string content, IReadOnlyList<string> terms) =>
        Sentences(content).FirstOrDefault(sentence =>
            terms.Any(term => ContainsTerm(sentence, term))) ?? FirstSentence(content);

    private static string FirstSentence(string content) =>
        Sentences(content).FirstOrDefault() ?? content.Trim();

    private static ExtractedField SourceField(string value, string excerpt, decimal confidence) =>
        new(string.Empty, value, MasterDataCodes.EvidenceClassifications.Fact,
            confidence, excerpt, SourceLocator);

    private static ExtractedField InferredField(string value, string excerpt, decimal confidence) =>
        new(string.Empty, value, MasterDataCodes.EvidenceClassifications.Inference,
            confidence, excerpt, SourceLocator);

    private static string Required(string value, int maximum, string name)
    {
        var result = value.Trim();
        if (result.Length == 0 || result.Length > maximum)
        {
            throw new ArgumentException("A valid supplied Brief value is required.", name);
        }
        return result;
    }

    [GeneratedRegex(@"\b\d{1,4}[-/]\d{1,2}(?:[-/]\d{1,4})?\b", RegexOptions.CultureInvariant)]
    private static partial Regex NumericDate();

    [GeneratedRegex(@"\b(from|between|during|by|starting|commencing|until|for\s+\d+\s+(?:day|week|month)s?)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DateRangeCue();

    [GeneratedRegex(@"(?<=[.!?])\s+|\r?\n+", RegexOptions.CultureInvariant)]
    private static partial Regex SentenceBreak();
}

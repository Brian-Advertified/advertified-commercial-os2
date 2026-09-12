using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Opportunity;

public sealed class AgentRuntimeOptions
{
    public const string SectionName = "AgentRuntime";
    public const string HttpDeterministicMode = "HttpDeterministic";
    public const string HttpMode = "Http";
    public const string DeterministicProvider = "deterministic";
    public const string BedrockProvider = "bedrock";

    private const string ModelRouteSeparator = "__";

    private static readonly string[] RequiredBedrockModelRoutes =
    [
        MasterDataCodes.AgentTypes.BusinessInterpretation,
        MasterDataCodes.AgentTypes.OpportunityIntelligence,
        MasterDataCodes.AgentTypes.Strategy,
        MasterDataCodes.AgentTypes.CriticReadiness,
        MasterDataCodes.AgentTypes.BriefDrafting,
        MasterDataCodes.AgentTypes.MarketIntelligence,
        MasterDataCodes.AgentTypes.AudienceIntelligence,
        MasterDataCodes.AgentTypes.LocationIntelligence,
        MasterDataCodes.AgentTypes.MediaStrategy,
        MasterDataCodes.AgentTypes.InventoryIntelligence,
        MasterDataCodes.AgentTypes.ProposalNarrative,
        MasterDataCodes.AgentTypes.Creative,
        MasterDataCodes.AgentTypes.Measurement,
        ModelRoute(
            MasterDataCodes.AgentTypes.BriefDrafting,
            "SUPPLIED_BRIEF_UNDERSTANDING"),
        ModelRoute(
            MasterDataCodes.AgentTypes.InventoryIntelligence,
            "SCHEMA_DISCOVERY"),
        ModelRoute(
            MasterDataCodes.AgentTypes.InventoryIntelligence,
            "SOURCE_TRANSCRIPTION"),
        ModelRoute(
            MasterDataCodes.AgentTypes.InventoryIntelligence,
            "SEMANTIC_ENRICHMENT"),
    ];

    public string Mode { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = string.Empty;
    public string? ServiceKey { get; init; }
    public int RecoverySweepSeconds { get; init; } = 300;
    public int ReconnectMinSeconds { get; init; } = 5;
    public int ReconnectMaxSeconds { get; init; } = 60;
    public string Provider { get; init; } = string.Empty;
    public string DefaultModel { get; init; } = string.Empty;
    public long DefaultCostCapMinor { get; init; }
    public int TimeoutSeconds { get; init; } = 30;
    public int MaxAttempts { get; init; } = 1;
    public bool AllowLive { get; init; }
    public Dictionary<string, string> Models { get; init; } =
        new(StringComparer.Ordinal);
    public Dictionary<string, long> CostCapsMinor { get; init; } =
        new(StringComparer.Ordinal);

    public static bool HasSafeTiming(AgentRuntimeOptions options) =>
        options.RecoverySweepSeconds is >= 30 and <= 3_600 &&
        options.ReconnectMinSeconds is >= 5 and <= 60 &&
        options.ReconnectMaxSeconds >= options.ReconnectMinSeconds &&
        options.ReconnectMaxSeconds <= 300 &&
        options.TimeoutSeconds is >= 1 and <= 120;

    public bool UsesHttp => Mode is HttpMode or HttpDeterministicMode;

    public string ModelFor(string agentCode, string? operation = null)
    {
        if (Provider == DeterministicProvider) return "fixture-v1";
        var route = ModelRoute(agentCode, operation);
        return Models.TryGetValue(route, out var model)
            ? model
            : throw new InvalidOperationException(
                $"No approved Bedrock model is configured for route '{route}'.");
    }

    public long CostCapFor(string agentCode) =>
        CostCapsMinor.TryGetValue(agentCode, out var cap)
            ? cap
            : DefaultCostCapMinor;

    public static bool HasSupportedMode(AgentRuntimeOptions options) =>
        options.Mode is HttpDeterministicMode or HttpMode;

    public static bool HasSupportedProvider(AgentRuntimeOptions options) =>
        options.Provider is DeterministicProvider or BedrockProvider;

    public static bool HasSafeProviderPolicy(AgentRuntimeOptions options)
    {
        if (options.Provider == DeterministicProvider)
        {
            return options.DefaultModel == "fixture-v1" &&
                options.DefaultCostCapMinor == 0 &&
                !options.AllowLive &&
                options.MaxAttempts == 1;
        }
        return options.Provider == BedrockProvider &&
            options.DefaultCostCapMinor > 0 &&
            options.MaxAttempts == 1 &&
            RequiredBedrockModelRoutes.All(options.Models.ContainsKey);
    }

    public static bool HasCompatibleMode(AgentRuntimeOptions options) =>
        options.Mode != HttpDeterministicMode ||
        options.Provider == DeterministicProvider;

    public static bool HasSafeRoutes(AgentRuntimeOptions options) =>
        options.Models.All(item =>
            IsSafeRouteCode(item.Key) &&
            IsSafeModel(item.Value) &&
            item.Value != "fixture-v1") &&
        options.CostCapsMinor.All(item =>
            IsSafeRouteCode(item.Key) &&
            (options.Provider == DeterministicProvider
                ? item.Value == 0
                : item.Value > 0));

    public static string ModelRoute(
        string agentCode,
        string? operation = null)
    {
        if (!IsSafeRouteCode(agentCode) ||
            operation is not null && !IsSafeRouteCode(operation))
        {
            throw new ArgumentException(
                "The agent model route is invalid.",
                nameof(agentCode));
        }
        return operation is null
            ? agentCode
            : string.Concat(
                agentCode,
                ModelRouteSeparator,
                operation.ToLowerInvariant());
    }

    private static bool IsSafeRouteCode(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 200 &&
        value.All(character =>
            char.IsLetterOrDigit(character) ||
            character is '_' or '-');

    private static bool IsSafeModel(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 300 &&
        value.All(character => !char.IsWhiteSpace(character));
}

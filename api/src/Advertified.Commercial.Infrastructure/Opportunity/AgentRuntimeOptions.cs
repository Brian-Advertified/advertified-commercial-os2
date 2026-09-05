namespace Advertified.Commercial.Infrastructure.Opportunity;

public sealed class AgentRuntimeOptions
{
    public const string SectionName = "AgentRuntime";
    public const string DisabledMode = "Disabled";
    public const string InProcessMode = "InProcessDeterministic";
    public const string HttpDeterministicMode = "HttpDeterministic";
    public const string HttpMode = "Http";
    public const string DeterministicProvider = "deterministic";
    public const string BedrockProvider = "bedrock";

    public string Mode { get; init; } = DisabledMode;
    public string BaseUrl { get; init; } = "http://localhost:8000";
    public string? ServiceKey { get; init; }
    public int RecoverySweepSeconds { get; init; } = 300;
    public int ReconnectMinSeconds { get; init; } = 5;
    public int ReconnectMaxSeconds { get; init; } = 60;
    public string Provider { get; init; } = DeterministicProvider;
    public string DefaultModel { get; init; } = "fixture-v1";
    public long DefaultCostCapMinor { get; init; }
    public int TimeoutSeconds { get; init; } = 30;
    public int MaxAttempts { get; init; } = 1;
    public bool AllowLive { get; init; }
    public Dictionary<string, string> Models { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, long> CostCapsMinor { get; init; } = new(StringComparer.Ordinal);

    public static bool HasSafeTiming(AgentRuntimeOptions options) =>
        options.RecoverySweepSeconds is >= 30 and <= 3_600 &&
        options.ReconnectMinSeconds is >= 5 and <= 60 &&
        options.ReconnectMaxSeconds >= options.ReconnectMinSeconds &&
        options.ReconnectMaxSeconds <= 300 &&
        options.TimeoutSeconds is >= 1 and <= 120;

    public bool UsesHttp => Mode is HttpMode or HttpDeterministicMode;

    public string ModelFor(string agentCode) =>
        Models.TryGetValue(agentCode, out var model) ? model : DefaultModel;

    public long CostCapFor(string agentCode) =>
        CostCapsMinor.TryGetValue(agentCode, out var cap) ? cap : DefaultCostCapMinor;

    public static bool HasSupportedMode(AgentRuntimeOptions options) =>
        options.Mode is DisabledMode or InProcessMode or HttpDeterministicMode or HttpMode;

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
            options.DefaultModel != "fixture-v1" &&
            options.DefaultCostCapMinor > 0 &&
            options.MaxAttempts == 1;
    }

    public static bool HasCompatibleMode(AgentRuntimeOptions options) =>
        options.Mode is not (InProcessMode or HttpDeterministicMode) ||
        options.Provider == DeterministicProvider;

    public static bool HasSafeRoutes(AgentRuntimeOptions options) =>
        options.Models.All(item =>
            IsSafeAgentCode(item.Key) && IsSafeModel(item.Value) &&
            (options.Provider == DeterministicProvider
                ? item.Value == "fixture-v1"
                : item.Value != "fixture-v1")) &&
        options.CostCapsMinor.All(item =>
            IsSafeAgentCode(item.Key) &&
            (options.Provider == DeterministicProvider ? item.Value == 0 : item.Value > 0));

    private static bool IsSafeAgentCode(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 100 &&
        value.All(character => char.IsLetterOrDigit(character) || character is '_' or '-');

    private static bool IsSafeModel(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 300 &&
        value.All(character => !char.IsWhiteSpace(character));
}

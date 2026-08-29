namespace Advertified.Commercial.Infrastructure.Opportunity;

public sealed class AgentRuntimeOptions
{
    public const string SectionName = "AgentRuntime";
    public const string DisabledMode = "Disabled";
    public const string InProcessMode = "InProcessDeterministic";
    public const string HttpMode = "HttpDeterministic";

    public string Mode { get; init; } = DisabledMode;
    public string BaseUrl { get; init; } = "http://localhost:8000";
    public string? ServiceKey { get; init; }
    public int PollMilliseconds { get; init; } = 100;
}

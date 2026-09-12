using Advertified.Commercial.Application.Intelligence;
using Advertified.Commercial.Infrastructure.Opportunity;

namespace Advertified.Commercial.Infrastructure.Intelligence;

internal static class IntelligenceUsage
{
    internal static IntelligenceInvocationUsage FromRuntime(
        string operationCode,
        AgentProviderUsage usage) => new(
            operationCode,
            usage.Provider,
            usage.Model,
            usage.IncrementalCostMinor,
            usage.CacheStatus,
            usage.ProviderRequestId,
            usage.InputTokens,
            usage.OutputTokens,
            usage.IncrementalCostUsdMicros);
}

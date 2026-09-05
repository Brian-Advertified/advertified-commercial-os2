using Advertified.Commercial.Application.Brief;
using Advertified.Commercial.Infrastructure.Brief;

namespace Advertified.Commercial.Api.Startup;

internal static class SuppliedBriefConfiguration
{
    // This selects an implementation, not a model route or research permission.
    internal const string ModeKey = "SuppliedBrief:Mode";
    internal const string Disabled = "Disabled";
    internal const string Deterministic = "Deterministic";

    internal static void AddSuppliedBriefInterpretation(this WebApplicationBuilder builder)
    {
        var mode = builder.Configuration[ModeKey] ?? Disabled;
        if (mode == Deterministic)
        {
            if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Test"))
                throw new InvalidOperationException("Deterministic brief interpretation is development/test only.");
            builder.Services.AddScoped<ISuppliedBriefAgentClient, DeterministicSuppliedBriefAgentClient>();
        }
        else if (mode == Disabled)
            builder.Services.AddScoped<ISuppliedBriefAgentClient, DisabledSuppliedBriefAgentClient>();
        else
            throw new InvalidOperationException("No approved supplied-brief implementation matches the configured mode.");
    }
}

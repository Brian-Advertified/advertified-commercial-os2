using Advertified.Commercial.Application.Brief;
using Advertified.Commercial.Infrastructure.Brief;
using Advertified.Commercial.Infrastructure.Opportunity;

namespace Advertified.Commercial.Api.Startup;

internal static class SuppliedBriefConfiguration
{
    internal static void AddSuppliedBriefInterpretation(this WebApplicationBuilder builder)
    {
        var runtime = builder.Configuration.GetSection(AgentRuntimeOptions.SectionName)
            .Get<AgentRuntimeOptions>() ?? new AgentRuntimeOptions();
        if (!runtime.UsesHttp)
            throw new InvalidOperationException(
                "Brief Intelligence requires the configured HTTP agent runtime boundary.");

        builder.Services.AddScoped<ISuppliedBriefInterpretationStore, SuppliedBriefInterpretationStore>();
        builder.Services.AddHttpClient<HttpSuppliedBriefAgentClient>(AgentRuntimeClientConfiguration.Configure)
            .AddHttpMessageHandler<AiMonthlyBudgetHandler>()
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        builder.Services.AddScoped<ISuppliedBriefAgentClient>(sp =>
            sp.GetRequiredService<HttpSuppliedBriefAgentClient>());
    }
}

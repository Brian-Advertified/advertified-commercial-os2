using Advertified.Commercial.Api.Background;
using Advertified.Commercial.Application.Outbox;
using Advertified.Commercial.Infrastructure.Outbox;

namespace Advertified.Commercial.Api.Startup;

public static class OutboxDispatchRegistration
{
    public static void AddOutboxDispatch(this WebApplicationBuilder builder)
    {
        var section = builder.Configuration.GetSection(OutboxDispatchOptions.SectionName);
        var configured = section.Get<OutboxDispatchOptions>() ?? new OutboxDispatchOptions();
        EnsureLocalTransportBoundary(builder.Environment, configured);

        builder.Services.AddOptions<OutboxDispatchOptions>()
            .Bind(section)
            .Validate(
                OutboxDispatchOptions.HasSupportedMode,
                "The outbox dispatch mode is invalid.")
            .Validate(
                OutboxDispatchOptions.HasSupportedTiming,
                "The outbox dispatch timing is invalid.")
            .Validate(
                OutboxDispatchOptions.HasRequiredTenant,
                "Enabled outbox dispatch requires one tenant ID.")
            .ValidateOnStart();
        builder.Services.AddSingleton<DeterministicOutboxTransport>();
        builder.Services.AddSingleton<DisabledOutboxTransport>();
        builder.Services.AddSingleton<IOutboxTransport>(services =>
            configured.Mode == OutboxDispatchOptions.DeterministicMode
                ? services.GetRequiredService<DeterministicOutboxTransport>()
                : services.GetRequiredService<DisabledOutboxTransport>());
        builder.Services.AddSingleton<OutboxDispatchMetrics>();
        builder.Services.AddSingleton<OutboxDispatchReadiness>();
        builder.Services.AddScoped<OutboxDispatchStore>();
        builder.Services.AddScoped<OutboxDispatchProcessor>();
        if (configured.IsEnabled)
        {
            builder.Services.AddHostedService<OutboxDispatchDispatcher>();
        }
    }

    private static void EnsureLocalTransportBoundary(
        IWebHostEnvironment environment,
        OutboxDispatchOptions options)
    {
        var localEnvironment = environment.IsDevelopment() ||
            environment.IsEnvironment("Test");
        if (options.Mode == OutboxDispatchOptions.DeterministicMode && !localEnvironment)
        {
            throw new InvalidOperationException(
                "Deterministic outbox dispatch is restricted to Development and Test.");
        }
    }
}

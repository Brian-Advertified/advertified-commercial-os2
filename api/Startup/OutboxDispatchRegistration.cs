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
        EnsureEnvironmentBoundary(builder.Environment, configured);

        builder.Services.AddOptions<OutboxDispatchOptions>()
            .Bind(section)
            .Validate(OutboxDispatchOptions.HasSupportedMode,
                "The outbox dispatch mode is invalid.")
            .Validate(OutboxDispatchOptions.HasSupportedTiming,
                "The outbox dispatch timing is invalid.")
            .Validate(OutboxDispatchOptions.HasValidLocalTenant,
                "The outbox dispatch tenant is invalid.")
            .Validate(OutboxDispatchOptions.HasSafeTransportConfiguration,
                "The outbox transport configuration is unsafe.")
            .ValidateOnStart();
        builder.Services.AddSingleton<DeterministicOutboxTransport>();
        builder.Services.AddSingleton<DisabledOutboxTransport>();
        builder.Services.AddSingleton<EventBridgeOutboxTransport>();
        builder.Services.AddSingleton<IOutboxTransport>(services => configured.Mode switch
        {
            OutboxDispatchOptions.DeterministicMode =>
                services.GetRequiredService<DeterministicOutboxTransport>(),
            OutboxDispatchOptions.EventBridgeMode =>
                services.GetRequiredService<EventBridgeOutboxTransport>(),
            _ => services.GetRequiredService<DisabledOutboxTransport>(),
        });
        builder.Services.AddSingleton<OutboxDispatchMetrics>();
        builder.Services.AddSingleton<OutboxDispatchReadiness>();
        builder.Services.AddScoped<OutboxDispatchStore>();
        builder.Services.AddScoped<OutboxDispatchProcessor>();

        if (configured.Mode == OutboxDispatchOptions.DeterministicMode &&
            configured.TenantId is not null)
        {
            builder.Services.AddHostedService<OutboxDispatchDispatcher>();
        }
    }

    private static void EnsureEnvironmentBoundary(
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

using Advertified.Commercial.Application.EmailAutomation;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Infrastructure.EmailAutomation;

namespace Advertified.Commercial.Api;

internal static class EmailAutomationRegistration
{
    internal static void AddEmailAutomation(
        this WebApplicationBuilder builder,
        EmailAutomationOptions settings)
    {
        EnsureEnvironmentIsSafe(builder, settings);
        builder.Services.AddOptions<EmailAutomationOptions>()
            .Bind(builder.Configuration.GetSection(EmailAutomationOptions.SectionName))
            .Validate(EmailAutomationOptions.IsSupported,
                "The inbound email automation mode is invalid.")
            .Validate(EmailAutomationOptions.HasValidTolerance,
                "The inbound email webhook tolerance is invalid.")
            .Validate(EmailAutomationOptions.HasProviderConfiguration,
                "Resend inbound email automation requires HTTPS API, key, webhook secret and sender configuration.")
            .ValidateOnStart();
        builder.Services.AddSingleton(EmailAutomationPolicy.Load());
        builder.Services.AddScoped<EmailAutomationRecordStore>();
        builder.Services.AddScoped<ProposalEmailIntentStore>();
        builder.Services.AddScoped<DurableProposalEmailDelivery>();
        builder.Services.AddScoped<IEmailAutomationReader, EmailAutomationReader>();
        builder.Services.AddScoped<IEmailAutomationCommands, EmailAutomationCommands>();
        builder.Services.AddScoped<IAutomationCommandEnvelopeFactory,
            AutomationCommandEnvelopeFactory>();
        builder.Services.AddScoped<IStpReadinessEvaluator, StpReadinessEvaluator>();
        builder.Services.AddScoped<IEmailAutomationInventorySelector,
            EmailAutomationInventorySelector>();
        builder.Services.AddScoped<IEmailProposalAutomationProcessor,
            EmailProposalAutomationProcessor>();
        builder.Services.AddScoped<IInboundEmailReceiver, InboundEmailReceiver>();
        builder.Services.AddSingleton<DeterministicEmailProviderClient>();
        builder.Services.AddSingleton<IEmailProviderClient>(serviceProvider =>
            serviceProvider.GetRequiredService<DeterministicEmailProviderClient>());
        builder.Services.AddHttpClient<ResendEmailProviderClient>(ConfigureResendClient);
        builder.Services.AddTransient<IEmailProviderClient>(serviceProvider =>
            serviceProvider.GetRequiredService<ResendEmailProviderClient>());
        builder.Services.AddScoped<IEmailProviderResolver, EmailProviderResolver>();
    }

    private static void EnsureEnvironmentIsSafe(
        WebApplicationBuilder builder,
        EmailAutomationOptions settings)
    {
        if (settings.Mode == EmailAutomationOptions.DeterministicMode &&
            !builder.Environment.IsDevelopment() &&
            !builder.Environment.IsEnvironment("Test"))
        {
            throw new InvalidOperationException(
                "Deterministic inbound email automation is restricted to development and test.");
        }
    }

    private static void ConfigureResendClient(
        IServiceProvider serviceProvider,
        HttpClient client)
    {
        var settings = serviceProvider.GetRequiredService<
            Microsoft.Extensions.Options.IOptions<EmailAutomationOptions>>().Value;
        client.BaseAddress = new Uri(settings.ResendApiBaseUrl, UriKind.Absolute);
    }
}

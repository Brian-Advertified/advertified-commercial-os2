using Advertified.Commercial.Api;
using Advertified.Commercial.Api.Authentication;
using Advertified.Commercial.Api.Background;
using Advertified.Commercial.Api.Endpoints;
using Advertified.Commercial.Api.Errors;
using Advertified.Commercial.Api.OpenApi;
using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Identity;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Application.Security;
using Advertified.Commercial.Infrastructure.Foundation;
using Advertified.Commercial.Infrastructure.Identity;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;
using Advertified.Commercial.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);
var authenticationMode = builder.Configuration["Authentication:Mode"];
var agentRuntime = builder.Configuration
    .GetSection(AgentRuntimeOptions.SectionName)
    .Get<AgentRuntimeOptions>() ?? new AgentRuntimeOptions();

if ((authenticationMode is LocalIdentityDefaults.DeterministicMode
        or LocalIdentityDefaults.DeterministicSessionMode)
    && !builder.Environment.IsDevelopment()
    && !builder.Environment.IsEnvironment("Test"))
{
    throw new InvalidOperationException(
        "Deterministic authentication and sessions are restricted to development and test.");
}

if (agentRuntime.Mode != AgentRuntimeOptions.DisabledMode &&
    !builder.Environment.IsDevelopment() &&
    !builder.Environment.IsEnvironment("Test"))
{
    throw new InvalidOperationException(
        "The Gate 4 deterministic agent runtime is restricted to development and test.");
}

var connectionString = builder.Configuration.GetConnectionString("CommercialDatabase");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("The commercial database connection is not configured.");
}

builder.Services.AddDbContext<GovernanceDbContext>(
    options => options.UseNpgsql(connectionString));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IBrowserSessionStore, InMemoryBrowserSessionStore>();
builder.Services.AddScoped<IIdentityWorkspaceReader, IdentityWorkspaceReader>();
builder.Services.AddScoped<ITenantMembershipSource, DatabaseTenantMembershipSource>();
builder.Services.AddScoped<ITenantAuthorizer, TenantAuthorizer>();
builder.Services.AddScoped<CommandDispatcher>();
builder.Services.AddScoped<IIdempotentCommandUnitOfWork, PersistedCommandUnitOfWork>();
builder.Services.AddScoped<ICommercialFoundationReader, CommercialFoundationReader>();
builder.Services.AddScoped<IIdentityFoundationCommands, IdentityFoundationCommands>();
builder.Services.AddScoped<IBusinessFoundationCommands, BusinessFoundationCommands>();
builder.Services.AddScoped<OpportunityRecordStore>();
builder.Services.AddScoped<OpportunityRunStore>();
builder.Services.AddScoped<OpportunityRunProcessor>();
builder.Services.AddScoped<IOpportunityReader, OpportunityReader>();
builder.Services.AddScoped<IOpportunityCommands, OpportunityCommands>();
builder.Services.AddScoped<IOpportunityWorkflowCommands, OpportunityWorkflowCommands>();
builder.Services.AddOptions<AgentRuntimeOptions>()
    .Bind(builder.Configuration.GetSection(AgentRuntimeOptions.SectionName))
    .Validate(
        options => options.Mode is AgentRuntimeOptions.DisabledMode
            or AgentRuntimeOptions.InProcessMode
            or AgentRuntimeOptions.HttpMode,
        "The agent runtime mode is invalid.")
    .Validate(
        options => options.PollMilliseconds is >= 25 and <= 5_000,
        "The agent runtime poll interval must be between 25 and 5000 milliseconds.")
    .Validate(
        options => options.Mode != AgentRuntimeOptions.HttpMode ||
            (Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _) &&
             !string.IsNullOrWhiteSpace(options.ServiceKey)),
        "The HTTP agent runtime requires an absolute URL and service key.")
    .ValidateOnStart();
builder.Services.AddHttpClient<HttpOpportunityAgentClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<
        Microsoft.Extensions.Options.IOptions<AgentRuntimeOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
});
builder.Services.AddScoped<IOpportunityAgentClient>(serviceProvider =>
    agentRuntime.Mode switch
    {
        AgentRuntimeOptions.InProcessMode =>
            ActivatorUtilities.CreateInstance<InProcessOpportunityAgentClient>(serviceProvider),
        AgentRuntimeOptions.HttpMode =>
            serviceProvider.GetRequiredService<HttpOpportunityAgentClient>(),
        _ => ActivatorUtilities.CreateInstance<InProcessOpportunityAgentClient>(serviceProvider),
    });
if (agentRuntime.Mode != AgentRuntimeOptions.DisabledMode)
{
    builder.Services.AddHostedService<OpportunityRunDispatcher>();
}
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentIdentity, ClaimsCurrentIdentity>();
builder.Services.AddScoped<BrowserRequestGuard>();
builder.Services.AddOptions<BrowserSessionOptions>()
    .Bind(builder.Configuration.GetSection(BrowserSessionOptions.SectionName))
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.CookieName),
        "The browser session cookie name is required.")
    .Validate(
        options => options.LifetimeMinutes is >= 5 and <= 1440,
        "The browser session lifetime must be between 5 and 1440 minutes.")
    .ValidateOnStart();

var sessionSettings = builder.Configuration
    .GetSection(BrowserSessionOptions.SectionName)
    .Get<BrowserSessionOptions>() ?? new BrowserSessionOptions();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = BrowserSessionOptions.AntiforgeryHeaderName;
    options.Cookie.Name = sessionSettings.AntiforgeryCookieName;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = sessionSettings.SecureCookie
        ? CookieSecurePolicy.Always
        : CookieSecurePolicy.None;
});

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = LocalIdentityDefaults.CompositeScheme;
        options.DefaultChallengeScheme = LocalIdentityDefaults.CompositeScheme;
    })
    .AddPolicyScheme(
        LocalIdentityDefaults.CompositeScheme,
        displayName: null,
        options => options.ForwardDefaultSelector = context =>
            string.Equals(
                context.RequestServices.GetRequiredService<IConfiguration>()
                    ["Authentication:Mode"],
                LocalIdentityDefaults.DeterministicSessionMode,
                StringComparison.Ordinal)
                ? BrowserSessionAuthenticationHandler.AuthenticationScheme
                : LocalIdentityDefaults.Scheme)
    .AddScheme<AuthenticationSchemeOptions, DevelopmentIdentityHandler>(
        LocalIdentityDefaults.Scheme,
        _ => { })
    .AddScheme<AuthenticationSchemeOptions, BrowserSessionAuthenticationHandler>(
        BrowserSessionAuthenticationHandler.AuthenticationScheme,
        _ => { });
builder.Services.AddAuthorization();

builder.Services.AddExceptionHandler<HumanSafeExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Advertified Commercial API",
        Version = "1.0.0",
    });
    options.OperationFilter<HttpContractOperationFilter>();
});

var app = builder.Build();

app.UseMiddleware<CorrelationMiddleware>();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseMiddleware<BrowserSessionProtectionMiddleware>();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Advertified API v1"));
}

app.MapGet("/", () => Results.Ok(new ServiceDescription(
    "Advertified Commercial API",
    "gate-4-evidence-opportunity",
    "Tenant-safe commercial operations with a local browser-session boundary.")))
    .WithTags("Service");

app.MapGet("/health/live", () => Results.Ok(new HealthResponse(
    "healthy",
    "advertified-commercial-api",
    ["process"])))
    .WithTags("Health");

app.MapGet("/health/ready", () => Results.Ok(new HealthResponse(
    "ready",
    "advertified-commercial-api",
    ["process"])))
    .WithTags("Health");

app.MapBrowserSessionEndpoints();
app.MapIdentityEndpoints();
app.MapFoundationEndpoints();
app.MapOpportunityEndpoints();

app.Run();

public partial class Program;

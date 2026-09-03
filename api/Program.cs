using Advertified.Commercial.Api;
using Advertified.Commercial.Api.Authentication;
using Advertified.Commercial.Api.Background;
using Advertified.Commercial.Api.Endpoints;
using Advertified.Commercial.Api.Errors;
using Advertified.Commercial.Api.OpenApi;
using Advertified.Commercial.Api.Observability;
using Advertified.Commercial.Api.Startup;
using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Booking;
using Advertified.Commercial.Application.AgentOperations;
using Advertified.Commercial.Application.Campaign;
using Advertified.Commercial.Application.CommercialSettings;
using Advertified.Commercial.Application.Creative;
using Advertified.Commercial.Application.Delivery;
using Advertified.Commercial.Application.Brief;
using Advertified.Commercial.Application.EmailAutomation;
using Advertified.Commercial.Application.Identity;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Funding;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Application.Security;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Application.Marketplace;
using Advertified.Commercial.Application.Measurement;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Application.Proposal;
using Advertified.Commercial.Infrastructure.Foundation;
using Advertified.Commercial.Infrastructure.Funding;
using Advertified.Commercial.Infrastructure.Booking;
using Advertified.Commercial.Infrastructure.AgentOperations;
using Advertified.Commercial.Infrastructure.Campaign;
using Advertified.Commercial.Infrastructure.CommercialSettings;
using Advertified.Commercial.Infrastructure.Creative;
using Advertified.Commercial.Infrastructure.Delivery;
using Advertified.Commercial.Infrastructure.Brief;
using Advertified.Commercial.Infrastructure.EmailAutomation;
using Advertified.Commercial.Infrastructure.Identity;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;
using Advertified.Commercial.Infrastructure.Persistence;
using Advertified.Commercial.Infrastructure.Inventory;
using Advertified.Commercial.Infrastructure.Marketplace;
using Advertified.Commercial.Infrastructure.Measurement;
using Advertified.Commercial.Infrastructure.Planning;
using Advertified.Commercial.Infrastructure.Proposal;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Minio;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.Limits.MaxRequestBodySize =
        InventoryProtectionOptions.MaximumSupportedSourceBytes + 1_048_576;
});
var processRole = builder.AddAdvertifiedProcessRole();
var authenticationMode = builder.Configuration["Authentication:Mode"];
var agentRuntime = builder.Configuration
    .GetSection(AgentRuntimeOptions.SectionName)
    .Get<AgentRuntimeOptions>() ?? new AgentRuntimeOptions();
var inventoryProtection = builder.Configuration
    .GetSection(InventoryProtectionOptions.SectionName)
    .Get<InventoryProtectionOptions>() ?? new InventoryProtectionOptions();
var inventoryExtraction = builder.Configuration
    .GetSection(InventoryExtractionOptions.SectionName)
    .Get<InventoryExtractionOptions>() ?? new InventoryExtractionOptions();
var emailAutomation = builder.Configuration
    .GetSection(EmailAutomationOptions.SectionName)
    .Get<EmailAutomationOptions>() ?? new EmailAutomationOptions();

var connectionString = StartupConfigurationValidator.ValidateAndGetConnectionString(
    builder,
    authenticationMode,
    agentRuntime,
    inventoryProtection);

builder.Services.AddDbContext<GovernanceDbContext>(
    options => options.UseNpgsql(connectionString));
builder.AddOutboxDispatch();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IBrowserSessionStore, PostgresBrowserSessionStore>();
builder.Services.AddScoped<AgentOperationsStore>();
builder.Services.AddScoped<IAgentOperationsReader, AgentOperationsReader>();
builder.Services.AddScoped<OidcIdentityResolver>();
builder.Services.AddScoped<IIdentityWorkspaceReader, IdentityWorkspaceReader>();
builder.Services.AddScoped<ITenantMembershipSource, DatabaseTenantMembershipSource>();
builder.Services.AddScoped<ITenantAuthorizer, TenantAuthorizer>();
builder.Services.AddScoped<CommandDispatcher>();
builder.Services.AddScoped<IIdempotentCommandUnitOfWork, PersistedCommandUnitOfWork>();
builder.Services.AddScoped<ICommercialFoundationReader, CommercialFoundationReader>();
builder.Services.AddScoped<CommercialPolicyRecordStore>();
builder.Services.AddScoped<ICommercialPolicyReader, CommercialPolicyReader>();
builder.Services.AddScoped<ICommercialPolicyCommands, CommercialPolicyCommands>();
builder.Services.AddScoped<BookingRecordStore>();
builder.Services.AddScoped<IBookingReader, BookingReader>();
builder.Services.AddScoped<IBookingCommands, BookingCommands>();
builder.Services.AddScoped<CampaignRecordStore>();
builder.Services.AddScoped<ICampaignReader, CampaignReader>();
builder.Services.AddScoped<ICampaignCommands, CampaignCommands>();
builder.Services.AddScoped<CreativeRecordStore>();
builder.Services.AddScoped<ICreativeReader, CreativeReader>();
builder.Services.AddScoped<ICreativeCommands, CreativeCommands>();
builder.Services.AddScoped<DeliveryProofRecordStore>();
builder.Services.AddScoped<IDeliveryProofReader, DeliveryProofReader>();
builder.Services.AddScoped<IDeliveryProofCommands, DeliveryProofCommands>();
builder.Services.AddScoped<PerformanceEvidenceRecordStore>();
builder.Services.AddScoped<IPerformanceEvidenceReader, PerformanceEvidenceReader>();
builder.Services.AddScoped<IPerformanceEvidenceCommands, PerformanceEvidenceCommands>();
builder.Services.AddScoped<MeasurementReportRecordStore>();
builder.Services.AddScoped<IMeasurementReportReader, MeasurementReportReader>();
builder.Services.AddScoped<IMeasurementReportCommands, MeasurementReportCommands>();
builder.Services.AddScoped<DeterministicMeasurementAgentClient>();
builder.Services.AddScoped<FundingRecordStore>();
builder.Services.AddScoped<IFundingReader, FundingReader>();
builder.Services.AddScoped<IFundingCommands, FundingCommands>();
builder.Services.AddScoped<IIdentityFoundationCommands, IdentityFoundationCommands>();
builder.Services.AddScoped<IBusinessFoundationCommands, BusinessFoundationCommands>();
builder.Services.AddScoped<OpportunityRecordStore>();
builder.Services.AddScoped<OpportunityRunStore>();
builder.Services.AddScoped<OpportunityRunProcessor>();
builder.Services.AddScoped<IOpportunityReader, OpportunityReader>();
builder.Services.AddScoped<IOpportunityCommands, OpportunityCommands>();
builder.Services.AddScoped<IOpportunityWorkflowCommands, OpportunityWorkflowCommands>();
builder.Services.AddScoped<BriefRecordStore>();
builder.Services.AddScoped<BriefClientResolver>();
builder.Services.AddScoped<IBriefReader, BriefReader>();
builder.Services.AddScoped<IBriefCommands, BriefCommands>();
builder.Services.AddSingleton(SuppliedBriefAgentPolicy.Load());
builder.Services.AddScoped<ISuppliedBriefAgentClient, DeterministicSuppliedBriefAgentClient>();
builder.Services.AddScoped<ISuppliedBriefUnderstandingService, SuppliedBriefUnderstandingService>();
builder.Services.AddScoped<InventoryRecordStore>();
builder.Services.AddScoped<InventoryExtractionAttemptStore>();
builder.Services.AddScoped<InventoryExtractionCompletionService>();
builder.Services.AddScoped<InventoryExtractionAttemptProcessor>();
builder.Services.AddScoped<IInventoryReader, InventoryReader>();
builder.Services.AddScoped<IInventoryCommands, InventoryCommands>();
builder.Services.AddSingleton(InventoryDuplicatePolicy.Load());
builder.AddInventoryEmbeddings();
builder.Services.AddScoped<MarketplaceRecordStore>();
builder.Services.AddScoped<IMarketplaceReader, MarketplaceReader>();
builder.Services.AddScoped<IMarketplaceCommands, MarketplaceCommands>();
builder.Services.AddScoped<PlanningRecordStore>();
builder.Services.AddScoped<IPlanningReader, PlanningReader>();
builder.Services.AddScoped<IInventoryBenchmarkReader, InventoryBenchmarkReader>();
builder.Services.AddSingleton(PlanningPolicy.Load());
builder.Services.AddSingleton(CampaignModePolicy.Load());
builder.Services.AddScoped<IPlanningCommands, PlanningCommands>();
builder.Services.AddScoped<DeterministicPlanningAgentClient>();
builder.Services.AddScoped<ProposalRecordStore>();
builder.Services.AddSingleton(ProposalPolicy.Load());
builder.Services.AddScoped<IProposalReader, ProposalReader>();
builder.Services.AddScoped<IProposalCommands, ProposalCommands>();
builder.Services.AddScoped<DeterministicProposalNarrativeClient>();
builder.Services.AddScoped<IProposalDeliveryClient, DeterministicProposalDeliveryClient>();
builder.AddEmailAutomation(emailAutomation);
builder.AddCommercialWorkers(processRole);
builder.AddInventoryExtraction(inventoryExtraction);
builder.Services.Configure<FormOptions>(options =>
    options.MultipartBodyLengthLimit = inventoryProtection.MaximumSourceBytes + 1_048_576);
builder.Services.AddOptions<InventoryProtectionOptions>()
    .Bind(builder.Configuration.GetSection(InventoryProtectionOptions.SectionName))
    .Validate(InventoryProtectionOptions.HasSupportedObjectStore,
        "The inventory object store mode is invalid.")
    .Validate(InventoryProtectionOptions.HasSupportedScanner,
        "The inventory scanner mode is invalid.")
    .Validate(InventoryProtectionOptions.HasSupportedSourceLimit,
        "The inventory source limit must be between 1 byte and 100 MiB.")
    .Validate(InventoryProtectionOptions.HasCompleteMinioConfiguration,
        "MinIO inventory protection requires an endpoint and credentials.")
    .Validate(InventoryProtectionOptions.HasCompleteClamAvConfiguration,
        "ClamAV inventory protection requires a valid host and port.")
    .ValidateOnStart();
builder.Services.AddSingleton<IMinioClient>(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<
        Microsoft.Extensions.Options.IOptions<InventoryProtectionOptions>>().Value;
    var client = new MinioClient().WithEndpoint(options.Endpoint)
        .WithCredentials(options.AccessKey, options.SecretKey);
    return (options.UseTls ? client.WithSSL() : client).Build();
});
builder.Services.AddSingleton<IInventoryObjectStore>(serviceProvider =>
    inventoryProtection.ObjectStoreMode == InventoryProtectionOptions.MinioMode
        ? ActivatorUtilities.CreateInstance<MinioInventoryObjectStore>(serviceProvider)
        : new InMemoryInventoryObjectStore());
builder.Services.AddSingleton<IInventoryMalwareScanner>(serviceProvider =>
    inventoryProtection.ScannerMode == InventoryProtectionOptions.ClamAvScanner
        ? ActivatorUtilities.CreateInstance<ClamAvInventoryMalwareScanner>(serviceProvider)
        : new DeterministicInventoryMalwareScanner());
builder.Services.AddOptions<AgentRuntimeOptions>()
    .Bind(builder.Configuration.GetSection(AgentRuntimeOptions.SectionName))
    .Validate(AgentRuntimeOptions.HasSupportedMode,
        "The agent runtime mode is invalid.")
    .Validate(AgentRuntimeOptions.HasSupportedProvider,
        "The agent runtime provider is invalid.")
    .Validate(AgentRuntimeOptions.HasCompatibleMode,
        "The agent runtime mode and provider are incompatible.")
    .Validate(AgentRuntimeOptions.HasSafeProviderPolicy,
        "The agent runtime provider policy is unsafe.")
    .Validate(AgentRuntimeOptions.HasSafeRoutes,
        "The agent runtime model or cost route is invalid.")
    .Validate(
        options => options.PollMilliseconds is >= 25 and <= 5_000 &&
            options.TimeoutSeconds is >= 1 and <= 120,
        "The agent runtime timing configuration is invalid.")
    .Validate(
        options => !options.UsesHttp ||
            (Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _) &&
             !string.IsNullOrWhiteSpace(options.ServiceKey)),
        "The HTTP agent runtime requires an absolute URL and service key.")
    .ValidateOnStart();
builder.Services.AddHttpClient<HttpOpportunityAgentClient>(ConfigureAgentRuntimeHttpClient);
builder.Services.AddHttpClient<HttpPlanningAgentClient>(ConfigureAgentRuntimeHttpClient);
builder.Services.AddHttpClient<HttpProposalNarrativeClient>(ConfigureAgentRuntimeHttpClient);
builder.Services.AddHttpClient<HttpMeasurementAgentClient>(ConfigureAgentRuntimeHttpClient);
builder.Services.AddScoped<IOpportunityAgentClient>(serviceProvider =>
    agentRuntime.UsesHttp
        ? serviceProvider.GetRequiredService<HttpOpportunityAgentClient>()
        : ActivatorUtilities.CreateInstance<InProcessOpportunityAgentClient>(serviceProvider));
builder.Services.AddScoped<IPlanningAgentClient>(serviceProvider =>
    agentRuntime.UsesHttp
        ? serviceProvider.GetRequiredService<HttpPlanningAgentClient>()
        : serviceProvider.GetRequiredService<DeterministicPlanningAgentClient>());
builder.Services.AddScoped<IProposalNarrativeClient>(serviceProvider =>
    agentRuntime.UsesHttp
        ? serviceProvider.GetRequiredService<HttpProposalNarrativeClient>()
        : serviceProvider.GetRequiredService<DeterministicProposalNarrativeClient>());
builder.Services.AddScoped<IMeasurementAgentClient>(serviceProvider =>
    agentRuntime.UsesHttp
        ? serviceProvider.GetRequiredService<HttpMeasurementAgentClient>()
        : serviceProvider.GetRequiredService<DeterministicMeasurementAgentClient>());
if (agentRuntime.Mode != AgentRuntimeOptions.DisabledMode && processRole.RunsWorkers)
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

var authentication = builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = LocalIdentityDefaults.CompositeScheme;
        options.DefaultChallengeScheme = LocalIdentityDefaults.CompositeScheme;
    })
    .AddPolicyScheme(
        LocalIdentityDefaults.CompositeScheme,
        displayName: null,
        options => options.ForwardDefaultSelector = context =>
        {
            var mode = context.RequestServices.GetRequiredService<IConfiguration>()
                ["Authentication:Mode"];
            return mode is LocalIdentityDefaults.DeterministicSessionMode or
                    LocalIdentityDefaults.OidcMode
                ? BrowserSessionAuthenticationHandler.AuthenticationScheme
                : LocalIdentityDefaults.Scheme;
        })
    .AddScheme<AuthenticationSchemeOptions, DevelopmentIdentityHandler>(
        LocalIdentityDefaults.Scheme,
        _ => { })
    .AddScheme<AuthenticationSchemeOptions, BrowserSessionAuthenticationHandler>(
        BrowserSessionAuthenticationHandler.AuthenticationScheme,
        _ => { });
builder.AddAdvertifiedOidc(authentication, authenticationMode);
builder.Services.AddAuthorization();
builder.Services.AddTrustedProxyHeaders(builder.Configuration);
builder.Services.AddAdvertifiedRateLimits();
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
});

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

app.UseForwardedHeaders();
app.UseMiddleware<CorrelationMiddleware>();
app.UseMiddleware<RequestCompletionTelemetryMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Test"))
{
    app.UseHsts();
}
app.UseExceptionHandler();
app.UseAuthentication();
app.UseRateLimiter();
app.UseMiddleware<BrowserSessionProtectionMiddleware>();
app.UseAuthorization();

if (app.Environment.IsDevelopment() && processRole.RunsApi)
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Advertified API v1"));
}

if (processRole.RunsApi)
{
    app.MapGet("/", () => Results.Ok(new ServiceDescription(
        "Advertified Commercial API",
        "inventory-and-planning",
        "Tenant-safe commercial operations with a secure server-side session boundary.")))
        .WithTags("Service");
    app.MapHealthEndpoints();
    app.MapBrowserSessionEndpoints();
    app.MapAgentOperationsEndpoints();
    app.MapIdentityEndpoints();
    app.MapFoundationEndpoints();
    app.MapOpportunityEndpoints();
    app.MapBriefEndpoints();
    app.MapCommercialPolicyEndpoints();
    app.MapBookingEndpoints();
    app.MapCampaignEndpoints();
    app.MapCreativeEndpoints();
    app.MapDeliveryEndpoints();
    app.MapMeasurementEndpoints();
    app.MapFundingEndpoints();
    app.MapInventoryEndpoints();
    app.MapMarketplaceEndpoints();
    app.MapPlanningEndpoints();
    app.MapProposalEndpoints();
    app.MapEmailAutomationEndpoints();
}
else
{
    app.MapWorkerHealthEndpoints();
}

static void ConfigureAgentRuntimeHttpClient(
    IServiceProvider serviceProvider,
    HttpClient client)
{
    var options = serviceProvider.GetRequiredService<
        Microsoft.Extensions.Options.IOptions<AgentRuntimeOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
}

app.Run();

public partial class Program;

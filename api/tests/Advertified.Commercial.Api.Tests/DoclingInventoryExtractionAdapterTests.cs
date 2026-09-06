using System.Net;
using System.Text;
using System.Text.Json;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Infrastructure.Inventory;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class DoclingInventoryExtractionAdapterTests
{
    private static readonly string[] ProjectionWarnings =
    [
        "Picture regions were retained without pixel reinterpretation.",
    ];

    [Theory]
    [InlineData("http://docling.test")]
    [InlineData("https://identity@docling.test")]
    public void ProductionStartupRejectsUnsafeDoclingTransport(string baseUrl)
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                ConfigureClosedProduction(builder);
                builder.UseSetting("InventoryExtraction:Mode", "Docling");
                builder.UseSetting("InventoryExtraction:BaseUrl", baseUrl);
                builder.UseSetting(
                    "InventoryExtraction:ApiKey", "closed-test-key");
            });

        var exception =
            Assert.Throws<InvalidOperationException>(factory.CreateClient);

        Assert.Contains(
            "Docling document extraction must use an HTTPS URL with a host",
            exception.ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task TestStartupAllowsLocalHttpDoclingTransport()
    {
        await using var factory =
            CreateDoclingTestFactory("http://docling.test");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ResultUsesAuthenticatedVersionedPythonProjection()
    {
        const string providerJson =
            "{\"texts\":[],\"tables\":[{\"data\":{\"table_cells\":[]}}]}";
        var doclingResponse = JsonSerializer.Serialize(new
        {
            status = "success",
            document = new { json_content = providerJson },
        });
        using var docling = Client(_ => Json(doclingResponse), "docling.test");
        using var python = Client(request =>
        {
            Assert.Equal(
                "/v1/inventory-extraction/project",
                request.RequestUri!.AbsolutePath);
            Assert.Equal(
                "projection-key",
                request.Headers.GetValues(
                    "X-Advertified-Service-Key").Single());
            var body = request.Content!.ReadAsStringAsync().GetAwaiter()
                .GetResult();
            Assert.Contains("\"providerDocument\"", body,
                StringComparison.Ordinal);
            Assert.DoesNotContain("sourceHash", body,
                StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("documentClass", body,
                StringComparison.OrdinalIgnoreCase);
            return Json(JsonSerializer.Serialize(new
            {
                schemaVersion =
                    PythonInventoryProjectionClient.SchemaVersion,
                projectorVersion =
                    PythonInventoryProjectionClient.ProjectorVersion,
                rows = new[]
                {
                    new
                    {
                        number = 1,
                        locator =
                            "docling:page=1;table=1;row=2;cell=1",
                        values = new Dictionary<string, string>
                        {
                            ["productcode"] = "SITE-1",
                            ["rate"] = "R 90 000",
                        },
                        extractionMethod = "TABULAR",
                        fieldLocators =
                            new Dictionary<string, string>
                            {
                                ["productcode"] =
                                    "docling:page=1;table=1;row=2;cell=1",
                            },
                    },
                },
                sourceElements = new[]
                {
                    new
                    {
                        locator =
                            "docling:page=1;table=1;row=2;cell=1",
                        structureId = "docling:page=1;table=1",
                        structureKind = "table",
                        row = 2,
                        column = 1,
                        rawValue = "SITE-1",
                    },
                },
                warnings = ProjectionWarnings,
            }));
        }, "agent.test");
        var adapter = new DoclingInventoryExtractionAdapter(
            docling,
            Options.Create(DoclingOptions()),
            new PythonInventoryProjectionClient(
                python, Options.Create(new AgentRuntimeOptions
                {
                    Mode = AgentRuntimeOptions.HttpDeterministicMode,
                    ServiceKey = "projection-key",
                })));

        var result = await adapter.ReadResultAsync(
            Request(), "retained-result", CancellationToken.None);

        var row = Assert.Single(result.Rows);
        Assert.Equal("SITE-1", row.Values["productcode"]);
        Assert.Equal(PythonInventoryProjectionClient.SchemaVersion,
            result.SchemaVersion);
        Assert.Single(result.Document.SourceElements!);
        Assert.Null(result.Document.SchemaDiscoveryFailure);
        Assert.Single(result.Document.ProjectionWarnings!);
        Assert.Equal(providerJson, result.ProviderJson);
    }

    [Fact]
    public async Task MissingTaskIsAnExplicitTerminalPollResult()
    {
        using var docling = Client(
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            "docling.test");
        using var python = Client(
            _ => throw new InvalidOperationException(), "agent.test");
        var adapter = new DoclingInventoryExtractionAdapter(
            docling,
            Options.Create(DoclingOptions()),
            new PythonInventoryProjectionClient(
                python, Options.Create(new AgentRuntimeOptions())));

        var result = await adapter.PollAsync(
            Guid.NewGuid().ToString(), CancellationToken.None);

        Assert.Equal(InventoryProviderTaskState.Failed, result.State);
        Assert.Equal("task_not_found", result.ProviderResponseCode);
        Assert.Equal("DOCLING_TASK_NOT_FOUND", result.ProviderErrorCode);
    }

    [Fact]
    public async Task RegisteredDoclingClientDoesNotFollowRedirect()
    {
        var redirectedRequests = 0;
        await using var target = await StartLoopbackServerAsync(app =>
            app.MapPost("/redirected", () =>
            {
                Interlocked.Increment(ref redirectedRequests);
                return Results.Ok();
            }));
        var targetAddress = GetServerAddress(target);
        await using var source = await StartLoopbackServerAsync(app =>
            app.MapPost("/v1/convert/file/async", (HttpContext context) =>
            {
                context.Response.StatusCode =
                    StatusCodes.Status307TemporaryRedirect;
                context.Response.Headers.Location =
                    $"{targetAddress}/redirected";
                return Task.CompletedTask;
            }));
        await using var factory =
            CreateDoclingTestFactory(GetServerAddress(source));
        using var scope = factory.Services.CreateScope();
        var adapter = scope.ServiceProvider
            .GetRequiredService<DoclingInventoryExtractionAdapter>();

        await Assert.ThrowsAsync<InventoryExtractionUnavailableException>(
            () => adapter.ExtractAsync(Request(), CancellationToken.None));

        Assert.Equal(0, Volatile.Read(ref redirectedRequests));
    }

    private static InventoryExtractionRequest Request() => new(
        "rates.pdf", "application/pdf", "PDF",
        new string('a', 64), [1, 2, 3]);

    private static InventoryExtractionOptions DoclingOptions() => new()
    {
        Mode = InventoryExtractionOptions.DoclingMode,
        BaseUrl = "http://docling.test",
        ApiKey = "local-contract-key",
    };

    private static HttpClient Client(
        Func<HttpRequestMessage, HttpResponseMessage> send,
        string host) => new(new StubHandler(send))
    {
        BaseAddress = new Uri($"http://{host}"),
    };

    private static HttpResponseMessage Json(string body) => new(
        HttpStatusCode.OK)
    {
        Content = new StringContent(
            body, Encoding.UTF8, "application/json"),
    };

    private static WebApplicationFactory<Program>
        CreateDoclingTestFactory(string baseUrl) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Test");
            builder.UseSetting(
                "ConnectionStrings:CommercialDatabase",
                "Host=localhost;Database=closed;Username=closed");
            builder.UseSetting("InventoryExtraction:Mode", "Docling");
            builder.UseSetting("InventoryExtraction:BaseUrl", baseUrl);
            builder.UseSetting(
                "InventoryExtraction:ApiKey", "redirect-test-key");
        });

    private static void ConfigureClosedProduction(
        IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting(
            "ConnectionStrings:CommercialDatabase",
            "Host=localhost;Database=closed;Username=closed");
        builder.UseSetting("Authentication:Mode", "Disabled");
        builder.UseSetting("AgentRuntime:Mode", "Disabled");
        builder.UseSetting(
            "AllowedHosts", "api.advertified.example");
        builder.UseSetting(
            "ReverseProxy:KnownProxies:0", "127.0.0.1");
        builder.UseSetting(
            "InventoryProtection:ObjectStoreMode", "Minio");
        builder.UseSetting(
            "InventoryProtection:ScannerMode", "ClamAv");
        builder.UseSetting(
            "InventoryProtection:Endpoint", "localhost:9000");
        builder.UseSetting(
            "InventoryProtection:AccessKey", "closed-test-access");
        builder.UseSetting(
            "InventoryProtection:SecretKey", "closed-test-secret");
        builder.UseSetting(
            "InventoryProtection:UseTls", "true");
        builder.UseSetting(
            "InventoryProtection:ClamAvHost", "localhost");
    }

    private static async Task<WebApplication>
        StartLoopbackServerAsync(
            Action<WebApplication> configure)
    {
        var builder = WebApplication.CreateSlimBuilder(
            new WebApplicationOptions
            {
                EnvironmentName = "Test",
            });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options =>
            options.Listen(IPAddress.Loopback, 0));
        var app = builder.Build();
        configure(app);
        await app.StartAsync();
        return app;
    }

    private static string GetServerAddress(WebApplication app)
    {
        var addresses = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>();
        return Assert.Single(addresses!.Addresses);
    }

    private sealed class StubHandler(
        Func<HttpRequestMessage, HttpResponseMessage> send) :
        HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(send(request));
    }
}

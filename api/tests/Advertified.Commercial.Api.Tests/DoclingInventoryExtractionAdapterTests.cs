using System.Net;
using System.Text;
using System.Text.Json;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Infrastructure.Inventory;
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
    [Theory]
    [InlineData("http://docling.test")]
    [InlineData("https://identity@docling.test")]
    public void ProductionStartupRejectsUnsafeDoclingTransport(string baseUrl)
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            ConfigureClosedProduction(builder);
            builder.UseSetting("InventoryExtraction:Mode", "Docling");
            builder.UseSetting("InventoryExtraction:BaseUrl", baseUrl);
            builder.UseSetting("InventoryExtraction:ApiKey", "closed-test-key");
        });

        var exception = Assert.Throws<InvalidOperationException>(factory.CreateClient);

        Assert.Contains(
            "Docling document extraction must use an HTTPS URL with a host and no " +
            "embedded credentials outside development and test.",
            exception.ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task TestStartupAllowsLocalHttpDoclingTransport()
    {
        await using var factory = CreateDoclingTestFactory("http://docling.test");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RegisteredClientDoesNotFollowRedirectWithBytesOrApiKey()
    {
        var redirectedRequests = 0;
        await using var target = await StartLoopbackServerAsync(app =>
            app.MapPost("/redirected", async context =>
            {
                Interlocked.Increment(ref redirectedRequests);
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(
                    "{\"status\":\"success\",\"document\":{\"json_content\":{\"tables\":[]}}}");
            }));
        var targetAddress = GetServerAddress(target);
        string? receivedApiKey = null;
        string? receivedBody = null;
        await using var source = await StartLoopbackServerAsync(app =>
            app.MapPost("/v1/convert/file", async context =>
            {
                receivedApiKey = context.Request.Headers["X-Api-Key"];
                using var reader = new StreamReader(context.Request.Body);
                receivedBody = await reader.ReadToEndAsync(context.RequestAborted);
                context.Response.StatusCode = StatusCodes.Status307TemporaryRedirect;
                context.Response.Headers.Location = $"{targetAddress}/redirected";
            }));
        await using var factory = CreateDoclingTestFactory(GetServerAddress(source));
        using var scope = factory.Services.CreateScope();
        var adapter = scope.ServiceProvider
            .GetRequiredService<DoclingInventoryExtractionAdapter>();

        await Assert.ThrowsAsync<InventoryExtractionUnavailableException>(() =>
            adapter.ExtractAsync(new InventoryExtractionRequest(
                "redirect-proof.pdf", "application/pdf", "PDF",
                new string('a', 64), [1, 2, 3]), CancellationToken.None));

        Assert.Equal("redirect-test-key", receivedApiKey);
        Assert.Contains("redirect-proof.pdf", receivedBody, StringComparison.Ordinal);
        Assert.Equal(0, Volatile.Read(ref redirectedRequests));
    }

    private static WebApplicationFactory<Program> CreateDoclingTestFactory(string baseUrl) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Test");
            builder.UseSetting(
                "ConnectionStrings:CommercialDatabase",
                "Host=localhost;Database=closed;Username=closed");
            builder.UseSetting("InventoryExtraction:Mode", "Docling");
            builder.UseSetting("InventoryExtraction:BaseUrl", baseUrl);
            builder.UseSetting("InventoryExtraction:ApiKey", "redirect-test-key");
        });

    private static async Task<WebApplication> StartLoopbackServerAsync(
        Action<WebApplication> configure)
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
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

    [Fact]
    public async Task MapsPinnedStructuredTableAndCoordinatesWithoutVendorTypes()
    {
        var response = JsonSerializer.Serialize(new
        {
            status = "success",
            document = new
            {
                json_content = new
                {
                    texts = new[]
                    {
                        new
                        {
                            text = "Supplier: City Media; VAT status: REGISTERED",
                            prov = new[] { new { page_no = 1 } },
                            ocr_confidence = 0.99m,
                        },
                    },
                    tables = new[]
                    {
                        new
                        {
                            prov = new[] { new { page_no = 2 } },
                            data = new
                            {
                                table_cells = new object[]
                                {
                                    Cell("Product Code", 0, 0), Cell("Rate", 0, 1),
                                    Cell("SITE-1", 1, 0), Cell("1250.00", 1, 1),
                                },
                            },
                        },
                    },
                },
                text_content = string.Empty,
            },
        });
        using var client = new HttpClient(new StubHandler(request =>
        {
            Assert.Equal("local-contract-key", request.Headers.GetValues("X-Api-Key").Single());
            Assert.Equal("/v1/convert/file", request.RequestUri!.AbsolutePath);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json"),
            };
        }))
        { BaseAddress = new Uri("http://docling.test") };
        var settings = Options.Create(new InventoryExtractionOptions
        {
            Mode = InventoryExtractionOptions.DoclingMode,
            BaseUrl = "http://docling.test",
            ApiKey = "local-contract-key",
        });
        var adapter = new DoclingInventoryExtractionAdapter(client, settings);

        var result = await adapter.ExtractAsync(new InventoryExtractionRequest(
            "rates.pdf", "application/pdf", "PDF", new string('a', 64), [1, 2, 3]),
            CancellationToken.None);

        Assert.Equal("docling", result.AdapterCode);
        Assert.Equal(InventoryExtractionOptions.PinnedAdapterVersion, result.AdapterVersion);
        Assert.Equal("SITE-1", Assert.Single(result.Rows).Values["productcode"]);
        Assert.Equal("City Media", result.Rows[0].Values["supplier"]);
        Assert.Equal("REGISTERED", result.Rows[0].Values["vatstatus"]);
        Assert.Equal("docling:page=2;table=1;row=2", result.Rows[0].Locator);
        Assert.Equal("docling:page=1;text=1;segment=1",
            result.Rows[0].FieldLocators!["supplier"]);
        Assert.Equal(64, result.ProviderOutputHash.Length);
        Assert.Equal(64, result.CanonicalOutputHash.Length);
        Assert.Contains("\"tables\"", result.ProviderJson, StringComparison.Ordinal);
        using var canonical = JsonDocument.Parse(result.CanonicalJson);
        Assert.Equal(InventoryExtractionOptions.CurrentSchemaVersion,
            canonical.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Equal("SITE-1", canonical.RootElement.GetProperty("rows")[0]
            .GetProperty("values").GetProperty("productcode").GetString());
    }

    private static void ConfigureClosedProduction(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting(
            "ConnectionStrings:CommercialDatabase",
            "Host=localhost;Database=closed;Username=closed");
        builder.UseSetting("Authentication:Mode", "Disabled");
        builder.UseSetting("AgentRuntime:Mode", "Disabled");
        builder.UseSetting("AllowedHosts", "api.advertified.example");
        builder.UseSetting("ReverseProxy:KnownProxies:0", "127.0.0.1");
        builder.UseSetting("InventoryProtection:ObjectStoreMode", "Minio");
        builder.UseSetting("InventoryProtection:ScannerMode", "ClamAv");
        builder.UseSetting("InventoryProtection:Endpoint", "localhost:9000");
        builder.UseSetting("InventoryProtection:AccessKey", "closed-test-access");
        builder.UseSetting("InventoryProtection:SecretKey", "closed-test-secret");
        builder.UseSetting("InventoryProtection:UseTls", "true");
        builder.UseSetting("InventoryProtection:ClamAvHost", "localhost");
    }

    private static object Cell(string text, int row, int column) => new
    {
        text,
        start_row_offset_idx = row,
        start_col_offset_idx = column,
    };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> send) :
        HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(send(request));
    }
}

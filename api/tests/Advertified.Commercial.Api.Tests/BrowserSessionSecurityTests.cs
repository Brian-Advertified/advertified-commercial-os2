using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class BrowserSessionSecurityTests
{
    private const string LocalOrigin = "http://localhost";
    private static readonly DateTimeOffset InitialTime =
        new(2026, 8, 29, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task LocalSessionRequiresOriginAndAntiforgeryAndLogoutInvalidatesIt()
    {
        await using var factory = CreateFactory();
        using var client = CreateClient(factory);
        var requestToken = await GetRequestTokenAsync(client, authenticated: false);

        using var missingToken = CreateRequest(HttpMethod.Post, requestToken: null, LocalOrigin);
        using var missingTokenResponse = await client.SendAsync(missingToken);
        await AssertProblemAsync(
            missingTokenResponse,
            HttpStatusCode.Forbidden,
            "CSRF_VALIDATION_FAILED");

        using var wrongOrigin = CreateRequest(
            HttpMethod.Post,
            requestToken,
            "http://untrusted.example");
        using var wrongOriginResponse = await client.SendAsync(wrongOrigin);
        await AssertProblemAsync(
            wrongOriginResponse,
            HttpStatusCode.Forbidden,
            "ORIGIN_NOT_ALLOWED");

        using var signIn = CreateRequest(HttpMethod.Post, requestToken, LocalOrigin);
        using var signInResponse = await client.SendAsync(signIn);
        Assert.Equal(HttpStatusCode.OK, signInResponse.StatusCode);
        Assert.Equal("nosniff", signInResponse.Headers.GetValues(
            "X-Content-Type-Options").Single());
        Assert.Equal("DENY", signInResponse.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", signInResponse.Headers.GetValues("Referrer-Policy").Single());
        Assert.True(signInResponse.Headers.CacheControl?.NoStore);
        Assert.Contains(
            signInResponse.Headers.GetValues("Set-Cookie"),
            value => value.Contains("advertified.session=", StringComparison.Ordinal)
                && value.Contains("httponly", StringComparison.OrdinalIgnoreCase)
                && value.Contains("samesite=lax", StringComparison.OrdinalIgnoreCase));

        var authenticatedToken = await GetRequestTokenAsync(client, authenticated: true);
        using var unsafeWithoutOrigin = new HttpRequestMessage(
            HttpMethod.Put,
            "/api/v1/tenants/10000000-0000-0000-0000-000000000001");
        using var unsafeResponse = await client.SendAsync(unsafeWithoutOrigin);
        await AssertProblemAsync(
            unsafeResponse,
            HttpStatusCode.Forbidden,
            "ORIGIN_NOT_ALLOWED");

        using var traceWithoutOrigin = new HttpRequestMessage(
            HttpMethod.Trace,
            "/api/v1/session");
        using var traceResponse = await client.SendAsync(traceWithoutOrigin);
        await AssertProblemAsync(
            traceResponse,
            HttpStatusCode.Forbidden,
            "ORIGIN_NOT_ALLOWED");

        using var logout = CreateRequest(HttpMethod.Delete, authenticatedToken, LocalOrigin);
        using var logoutResponse = await client.SendAsync(logout);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
        await GetRequestTokenAsync(client, authenticated: false);

        using var denied = await client.GetAsync("/api/v1/me");
        await AssertProblemAsync(
            denied,
            HttpStatusCode.Unauthorized,
            "AUTHENTICATION_REQUIRED");
    }

    [Fact]
    public async Task ExpiredBrowserSessionDeniesWithoutReachingCommercialData()
    {
        var timeProvider = new AdjustableTimeProvider(InitialTime);
        await using var factory = CreateFactory(timeProvider);
        using var client = CreateClient(factory);
        var requestToken = await GetRequestTokenAsync(client, authenticated: false);
        using var signIn = CreateRequest(HttpMethod.Post, requestToken, LocalOrigin);
        using var signInResponse = await client.SendAsync(signIn);
        Assert.Equal(HttpStatusCode.OK, signInResponse.StatusCode);

        timeProvider.Advance(TimeSpan.FromMinutes(6));
        using var denied = await client.GetAsync("/api/v1/me");
        await AssertProblemAsync(
            denied,
            HttpStatusCode.Unauthorized,
            "AUTHENTICATION_REQUIRED");
    }

    [Fact]
    public async Task RepeatedSessionMutationsReturnHumanSafeRateLimitProblem()
    {
        await using var factory = CreateFactory();
        using var client = CreateClient(factory);
        for (var attempt = 0; attempt < 20; attempt++)
        {
            using var denied = new HttpRequestMessage(HttpMethod.Post, "/api/v1/session");
            denied.Headers.Add("Origin", LocalOrigin);
            using var response = await client.SendAsync(denied);
            await AssertProblemAsync(
                response, HttpStatusCode.Forbidden, "CSRF_VALIDATION_FAILED");
        }

        using var limited = new HttpRequestMessage(HttpMethod.Post, "/api/v1/session");
        limited.Headers.Add("Origin", LocalOrigin);
        using var limitedResponse = await client.SendAsync(limited);
        await AssertProblemAsync(
            limitedResponse, HttpStatusCode.TooManyRequests, "RATE_LIMITED");
    }

    [Fact]
    public async Task TrustedForwardedClientAddressSeparatesRateLimitPartitions()
    {
        await using var factory = CreateFactory(useTrustedProxy: true);
        using var client = CreateClient(factory);
        for (var attempt = 0; attempt < 20; attempt++)
        {
            using var denied = new HttpRequestMessage(HttpMethod.Post, "/api/v1/session");
            denied.Headers.Add("Origin", LocalOrigin);
            denied.Headers.Add("X-Forwarded-For", "203.0.113.10");
            using var response = await client.SendAsync(denied);
            await AssertProblemAsync(
                response, HttpStatusCode.Forbidden, "CSRF_VALIDATION_FAILED");
        }

        using var otherClient = new HttpRequestMessage(HttpMethod.Post, "/api/v1/session");
        otherClient.Headers.Add("Origin", LocalOrigin);
        otherClient.Headers.Add("X-Forwarded-For", "203.0.113.11");
        using var otherResponse = await client.SendAsync(otherClient);
        await AssertProblemAsync(
            otherResponse, HttpStatusCode.Forbidden, "CSRF_VALIDATION_FAILED");
    }

    [Fact]
    public void ProductionRejectsWildcardAllowedHosts()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("AllowedHosts", "*");
            builder.UseSetting(
                "ConnectionStrings:CommercialDatabase",
                "Host=localhost;Database=closed;Username=closed");
            builder.UseSetting("Authentication:Mode", "Disabled");
            builder.UseSetting("AgentRuntime:Mode", "Disabled");
            builder.UseSetting("InventoryProtection:ObjectStoreMode", "Minio");
            builder.UseSetting("InventoryProtection:ScannerMode", "ClamAv");
            builder.UseSetting("InventoryProtection:Endpoint", "localhost:9000");
            builder.UseSetting("InventoryProtection:AccessKey", "test-access");
            builder.UseSetting("InventoryProtection:SecretKey", "test-secret");
            builder.UseSetting("InventoryProtection:ClamAvHost", "localhost");
        });

        var exception = Assert.Throws<InvalidOperationException>(factory.CreateClient);
        Assert.Contains("AllowedHosts", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionRejectsMissingTrustedProxyBoundary()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            ConfigureClosedProduction(builder);
            builder.UseSetting("AllowedHosts", "api.advertified.example");
        });

        var exception = Assert.Throws<InvalidOperationException>(factory.CreateClient);
        Assert.Contains("trusted reverse-proxy", exception.ToString(),
            StringComparison.OrdinalIgnoreCase);
    }

    private static void ConfigureClosedProduction(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting(
            "ConnectionStrings:CommercialDatabase",
            "Host=localhost;Database=closed;Username=closed");
        builder.UseSetting("Authentication:Mode", "Disabled");
        builder.UseSetting("AgentRuntime:Mode", "Disabled");
        builder.UseSetting("InventoryProtection:ObjectStoreMode", "Minio");
        builder.UseSetting("InventoryProtection:ScannerMode", "ClamAv");
        builder.UseSetting("InventoryProtection:Endpoint", "localhost:9000");
        builder.UseSetting("InventoryProtection:AccessKey", "test-access");
        builder.UseSetting("InventoryProtection:SecretKey", "test-secret");
        builder.UseSetting("InventoryProtection:ClamAvHost", "localhost");
    }

    private static WebApplicationFactory<Program> CreateFactory(
        TimeProvider? timeProvider = null,
        bool useTrustedProxy = false)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Test");
            builder.UseSetting(
                "ConnectionStrings:CommercialDatabase",
                "Host=localhost;Database=closed;Username=closed");
            builder.UseSetting("Authentication:Mode", "DeterministicSession");
            builder.UseSetting(
                "Authentication:DevelopmentIdentity:UserId",
                "10000000-0000-0000-0000-000000000001");
            builder.UseSetting(
                "Authentication:DevelopmentIdentity:ActorId",
                "10000000-0000-0000-0000-000000000001");
            builder.UseSetting("Authentication:DevelopmentIdentity:IdentityType", "human");
            builder.UseSetting("Authentication:BrowserSession:LifetimeMinutes", "5");
            builder.UseSetting("Authentication:BrowserSession:SecureCookie", "false");
            builder.UseSetting("Authentication:BrowserSession:AllowedOrigins:0", LocalOrigin);
            if (useTrustedProxy)
            {
                builder.UseSetting("ReverseProxy:KnownProxies:0", "127.0.0.1");
                builder.ConfigureServices(services =>
                    services.AddSingleton<IStartupFilter, LoopbackRemoteAddressStartupFilter>());
            }
            if (timeProvider is not null)
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton(timeProvider);
                });
            }
        });
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri(LocalOrigin),
            HandleCookies = true,
        });

    private static HttpRequestMessage CreateRequest(
        HttpMethod method,
        string? requestToken,
        string origin)
    {
        var request = new HttpRequestMessage(method, "/api/v1/session");
        request.Headers.Add("Origin", origin);
        if (requestToken is not null)
        {
            request.Headers.Add("X-CSRF-TOKEN", requestToken);
        }

        return request;
    }

    private static async Task<string> GetRequestTokenAsync(
        HttpClient client,
        bool authenticated)
    {
        using var response = await client.GetAsync("/api/v1/session");
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(authenticated, json.RootElement.GetProperty("authenticated").GetBoolean());
        return json.RootElement.GetProperty("antiforgeryToken").GetString()!;
    }

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode status,
        string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
        Assert.True(Guid.TryParse(
            json.RootElement.GetProperty("correlationId").GetString(),
            out _));
    }

    private sealed class AdjustableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset current = utcNow;

        public override DateTimeOffset GetUtcNow() => current;

        public void Advance(TimeSpan duration) => current = current.Add(duration);
    }

    private sealed class LoopbackRemoteAddressStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, continuation) =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Loopback;
                await continuation();
            });
            next(app);
        };
    }
}

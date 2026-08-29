using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class CommercialFoundationApiAcceptanceTests
{
    private const string BrowserOrigin = "http://localhost";

    private static async Task AssertBrowserSessionJourneyAsync(string connectionString)
    {
        await using var factory = CreateBrowserFactory(connectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri(BrowserOrigin),
            HandleCookies = true,
        });

        var anonymousToken = await ReadAntiforgeryTokenAsync(client, authenticated: false);
        using var signIn = SessionRequest(HttpMethod.Post, anonymousToken);
        using var signInResponse = await client.SendAsync(signIn);
        Assert.Equal(HttpStatusCode.OK, signInResponse.StatusCode);
        var authenticatedToken = await ReadAntiforgeryTokenAsync(client, authenticated: true);

        using var workspaces = await client.GetAsync("/api/v1/workspaces");
        using var workspacesJson = await ReadJsonAsync(workspaces);
        Assert.Equal(
            TenantId,
            workspacesJson.RootElement[0].GetProperty("tenantId").GetGuid());

        using var profile = await client.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        Assert.Equal("\"2\"", profile.Headers.ETag?.Tag);

        using var update = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/v1/tenants/{TenantId}/me")
        {
            Content = JsonContent.Create(new
            {
                displayName = "Gate Three User",
                phone = "+27115550101",
            }),
        };
        AddBrowserCommandHeaders(update, authenticatedToken, expectedVersion: 2);
        using var updateResponse = await client.SendAsync(update);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal("\"3\"", updateResponse.Headers.ETag?.Tag);

        using var denied = await client.GetAsync($"/api/v1/tenants/{OtherTenantId}");
        await AssertProblemAsync(denied, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");

        using var logout = SessionRequest(HttpMethod.Delete, authenticatedToken);
        using var logoutResponse = await client.SendAsync(logout);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
        using var signedOut = await client.GetAsync("/api/v1/me");
        await AssertProblemAsync(
            signedOut,
            HttpStatusCode.Unauthorized,
            "AUTHENTICATION_REQUIRED");
    }

    private static WebApplicationFactory<Program> CreateBrowserFactory(
        string connectionString)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("ConnectionStrings:CommercialDatabase", connectionString);
            builder.UseSetting("Authentication:Mode", "DeterministicSession");
            builder.UseSetting("Authentication:DevelopmentIdentity:UserId", UserId.ToString());
            builder.UseSetting("Authentication:DevelopmentIdentity:ActorId", UserId.ToString());
            builder.UseSetting("Authentication:DevelopmentIdentity:IdentityType", "human");
            builder.UseSetting("Authentication:BrowserSession:SecureCookie", "false");
            builder.UseSetting("Authentication:BrowserSession:AllowedOrigins:0", BrowserOrigin);
        });
    }

    private static async Task<string> ReadAntiforgeryTokenAsync(
        HttpClient client,
        bool authenticated)
    {
        using var response = await client.GetAsync("/api/v1/session");
        using var json = await ReadJsonAsync(response);
        Assert.Equal(
            authenticated,
            json.RootElement.GetProperty("authenticated").GetBoolean());
        return json.RootElement.GetProperty("antiforgeryToken").GetString()!;
    }

    private static HttpRequestMessage SessionRequest(HttpMethod method, string token)
    {
        var request = new HttpRequestMessage(method, "/api/v1/session");
        request.Headers.TryAddWithoutValidation("Origin", BrowserOrigin);
        request.Headers.Add("X-CSRF-TOKEN", token);
        return request;
    }

    private static void AddBrowserCommandHeaders(
        HttpRequestMessage request,
        string token,
        long expectedVersion)
    {
        request.Headers.TryAddWithoutValidation("Origin", BrowserOrigin);
        request.Headers.Add("X-CSRF-TOKEN", token);
        request.Headers.Add("Idempotency-Key", "gate-3-browser-profile");
        request.Headers.Add("X-Correlation-ID", Guid.NewGuid().ToString());
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{expectedVersion}\"");
    }
}

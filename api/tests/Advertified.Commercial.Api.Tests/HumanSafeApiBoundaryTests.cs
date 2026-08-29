using System.Net;
using System.Text.Json;
using Advertified.Commercial.Application.Identity;
using Advertified.Commercial.Domain.Governance;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class HumanSafeApiBoundaryTests
{
    [Theory]
    [InlineData("Deterministic")]
    [InlineData("DeterministicSession")]
    public void DeterministicAuthenticationCannotStartInProduction(string mode)
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                builder.UseSetting("Authentication:Mode", mode);
                builder.UseSetting(
                    "ConnectionStrings:CommercialDatabase",
                    "Host=localhost;Database=closed;Username=closed");
            });

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains(
            "Deterministic authentication and sessions are restricted to development and test.",
            exception.ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthenticationAndUnexpectedFailuresReturnOnlyHumanSafeProblems()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var unauthenticated = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Authentication:Mode"] = "Disabled",
                })));
        using var unauthenticatedClient = unauthenticated.CreateClient();

        using var denied = await unauthenticatedClient.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        await AssertProblemAsync(denied, "AUTHENTICATION_REQUIRED", null);

        const string internalMessage =
            "provider failure: password=do-not-render; sqlstate=internal";
        using var failing = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Authentication:Mode"] = "Deterministic",
                    ["Authentication:DevelopmentIdentity:UserId"] =
                        "10000000-0000-0000-0000-000000000001",
                    ["Authentication:DevelopmentIdentity:ActorId"] =
                        "10000000-0000-0000-0000-000000000001",
                }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IIdentityWorkspaceReader>();
                services.AddSingleton<IIdentityWorkspaceReader>(
                    new ThrowingWorkspaceReader(internalMessage));
            });
        });
        using var failingClient = failing.CreateClient();

        using var failed = await failingClient.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        await AssertProblemAsync(failed, "UNEXPECTED_ERROR", internalMessage);
    }

    [Theory]
    [InlineData("human", "2000-01-01T00:00:00Z")]
    [InlineData("unknown", null)]
    public async Task ExpiredOrInvalidLocalIdentitiesDenySafely(
        string identityType,
        string? expiresAtUtc)
    {
        await using var factory = CreateIdentityFactory(identityType, expiresAtUtc);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertProblemAsync(response, "AUTHENTICATION_REQUIRED", null);
    }

    [Fact]
    public async Task ServiceIdentityCannotUseInteractiveEndpoints()
    {
        await using var factory = CreateIdentityFactory("service", null);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/me");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemAsync(response, "TENANT_FORBIDDEN", null);
    }

    private static WebApplicationFactory<Program> CreateIdentityFactory(
        string identityType,
        string? expiresAtUtc)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("ConnectionStrings:CommercialDatabase", "Host=localhost;Database=closed;Username=closed");
            builder.UseSetting("Authentication:Mode", "Deterministic");
            builder.UseSetting(
                "Authentication:DevelopmentIdentity:UserId",
                "10000000-0000-0000-0000-000000000001");
            builder.UseSetting(
                "Authentication:DevelopmentIdentity:ActorId",
                "10000000-0000-0000-0000-000000000001");
            builder.UseSetting("Authentication:DevelopmentIdentity:IdentityType", identityType);
            if (expiresAtUtc is not null)
            {
                builder.UseSetting("Authentication:DevelopmentIdentity:ExpiresAtUtc", expiresAtUtc);
            }
        });
    }

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        string expectedCode,
        string? forbiddenText)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var content = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(content);
        Assert.Equal(expectedCode, json.RootElement.GetProperty("code").GetString());
        Assert.True(Guid.TryParse(
            json.RootElement.GetProperty("correlationId").GetString(),
            out _));

        if (forbiddenText is not null)
        {
            Assert.DoesNotContain(forbiddenText, content, StringComparison.Ordinal);
        }
    }

    private sealed class ThrowingWorkspaceReader(string message) : IIdentityWorkspaceReader
    {
        public Task<CurrentUserView> GetCurrentUserAsync(
            UserId userId,
            CancellationToken cancellationToken)
        {
            return Task.FromException<CurrentUserView>(new InvalidOperationException(message));
        }

        public Task<IReadOnlyList<WorkspaceView>> ListWorkspacesAsync(
            UserId userId,
            CancellationToken cancellationToken)
        {
            return Task.FromException<IReadOnlyList<WorkspaceView>>(
                new InvalidOperationException(message));
        }
    }
}

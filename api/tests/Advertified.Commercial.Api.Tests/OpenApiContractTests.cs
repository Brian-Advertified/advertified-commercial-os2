using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class OpenApiContractTests
{
    [Fact]
    public async Task RetainedV1ContractMatchesTheRunningApi()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var actualJson = await client.GetStringAsync("/swagger/v1/swagger.json");
        var retainedPath = Path.Combine(
            AppContext.BaseDirectory,
            "Contracts",
            "advertified-commercial-api.v1.json");
        var retainedJson = await File.ReadAllTextAsync(retainedPath);

        var actual = JsonNode.Parse(actualJson);
        var retained = JsonNode.Parse(retainedJson);

        Assert.True(
            JsonNode.DeepEquals(retained, actual),
            "The running v1 OpenAPI contract differs from the retained generated contract.");
    }

    [Fact]
    public async Task V1ContractPublishesGate3SessionAndCommercialSemantics()
    {
        var retainedPath = Path.Combine(
            AppContext.BaseDirectory,
            "Contracts",
            "advertified-commercial-api.v1.json");
        var contract = JsonNode.Parse(await File.ReadAllTextAsync(retainedPath))!;
        var paths = contract["paths"]!.AsObject();
        var tenantOperation = paths["/api/v1/tenants/{tenantId}"]!["put"]!;
        var parameters = tenantOperation["parameters"]!.AsArray();

        AssertHeaderParameter(parameters, "Idempotency-Key", required: true);
        AssertHeaderParameter(parameters, "If-Match", required: true);
        AssertHeaderParameter(parameters, "X-Correlation-ID", required: false);
        Assert.NotNull(tenantOperation["responses"]!["200"]!["headers"]!["ETag"]);
        Assert.NotNull(paths["/api/v1/tenants/{tenantId}/memberships"]);
        Assert.NotNull(paths["/api/v1/tenants/{tenantId}/agencies"]!["get"]);
        Assert.NotNull(paths["/api/v1/tenants/{tenantId}/contacts"]!["post"]);

        var session = paths["/api/v1/session"]!;
        var sessionPostParameters = session["post"]!["parameters"]!.AsArray();
        AssertHeaderParameter(sessionPostParameters, "X-CSRF-TOKEN", required: true);
        Assert.DoesNotContain(
            sessionPostParameters,
            item => item?["name"]?.GetValue<string>() == "Idempotency-Key");
        AssertHeaderParameter(
            session["delete"]!["parameters"]!.AsArray(),
            "X-CSRF-TOKEN",
            required: true);

        var profileParameters = paths["/api/v1/tenants/{tenantId}/me"]!["put"]!
            ["parameters"]!.AsArray();
        AssertHeaderParameter(profileParameters, "X-CSRF-TOKEN", required: false);
    }

    private static void AssertHeaderParameter(
        JsonArray parameters,
        string name,
        bool required)
    {
        var parameter = parameters.Single(item =>
            item?["in"]?.GetValue<string>() == "header" &&
            item["name"]?.GetValue<string>() == name);
        Assert.Equal(required, parameter!["required"]?.GetValue<bool>() ?? false);
    }
}

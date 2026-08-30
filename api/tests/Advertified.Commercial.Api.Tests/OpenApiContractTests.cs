using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class OpenApiContractTests
{
    [Fact]
    public async Task RetainedV1ContractMatchesTheRunningApi()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseDeterministicInventoryProtection();
            });
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
    public async Task V1ContractPublishesSessionCommercialAndCanonicalBriefSemantics()
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

        var interpret = paths[
            "/api/v1/tenants/{tenantId}/opportunities/{opportunityId}/interpret"]!["post"]!;
        AssertHeaderParameter(interpret["parameters"]!.AsArray(), "Idempotency-Key", true);
        Assert.DoesNotContain(
            interpret["parameters"]!.AsArray(),
            item => item?["name"]?.GetValue<string>() == "If-Match");
        Assert.NotNull(interpret["responses"]!["202"]);

        var selectAngle = paths[
            "/api/v1/tenants/{tenantId}/opportunity-angles/{angleId}:select"]!["post"]!;
        AssertHeaderParameter(selectAngle["parameters"]!.AsArray(), "If-Match", true);
        Assert.NotNull(paths[
            "/api/v1/tenants/{tenantId}/opportunities/{opportunityId}/strategies:generate"]);
        Assert.NotNull(paths[
            "/api/v1/tenants/{tenantId}/strategy-versions/{strategyId}:approve"]);
        Assert.NotNull(paths["/api/v1/tenants/{tenantId}/agent-runs/{runId}"]!["get"]);
        Assert.NotNull(paths["/api/v1/tenants/{tenantId}/human-tasks"]!["get"]);
        Assert.NotNull(paths["/api/v1/tenants/{tenantId}/briefs"]!["post"]);
        Assert.NotNull(paths["/api/v1/tenants/{tenantId}/briefs/{briefId}"]!["get"]);
        Assert.NotNull(paths[
            "/api/v1/tenants/{tenantId}/briefs/{briefId}/versions"]!["post"]);
        var confirmBrief = paths[
            "/api/v1/tenants/{tenantId}/brief-versions/{versionId}:approve"]!["post"]!;
        AssertHeaderParameter(confirmBrief["parameters"]!.AsArray(), "If-Match", true);
        var submitBrief = contract["components"]!["schemas"]!["SubmitBriefVersionCommand"]!;
        Assert.NotNull(submitBrief["properties"]!["confirmerUserId"]);
        Assert.Null(submitBrief["properties"]!["approverUserId"]);
        Assert.NotNull(paths[
            "/api/v1/tenants/{tenantId}/opportunities/{opportunityId}/briefs:draft"]);
        Assert.NotNull(paths["/api/v1/tenants/{tenantId}/inventory-imports"]!["post"]);
        var executeImport = paths[
            "/api/v1/tenants/{tenantId}/inventory-imports/{importId}:execute"]!["post"]!;
        AssertHeaderParameter(executeImport["parameters"]!.AsArray(), "If-Match", true);
        Assert.NotNull(paths[
            "/api/v1/tenants/{tenantId}/inventory-candidates/{candidateId}:review"]);
        Assert.NotNull(paths[
            "/api/v1/tenants/{tenantId}/inventory-imports/{importId}:publish"]);
        Assert.NotNull(paths["/api/v1/tenants/{tenantId}/inventory-products"]!["get"]);
        Assert.NotNull(paths[
            "/api/v1/tenants/{tenantId}/inventory-products/{productId}"]!["get"]);
        Assert.NotNull(paths[
            "/api/v1/tenants/{tenantId}/proposals/{proposalVersionId}"]!["get"]);
        Assert.NotNull(paths[
            "/api/v1/tenants/{tenantId}/proposal-versions/{proposalVersionId}:approve"]);
        Assert.NotNull(paths[
            "/api/v1/tenants/{tenantId}/proposal-versions/{proposalVersionId}:render"]);
        Assert.NotNull(paths[
            "/api/v1/tenants/{tenantId}/proposal-versions/{proposalVersionId}:share"]);
        Assert.NotNull(paths[
            "/api/v1/tenants/{tenantId}/proposal-versions/{proposalVersionId}:select-option"]);
        Assert.NotNull(paths[
            "/api/v1/tenants/{tenantId}/brief-versions/{briefVersionId}/planning"]!["get"]);
        Assert.NotNull(paths[
            "/api/v1/tenants/{tenantId}/brief-versions/{briefVersionId}/campaign-mode:select"]!["post"]);
        Assert.NotNull(paths[
            "/api/v1/tenants/{tenantId}/brief-versions/{briefVersionId}/audiences:generate"]!["post"]);
        Assert.NotNull(paths[
            "/api/v1/tenants/{tenantId}/briefs/{briefId}/approved-plans"]!["get"]);
        Assert.NotNull(paths[
            "/api/v1/tenants/{tenantId}/briefs/{briefId}/proposals:generate"]!["post"]);
        Assert.NotNull(paths[
            "/api/v1/tenants/{tenantId}/proposal-recipients"]!["get"]);
        Assert.NotNull(paths[
            "/api/v1/tenants/{tenantId}/proposals/{proposalVersionId}"]!["get"]);
        Assert.NotNull(paths[
            "/api/v1/tenants/{tenantId}/proposal-versions/{proposalVersionId}:render"]!["post"]);
        Assert.NotNull(paths[
            "/api/v1/tenants/{tenantId}/proposal-versions/{proposalVersionId}:share"]!["post"]);
        Assert.NotNull(paths[
            "/api/v1/tenants/{tenantId}/proposal-versions/{proposalVersionId}:select-option"]!["post"]);
        Assert.NotNull(paths["/api/v1/tenants/{tenantId}/marketplace-listings"]!["get"]);
        var publishListing = paths[
            "/api/v1/tenants/{tenantId}/marketplace-listings/{listingId}:publish"]!["post"]!;
        AssertHeaderParameter(publishListing["parameters"]!.AsArray(), "If-Match", true);
        Assert.NotNull(paths["/api/v1/tenants/{tenantId}/marketplace-rfqs"]!["post"]);
        var sendRfq = paths[
            "/api/v1/tenants/{tenantId}/marketplace-rfqs/{rfqId}:send"]!["post"]!;
        AssertHeaderParameter(sendRfq["parameters"]!.AsArray(), "If-Match", true);
        var respond = paths[
            "/api/v1/tenants/{tenantId}/marketplace-rfqs/{rfqId}/responses"]!["post"]!;
        Assert.DoesNotContain(respond["parameters"]!.AsArray(),
            item => item?["name"]?.GetValue<string>() == "If-Match");
        var accept = paths[
            "/api/v1/tenants/{tenantId}/marketplace-responses/{responseId}:accept"]!["post"]!;
        AssertHeaderParameter(accept["parameters"]!.AsArray(), "If-Match", true);
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

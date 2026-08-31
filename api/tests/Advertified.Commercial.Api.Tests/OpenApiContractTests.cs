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
        Assert.NotNull(paths["/api/v1/tenants/{tenantId}/funding"]!["get"]);
        Assert.NotNull(paths["/api/v1/tenants/{tenantId}/purchase-orders"]!["post"]);
        var approvePurchaseOrder = paths[
            "/api/v1/tenants/{tenantId}/purchase-orders/{purchaseOrderId}:approve"]!["post"]!;
        AssertHeaderParameter(
            approvePurchaseOrder["parameters"]!.AsArray(), "If-Match", true);
        Assert.NotNull(paths["/api/v1/tenants/{tenantId}/invoices:issue"]!["post"]);
        Assert.NotNull(paths["/api/v1/tenants/{tenantId}/payment-intents"]!["post"]);
        var reconcilePayment = paths[
            "/api/v1/tenants/{tenantId}/payment-intents/{paymentIntentId}:reconcile"]!["post"]!;
        AssertHeaderParameter(reconcilePayment["parameters"]!.AsArray(), "If-Match", true);
        Assert.NotNull(paths["/api/v1/tenants/{tenantId}/campaigns"]!["get"]);
        Assert.NotNull(paths["/api/v1/tenants/{tenantId}/campaigns/{campaignId}"]!["get"]);
        var confirmCampaignBookings = paths[
            "/api/v1/tenants/{tenantId}/campaigns/{campaignId}:confirm-bookings"]!["post"]!;
        AssertHeaderParameter(
            confirmCampaignBookings["parameters"]!.AsArray(), "If-Match", true);
        var requestCreative = paths[
            "/api/v1/tenants/{tenantId}/campaigns/{campaignId}:request-creative"]!["post"]!;
        AssertHeaderParameter(requestCreative["parameters"]!.AsArray(), "If-Match", true);
        var createCreativeAsset = paths[
            "/api/v1/tenants/{tenantId}/campaigns/{campaignId}/creative"]!["post"]!;
        Assert.DoesNotContain(
            createCreativeAsset["parameters"]!.AsArray(),
            item => item?["name"]?.GetValue<string>() == "If-Match");
        var uploadCreativeVersion = paths[
            "/api/v1/tenants/{tenantId}/campaigns/{campaignId}/creative/{assetId}:upload-version"]!["post"]!;
        AssertHeaderParameter(
            uploadCreativeVersion["parameters"]!.AsArray(), "If-Match", true);
        var brandReview = paths[
            "/api/v1/tenants/{tenantId}/campaigns/{campaignId}/creative/{assetId}:brand-review"]!["post"]!;
        AssertHeaderParameter(brandReview["parameters"]!.AsArray(), "If-Match", true);
        var supplierCreative = paths[
            "/api/v1/tenants/{tenantId}/creative-assets/{assetId}"]!;
        Assert.NotNull(supplierCreative["get"]);
        var supplierReview = paths[
            "/api/v1/tenants/{tenantId}/creative-assets/{assetId}:supplier-review"]!["post"]!;
        AssertHeaderParameter(supplierReview["parameters"]!.AsArray(), "If-Match", true);
        var approveCreative = paths[
            "/api/v1/tenants/{tenantId}/campaigns/{campaignId}:approve-creative"]!["post"]!;
        AssertHeaderParameter(approveCreative["parameters"]!.AsArray(), "If-Match", true);
        var startCampaign = paths[
            "/api/v1/tenants/{tenantId}/campaigns/{campaignId}:start"]!["post"]!;
        AssertHeaderParameter(startCampaign["parameters"]!.AsArray(), "If-Match", true);
        var completeCampaign = paths[
            "/api/v1/tenants/{tenantId}/campaigns/{campaignId}:complete"]!["post"]!;
        AssertHeaderParameter(completeCampaign["parameters"]!.AsArray(), "If-Match", true);
        var submitProof = paths[
            "/api/v1/tenants/{tenantId}/campaigns/{campaignId}/delivery-proofs"]!["post"]!;
        Assert.DoesNotContain(
            submitProof["parameters"]!.AsArray(),
            item => item?["name"]?.GetValue<string>() == "If-Match");
        var reviewProof = paths[
            "/api/v1/tenants/{tenantId}/campaigns/{campaignId}/delivery-proofs/{proofId}:review"]!["post"]!;
        AssertHeaderParameter(reviewProof["parameters"]!.AsArray(), "If-Match", true);
        Assert.NotNull(paths[
            "/api/v1/tenants/{tenantId}/delivery-proofs/{proofId}"]!["get"]);
        var submitPerformance = paths[
            "/api/v1/tenants/{tenantId}/campaigns/{campaignId}/performance-evidence"]!["post"]!;
        Assert.DoesNotContain(
            submitPerformance["parameters"]!.AsArray(),
            item => item?["name"]?.GetValue<string>() == "If-Match");
        var reviewPerformance = paths[
            "/api/v1/tenants/{tenantId}/campaigns/{campaignId}/performance-evidence/{evidenceId}:review"]!["post"]!;
        AssertHeaderParameter(reviewPerformance["parameters"]!.AsArray(), "If-Match", true);
        Assert.NotNull(paths[
            "/api/v1/tenants/{tenantId}/performance-evidence/{evidenceId}"]!["get"]);
        var generateMeasurement = paths[
            "/api/v1/tenants/{tenantId}/campaigns/{campaignId}/measurement-reports:generate"]!["post"]!;
        Assert.DoesNotContain(
            generateMeasurement["parameters"]!.AsArray(),
            item => item?["name"]?.GetValue<string>() == "If-Match");
        var reviewMeasurement = paths[
            "/api/v1/tenants/{tenantId}/campaigns/{campaignId}/measurement-reports/{reportId}:review"]!["post"]!;
        AssertHeaderParameter(reviewMeasurement["parameters"]!.AsArray(), "If-Match", true);
        Assert.NotNull(paths[
            "/api/v1/tenants/{tenantId}/measurement-reports/{reportId}"]!["get"]);
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

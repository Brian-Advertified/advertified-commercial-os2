using System.Net;
using System.Net.Http.Headers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Npgsql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class MarketplaceAcceptanceTests
{
    private static readonly byte[] PdfEvidence =
        Encoding.ASCII.GetBytes("%PDF-1.7\n% deterministic local funding evidence\n%%EOF");

    [Fact]
    [Trait("Category", "Migration")]
    public async Task SelectedOptionRequiresSeparatedFundingApprovalAndReconciliation()
    {
        await using var postgres = CreatePostgres();
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DisposableDatabaseRoles.ProvisionAsync(connectionString);
        await SeedAsync(connectionString);
        var clock = new AdjustableMarketplaceClock(InitialTime);
        await using var supplierFactory = CreateFactory(connectionString, SupplierUserId, clock);
        await using var buyerFactory = CreateFactory(connectionString, BuyerUserId, clock);
        await using var clientFactory = CreateFactory(connectionString, ClientUserId, clock);
        await using var reviewerFactory = CreateFactory(connectionString, ReviewerUserId, clock);
        await using var otherFactory = CreateFactory(connectionString, OtherUserId, clock);
        using var supplier = supplierFactory.CreateClient();
        using var buyer = buyerFactory.CreateClient();
        using var client = clientFactory.CreateClient();
        using var reviewer = reviewerFactory.CreateClient();
        using var other = otherFactory.CreateClient();

        var listing = await CreateAndPublishListingAsync(supplier, buyer);
        var plan = await BuildBuyerPlanAsync(buyer, listing.ListingVersionId);
        using var approvedPlan = await CommandAsync(
            buyer, BuyerTenantId, $"media-plan-versions/{plan.Id}:approve",
            "funding-plan-approve", plan.Version,
            new { reason = "Approve the exact plan before client selection." });
        var selected = await CreateSelectedProposalAsync(buyer, client, plan.Id, clock);

        using var wrongAmount = await SubmitPurchaseOrderAsync(
            buyer, selected, 1, "funding-po-wrong");
        await AssertProblemAsync(
            wrongAmount, HttpStatusCode.Conflict, "FUNDING_REVIEW_REQUIRED");
        using var submitted = await SubmitPurchaseOrderAsync(
            buyer, selected, 1_443_250, "funding-po-submit");
        Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);
        using var submittedJson = JsonDocument.Parse(
            await submitted.Content.ReadAsStringAsync());
        var purchaseOrderId = submittedJson.RootElement.GetProperty("id").GetGuid();
        Assert.Equal("SUBMITTED", submittedJson.RootElement.GetProperty("status").GetString());

        using var selfApproval = await RawCommandAsync(
            buyer, BuyerTenantId, $"purchase-orders/{purchaseOrderId}:approve",
            "funding-po-self-approve", 1,
            new { reconciliationReason = "The submitter cannot approve." });
        await AssertProblemAsync(selfApproval, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");
        using var approved = await CommandAsync(
            reviewer, BuyerTenantId, $"purchase-orders/{purchaseOrderId}:approve",
            "funding-po-approve", 1,
            new { reconciliationReason = "Amount, currency and selected option reconcile." });
        Assert.Equal("APPROVED", approved.RootElement.GetProperty("status").GetString());

        using var invoice = await CommandAsync(
            reviewer, BuyerTenantId, "invoices:issue", "funding-invoice", null,
            new { purchaseOrderId, invoiceNumber = "INV-2026-0001" });
        var invoiceId = invoice.RootElement.GetProperty("id").GetGuid();
        Assert.Equal(1_443_250, invoice.RootElement.GetProperty("totalMinor").GetInt64());
        Assert.Equal(
            invoice.RootElement.GetProperty("totalMinor").GetInt64(),
            invoice.RootElement.GetProperty("subtotalMinor").GetInt64()
            + invoice.RootElement.GetProperty("feesMinor").GetInt64()
            + invoice.RootElement.GetProperty("vatMinor").GetInt64());

        using var providerPayment = await RawCommandAsync(
            buyer, BuyerTenantId, "payment-intents", "funding-vodapay", null,
            new { invoiceId, methodCode = "VODAPAY" });
        await AssertProblemAsync(
            providerPayment, HttpStatusCode.Conflict, "PAYMENT_METHOD_UNAVAILABLE");
        using var payment = await CommandAsync(
            buyer, BuyerTenantId, "payment-intents", "funding-payment", null,
            new { invoiceId, methodCode = "MANUAL_EFT" });
        var paymentId = payment.RootElement.GetProperty("id").GetGuid();
        Assert.Equal("PENDING", payment.RootElement.GetProperty("status").GetString());

        using var selfReconcile = await ReconcilePaymentAsync(
            buyer, paymentId, "funding-self-reconcile", 1);
        await AssertProblemAsync(selfReconcile, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");
        using var reconciled = await ReconcilePaymentAsync(
            reviewer, paymentId, "funding-reconcile", 1);
        Assert.Equal(HttpStatusCode.OK, reconciled.StatusCode);
        using var reconciledJson = JsonDocument.Parse(
            await reconciled.Content.ReadAsStringAsync());
        Assert.Equal("CONFIRMED", reconciledJson.RootElement.GetProperty("status").GetString());

        using var campaigns = await ReadAsync(reviewer, BuyerTenantId, "campaigns");
        var campaign = Assert.Single(campaigns.RootElement.EnumerateArray());
        Assert.Equal("PLANNED", campaign.GetProperty("status").GetString());
        Assert.Equal(selected.ProposalId, campaign.GetProperty("proposalVersionId").GetGuid());
        Assert.NotEqual(Guid.Empty, campaign.GetProperty("planVersionId").GetGuid());
        Assert.Equal("CONFIRMED", campaign.GetProperty("fundingStatus").GetString());
        Assert.Equal(1, campaign.GetProperty("requiredBookingCount").GetInt32());
        Assert.Equal(0, campaign.GetProperty("confirmedBookingCount").GetInt32());
        Assert.Equal(JsonValueKind.Null,
            campaign.GetProperty("nextActionPermission").ValueKind);

        using var unrelated = await other.GetAsync($"/api/v1/tenants/{OtherTenantId}/funding");
        await AssertProblemAsync(unrelated, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");
        using var workspace = await ReadAsync(reviewer, BuyerTenantId, "funding");
        Assert.Single(workspace.RootElement.GetProperty("purchaseOrders").EnumerateArray());
        Assert.Single(workspace.RootElement.GetProperty("invoices").EnumerateArray());
        Assert.Single(workspace.RootElement.GetProperty("payments").EnumerateArray());
        await AssertFundingEvidenceAsync(connectionString, invoiceId);
    }

    private static async Task<HttpResponseMessage> SubmitPurchaseOrderAsync(
        HttpClient client,
        SelectedProposalFixture selected,
        long amountMinor,
        string key)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(selected.ProposalId.ToString()), "proposalVersionId");
        form.Add(new StringContent(selected.OptionId.ToString()), "proposalOptionId");
        form.Add(new StringContent("PO-2026-0001"), "purchaseOrderNumber");
        form.Add(new StringContent(amountMinor.ToString(CultureInfo.InvariantCulture)), "amountMinor");
        form.Add(new StringContent("ZAR"), "currency");
        var file = new ByteArrayContent(PdfEvidence);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "document", "signed-po.pdf");
        return await SendMultipartAsync(
            client, "purchase-orders", key, null, form);
    }

    private static async Task<HttpResponseMessage> ReconcilePaymentAsync(
        HttpClient client,
        Guid paymentId,
        string key,
        long version)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("EFT-LOCAL-2026-0001"), "reconciliationReference");
        form.Add(new StringContent("Authorised reviewer matched the local receipt."), "reason");
        var file = new ByteArrayContent(PdfEvidence);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "receipt", "eft-receipt.pdf");
        return await SendMultipartAsync(
            client, $"payment-intents/{paymentId}:reconcile", key, version, form);
    }

    private static async Task<HttpResponseMessage> SendMultipartAsync(
        HttpClient client,
        string path,
        string key,
        long? version,
        MultipartFormDataContent content)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/tenants/{BuyerTenantId}/{path}") { Content = content };
        request.Headers.Add("Idempotency-Key", key);
        request.Headers.Add("X-Correlation-ID", Guid.NewGuid().ToString());
        if (version.HasValue)
            request.Headers.TryAddWithoutValidation("If-Match", $"\"{version.Value}\"");
        return await client.SendAsync(request);
    }

    private static async Task<Guid> FundSelectedProposalAsync(
        HttpClient buyer,
        HttpClient reviewer,
        SelectedProposalFixture selected)
    {
        using var submitted = await SubmitPurchaseOrderAsync(
            buyer, selected, 1_443_250, "booking-funding-po");
        var submittedContent = await submitted.Content.ReadAsStringAsync();
        Assert.True(submitted.IsSuccessStatusCode, submittedContent);
        using var submittedJson = JsonDocument.Parse(submittedContent);
        var purchaseOrderId = submittedJson.RootElement.GetProperty("id").GetGuid();
        using var approved = await CommandAsync(
            reviewer, BuyerTenantId, $"purchase-orders/{purchaseOrderId}:approve",
            "booking-funding-po-approve", 1,
            new { reconciliationReason = "Exact selected option reconciles." });
        using var invoice = await CommandAsync(
            reviewer, BuyerTenantId, "invoices:issue", "booking-funding-invoice", null,
            new { purchaseOrderId, invoiceNumber = "INV-BOOKING-2026-0001" });
        var invoiceId = invoice.RootElement.GetProperty("id").GetGuid();
        using var payment = await CommandAsync(
            buyer, BuyerTenantId, "payment-intents", "booking-funding-payment", null,
            new { invoiceId, methodCode = "MANUAL_EFT" });
        var paymentId = payment.RootElement.GetProperty("id").GetGuid();
        using var reconciled = await ReconcilePaymentAsync(
            reviewer, paymentId, "booking-funding-reconcile", 1);
        var reconciliationContent = await reconciled.Content.ReadAsStringAsync();
        Assert.True(reconciled.IsSuccessStatusCode, reconciliationContent);
        using var campaigns = await ReadAsync(buyer, BuyerTenantId, "campaigns");
        return Assert.Single(campaigns.RootElement.EnumerateArray())
            .GetProperty("id").GetGuid();
    }

    private static async Task AssertFundingEvidenceAsync(
        string connectionString,
        Guid invoiceId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        Assert.Equal(6, await CountAsync(connection,
            "SELECT count(*)::integer FROM commercial.audit_events WHERE action_code IN " +
            "('purchase_order.submitted','purchase_order.approved','invoice.issued'," +
            "'payment.started','payment.confirmed','campaign.planned')"));
        Assert.Equal(6, await CountAsync(connection,
            "SELECT count(*)::integer FROM commercial.outbox_messages WHERE event_type_code IN " +
            "('PurchaseOrderSubmitted','PurchaseOrderApproved','InvoiceIssued'," +
            "'PaymentStarted','PaymentConfirmed','CampaignPlanned')"));
        await using var mutate = new NpgsqlCommand(
            "UPDATE commercial.invoices SET total_minor = 1 WHERE id = $1", connection);
        mutate.Parameters.AddWithValue(invoiceId);
        var exception = await Assert.ThrowsAsync<PostgresException>(mutate.ExecuteNonQueryAsync);
        Assert.Equal(PostgresErrorCodes.RaiseException, exception.SqlState);
    }
}

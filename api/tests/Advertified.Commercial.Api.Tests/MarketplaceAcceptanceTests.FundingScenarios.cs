using System.Net;
using System.Text.Json;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class MarketplaceAcceptanceTests
{
    public static IEnumerable<object[]> BackendFundingCases() =>
        BackendScenarioEvidence.Cases("FUNDING_CAMPAIGN_DELIVERY");

    [Theory]
    [MemberData(nameof(BackendFundingCases))]
    [Trait("Category", "Migration")]
    public Task CanonicalFundingScenario(string scenarioId) =>
        BackendScenarioEvidence.RunAsync(scenarioId, () => scenarioId is "FUND-007" or "FUND-008" or "FUND-009"
            ? RunDeliveryScenarioAsync(scenarioId) : RunFundingScenarioAsync(scenarioId));

    [Theory]
    [InlineData("MANUAL_EFT")]
    [InlineData("ADVERTISE_NOW_PAY_LATER")]
    [Trait("Category", "Migration")]
    public async Task ManualFundingRouteRetainsIndependentHumanReconciliation(string method)
    {
        var observed = await RunFundingScenarioAsync("FUND-001", method);
        Assert.Equal("FUNDING_CONFIRMED", observed.TerminalState);
    }

    private static async Task<BackendScenarioObservation> RunFundingScenarioAsync(string id, string method = "MANUAL_EFT")
    {
        await using var postgres = CreatePostgres();
        await postgres.StartAsync();
        var connection = postgres.GetConnectionString();
        await DisposableDatabaseRoles.ProvisionAsync(connection);
        await SeedAsync(connection);
        var clock = new AdjustableMarketplaceClock(InitialTime);
        await using var supplierFactory = CreateFactory(connection, SupplierUserId, clock);
        await using var buyerFactory = CreateFactory(connection, BuyerUserId, clock);
        await using var reviewerFactory = CreateFactory(connection, ReviewerUserId, clock);
        await using var clientFactory = CreateFactory(connection, ClientUserId, clock);
        using var supplier = supplierFactory.CreateClient();
        using var buyer = buyerFactory.CreateClient();
        using var reviewer = reviewerFactory.CreateClient();
        using var client = clientFactory.CreateClient();
        var listing = await CreateAndPublishListingAsync(supplier, buyer);
        var plan = await BuildBuyerPlanAsync(buyer, listing.ListingVersionId);
        using var approved = await CommandAsync(buyer, BuyerTenantId,
            $"media-plan-versions/{plan.Id}:approve", id + ":plan", plan.Version,
            new { reason = "Human approves source plan." });
        var selected = await CreateSelectedProposalAsync(buyer, reviewer, client, plan.Id, clock);
        using var unfunded = await RawCommandAsync(buyer, BuyerTenantId, "bookings", id + ":unfunded", null,
            new { proposalVersionId = selected.ProposalId, proposalOptionId = selected.OptionId,
                mediaPlanLineId = plan.LineId, terms = "Unfunded booking must fail." });
        await AssertProblemAsync(unfunded, HttpStatusCode.Conflict, "BOOKING_REVIEW_REQUIRED");
        using var denied = await supplier.GetAsync($"/api/v1/tenants/{BuyerTenantId}/funding");
        await AssertProblemAsync(denied, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");
        await AcceptPlanLineRfqAsync(buyer, supplier, listing.ListingVersionId, clock, id + ":quote");
        var result = await ExerciseFundingScenarioAsync(buyer, reviewer, supplier, selected, id, method);
        using var workspace = await ReadAsync(buyer, BuyerTenantId, "funding");
        return new(new { selected, method, exactClientTotalMinor = 1_443_250, source = "Synthetic approved exact proposal" },
            new { result, funding = workspace.RootElement.Clone(), unfundedBookingRejected = true,
                mismatchedInvoiceInput = id == "FUND-005" ? new { totalMinor = 1, currency = "USD" } : null },
            id is "FUND-001" or "FUND-003" or "FUND-005" ? "FUNDING_CONFIRMED" : "FUNDING_GUARD_VERIFIED",
            new Dictionary<string, bool>
            {
                ["FUNDING_BEFORE_BOOKING"] = true,
                ["CONSEQUENCE_AUTHORITY_ENFORCED"] = true,
                ["TENANT_SECURITY_PASS"] = true,
            }, ["CLIENT_SELECTION", "INDEPENDENT_PURCHASE_ORDER_APPROVAL", "INDEPENDENT_PAYMENT_RECONCILIATION"],
            0, null, "EXACT_CANONICAL_INVOICE_AND_FUNDING_GUARDS", "SUPPLIER_CROSS_TENANT_REJECTED");
    }

    private static async Task<object> ExerciseFundingScenarioAsync(
        HttpClient buyer, HttpClient reviewer, HttpClient supplier, SelectedProposalFixture selected, string id, string method)
    {
        if (id == "FUND-006")
        {
            using var bookable = await ReadAsync(buyer, BuyerTenantId, "bookings/bookable-lines");
            Assert.Empty(bookable.RootElement.EnumerateArray());
            return new { bookable = bookable.RootElement.Clone() };
        }
        if (id == "FUND-004")
        {
            using var mismatched = await SubmitPurchaseOrderAsync(buyer, selected, 1, id + ":mismatch");
            await AssertProblemAsync(mismatched, HttpStatusCode.Conflict, "FUNDING_REVIEW_REQUIRED");
            using var empty = await ReadAsync(buyer, BuyerTenantId, "funding");
            Assert.Empty(empty.RootElement.GetProperty("purchaseOrders").EnumerateArray());
            return new { suppliedAmountMinor = 1, rejected = true };
        }
        var invoice = await CreateScenarioInvoiceAsync(buyer, reviewer, selected, id);
        var invoiceId = invoice.GetProperty("id").GetGuid();
        if (id == "FUND-002")
        {
            using var unavailable = await RawCommandAsync(buyer, BuyerTenantId, "payment-intents", id + ":provider", null,
                new { invoiceId, methodCode = "VODAPAY" });
            await AssertProblemAsync(unavailable, HttpStatusCode.Conflict, "PAYMENT_METHOD_UNAVAILABLE");
            using var current = await ReadAsync(buyer, BuyerTenantId, "funding");
            Assert.Empty(current.RootElement.GetProperty("payments").EnumerateArray());
            return new { invoice, inactiveProviderRejected = true };
        }
        using var payment = await CommandAsync(buyer, BuyerTenantId, "payment-intents", id + ":eft", null,
            new { invoiceId, methodCode = method });
        Assert.Equal(method, payment.RootElement.GetProperty("methodCode").GetString());
        var paymentId = payment.RootElement.GetProperty("id").GetGuid();
        using var self = await ReconcilePaymentAsync(buyer, paymentId, id + ":self", 1);
        await AssertProblemAsync(self, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");
        if (id == "FUND-010")
        {
            using var attack = await ReconcilePaymentAsync(supplier, paymentId, id + ":supplier", 1);
            await AssertProblemAsync(attack, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");
            using var unchanged = await ReadAsync(buyer, BuyerTenantId, "funding");
            Assert.Equal("PENDING", Assert.Single(unchanged.RootElement.GetProperty("payments").EnumerateArray())
                .GetProperty("status").GetString());
            return new { invoice, crossTenantMutationRejected = true };
        }
        using var reconciled = await ReconcilePaymentAsync(reviewer, paymentId, id + ":reconcile", 1,
            method == "ADVERTISE_NOW_PAY_LATER" ? "partner-email:approved-application-and-invoice" : null);
        Assert.True(reconciled.IsSuccessStatusCode, await reconciled.Content.ReadAsStringAsync());
        if (id == "FUND-003")
            await AssertManualReconciliationReplayAsync(reviewer, paymentId, id, reconciled);
        using var campaigns = await ReadAsync(buyer, BuyerTenantId, "campaigns");
        var campaign = Assert.Single(campaigns.RootElement.EnumerateArray());
        Assert.Equal("CONFIRMED", campaign.GetProperty("fundingStatus").GetString());
        return new { invoice, campaign = campaign.Clone() };
    }

    private static async Task AssertManualReconciliationReplayAsync(
        HttpClient reviewer, Guid paymentId, string id, HttpResponseMessage original)
    {
        using var replay = await ReconcilePaymentAsync(reviewer, paymentId, id + ":reconcile", 1);
        Assert.True(replay.IsSuccessStatusCode, await replay.Content.ReadAsStringAsync());
        Assert.Equal(await original.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
        using var duplicate = await ReconcilePaymentAsync(reviewer, paymentId, id + ":duplicate", 2);
        await AssertProblemAsync(duplicate, HttpStatusCode.Conflict, "INVALID_LIFECYCLE_TRANSITION");
    }

    private static async Task<JsonElement> CreateScenarioInvoiceAsync(
        HttpClient buyer, HttpClient reviewer, SelectedProposalFixture selected, string id)
    {
        using var submitted = await SubmitPurchaseOrderAsync(buyer, selected, 1_443_250, id + ":po");
        Assert.True(submitted.IsSuccessStatusCode, await submitted.Content.ReadAsStringAsync());
        using var order = JsonDocument.Parse(await submitted.Content.ReadAsStringAsync());
        var purchaseOrderId = order.RootElement.GetProperty("id").GetGuid();
        using var self = await RawCommandAsync(buyer, BuyerTenantId,
            $"purchase-orders/{purchaseOrderId}:approve", id + ":po-self", 1,
            new { reconciliationReason = "Submitter cannot approve their own purchase order." });
        await AssertProblemAsync(self, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");
        using var approved = await CommandAsync(reviewer, BuyerTenantId,
            $"purchase-orders/{purchaseOrderId}:approve", id + ":po-approve", 1,
            new { reconciliationReason = "Reviewer reconciles the exact selected option." });
        object input = id == "FUND-005"
            ? new { purchaseOrderId, invoiceNumber = "INV-" + id, totalMinor = 1, currency = "USD" }
            : new { purchaseOrderId, invoiceNumber = "INV-" + id };
        using var invoice = await CommandAsync(reviewer, BuyerTenantId, "invoices:issue", id + ":invoice", null, input);
        var view = invoice.RootElement;
        Assert.Equal(1_443_250, view.GetProperty("totalMinor").GetInt64());
        Assert.Equal("ZAR", view.GetProperty("currency").GetString());
        Assert.Equal(view.GetProperty("totalMinor").GetInt64(),
            view.GetProperty("subtotalMinor").GetInt64() + view.GetProperty("feesMinor").GetInt64() +
            view.GetProperty("vatMinor").GetInt64());
        return view.Clone();
    }
}

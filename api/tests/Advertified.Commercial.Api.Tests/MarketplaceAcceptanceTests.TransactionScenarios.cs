using System.Net;
using System.Text.Json;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class MarketplaceAcceptanceTests
{
    public static IEnumerable<object[]> BackendTransactionCases() =>
        BackendScenarioEvidence.Cases("PROPOSAL_TRANSACTION");

    [Theory]
    [MemberData(nameof(BackendTransactionCases))]
    [Trait("Category", "Migration")]
    public Task CanonicalTransactionScenario(string scenarioId) =>
        BackendScenarioEvidence.RunAsync(scenarioId, () => RunTransactionScenarioAsync(scenarioId));

    private static async Task<BackendScenarioObservation> RunTransactionScenarioAsync(string id)
    {
        await using var postgres = CreatePostgres();
        await postgres.StartAsync();
        var connection = postgres.GetConnectionString();
        await DisposableDatabaseRoles.ProvisionAsync(connection);
        await SeedAsync(connection);
        var clock = new AdjustableMarketplaceClock(InitialTime);
        await using var supplierFactory = CreateFactory(connection, SupplierUserId, clock);
        await using var buyerFactory = CreateFactory(connection, BuyerUserId, clock);
        await using var otherFactory = CreateFactory(connection, OtherUserId, clock);
        await using var reviewerFactory = CreateFactory(connection, ReviewerUserId, clock);
        await using var clientFactory = CreateFactory(connection, ClientUserId, clock);
        using var reviewer = reviewerFactory.CreateClient();
        using var client = clientFactory.CreateClient();
        using var supplier = supplierFactory.CreateClient();
        using var buyer = buyerFactory.CreateClient();
        using var other = otherFactory.CreateClient();
        var listing = await CreateAndPublishListingAsync(supplier, buyer);
        using var original = await ReadAsync(supplier, SupplierTenantId, $"marketplace-listings/{listing.ListingId}");
        var isProposal = string.CompareOrdinal(id, "TXN-005") <= 0;
        var result = isProposal
            ? await ExerciseProposalScenarioAsync(buyer, supplier, reviewer, client, other, listing, clock, id)
            : id == "TXN-010"
                ? await ExerciseBookingScenarioAsync(buyer, supplier, reviewer, client, other, listing, clock, id)
                : await ExerciseQuoteScenarioAsync(buyer, supplier, other, listing.ListingVersionId, clock, id);
        using var retained = await ReadAsync(supplier, SupplierTenantId, $"marketplace-listings/{listing.ListingId}");
        Assert.Equal(original.RootElement.GetProperty("currentVersion").GetRawText(),
            retained.RootElement.GetProperty("currentVersion").GetRawText());
        return new(new { listingVersionId = listing.ListingVersionId, source = "Synthetic reviewed supplier inventory" },
            result, id switch { "TXN-001" or "TXN-002" => "PROPOSAL_SENT",
                "TXN-004" => "PROPOSAL_SELECTED", "TXN-005" => "PROPOSAL_DECLINED",
                "TXN-008" => "QUOTE_ACCEPTED", "TXN-010" => "BOOKING_CONFIRMED", _ => "TRANSACTION_BLOCKED" },
            new Dictionary<string, bool>
            {
                ["HISTORICAL_VERSION_IMMUTABLE"] = true,
                ["TRANSACTION_ORDER_ENFORCED"] = true,
                ["NEGOTIATION_LINEAGE_RETAINED"] = true,
            }, ["SUPPLIER_QUOTE_SUBMISSION", "BUYER_EXACT_QUOTE_ACCEPTANCE"],
            0, null, "EXACT_QUOTE_AMOUNTS_AND_HISTORY_CHECKED", "CROSS_TENANT_READ_REJECTED");
    }

    private static async Task<object> ExerciseQuoteScenarioAsync(
        HttpClient buyer, HttpClient supplier, HttpClient other,
        Guid listingVersionId, AdjustableMarketplaceClock clock, string id)
    {
        using var created = await CreateRfqAsync(buyer, listingVersionId,
            "Synthetic time-bounded supplier request", id + ":create", clock.GetUtcNow().AddDays(2));
        var rfqId = created.RootElement.GetProperty("id").GetGuid();
        using var sent = await CommandAsync(buyer, BuyerTenantId,
            $"marketplace-rfqs/{rfqId}:send", id + ":send", 1, new { reason = "Human authorises supplier review." });
        using var denied = await other.GetAsync($"/api/v1/tenants/{OtherTenantId}/marketplace-rfqs/{rfqId}");
        await AssertProblemAsync(denied, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");
        if (id == "TXN-006")
            return await ExerciseExpiredRfqScenarioAsync(buyer, supplier, rfqId, clock, id);
        using var first = await CommandAsync(supplier, SupplierTenantId,
            $"marketplace-rfqs/{rfqId}/responses", id + ":first", 0,
            ScenarioQuote(1_250_000, "AVAILABLE", clock.GetUtcNow().AddHours(1)));
        var firstQuote = first.RootElement.GetProperty("response").Clone();
        return await ExerciseQuoteRevisionScenarioAsync(buyer, supplier, rfqId, firstQuote, clock, id);
    }

    private static object ScenarioQuote(long amount, string availability, DateTimeOffset expiry) => new
    {
        amountMinor = amount, currency = "ZAR", availability,
        terms = "Synthetic human quote; no booking commitment.",
        validUntilUtc = expiry, evidenceReferences = HumanVerifiedEvidence,
    };

    private static async Task<object> ExerciseExpiredRfqScenarioAsync(
        HttpClient buyer, HttpClient supplier, Guid rfqId, AdjustableMarketplaceClock clock, string id)
    {
        clock.Advance(TimeSpan.FromDays(3));
        using var rejected = await RawCommandAsync(supplier, SupplierTenantId,
            $"marketplace-rfqs/{rfqId}/responses", id + ":late", 0,
            ScenarioQuote(1_250_000, "AVAILABLE", clock.GetUtcNow().AddDays(1)));
        await AssertProblemAsync(rejected, HttpStatusCode.Conflict, "INVALID_LIFECYCLE_TRANSITION");
        using var current = await ReadAsync(buyer, BuyerTenantId, $"marketplace-rfqs/{rfqId}");
        Assert.Equal("EXPIRED", current.RootElement.GetProperty("status").GetString());
        using var history = await ReadAsync(buyer, BuyerTenantId, $"marketplace-rfqs/{rfqId}/responses");
        Assert.Empty(history.RootElement.GetProperty("responses").EnumerateArray());
        return new { rfq = current.RootElement.Clone(), history = history.RootElement.Clone(), lateResponseRejected = true };
    }

    private static async Task<object> ExerciseQuoteRevisionScenarioAsync(
        HttpClient buyer, HttpClient supplier, Guid rfqId, JsonElement first,
        AdjustableMarketplaceClock clock, string id)
    {
        var firstId = first.GetProperty("id").GetGuid();
        var currentId = firstId;
        long version = 1;
        if (id == "TXN-007") clock.Advance(TimeSpan.FromHours(2));
        else
        {
            using var revised = await CommandAsync(supplier, SupplierTenantId,
                $"marketplace-rfqs/{rfqId}/responses", id + ":revision", 1,
                ScenarioQuote(1_180_000, id == "TXN-009" ? "UNAVAILABLE" : "AVAILABLE",
                    clock.GetUtcNow().AddDays(1)));
            currentId = revised.RootElement.GetProperty("response").GetProperty("id").GetGuid();
            version = 2;
            using var stale = await RawCommandAsync(buyer, BuyerTenantId,
                $"marketplace-responses/{firstId}:accept", id + ":old-accept", 1,
                new { reason = "Superseded quote must be rejected." });
            await AssertProblemAsync(stale, HttpStatusCode.Conflict, "INVALID_LIFECYCLE_TRANSITION");
        }
        using var acceptance = await RawCommandAsync(buyer, BuyerTenantId,
            $"marketplace-responses/{currentId}:accept", id + ":accept", version,
            new { reason = "Human evaluates the exact current quote." });
        if (id == "TXN-008") Assert.True(acceptance.IsSuccessStatusCode, await acceptance.Content.ReadAsStringAsync());
        else await AssertProblemAsync(acceptance, HttpStatusCode.Conflict,
            id == "TXN-007" ? "MARKETPLACE_RESPONSE_EXPIRED" : "MARKETPLACE_RESPONSE_UNAVAILABLE");
        using var history = await ReadAsync(buyer, BuyerTenantId, $"marketplace-rfqs/{rfqId}/responses");
        var quotes = history.RootElement.GetProperty("responses").EnumerateArray().ToArray();
        Assert.Equal((int)version, quotes.Length);
        Assert.Equal(firstId, quotes[0].GetProperty("id").GetGuid());
        Assert.Equal(1_250_000, quotes[0].GetProperty("amountMinor").GetInt64());
        Assert.Equal(first.GetProperty("terms").GetString(), quotes[0].GetProperty("terms").GetString());
        using var current = await ReadAsync(buyer, BuyerTenantId, $"marketplace-rfqs/{rfqId}");
        if (id == "TXN-008")
        {
            Assert.Equal("ACCEPTED", current.RootElement.GetProperty("status").GetString());
            Assert.Equal(1_180_000, current.RootElement.GetProperty("negotiation").GetProperty("acceptedAmountMinor").GetInt64());
        }
        return new { rfq = current.RootElement.Clone(), history = history.RootElement.Clone(),
            originalQuote = first, acceptanceStatus = (int)acceptance.StatusCode };
    }
}

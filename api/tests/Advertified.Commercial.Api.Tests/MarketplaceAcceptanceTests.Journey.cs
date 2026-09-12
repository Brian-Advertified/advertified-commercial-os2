using System.Net;
using System.Text.Json;
using Npgsql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class MarketplaceAcceptanceTests
{
    private static readonly string[] HumanVerifiedEvidence =
        ["inventory:human-verified"];

    private static async Task<ListingFixture> CreateAndPublishListingAsync(
        HttpClient supplier, HttpClient buyer)
    {
        using var inventory = await ReadAsync(
            supplier, SupplierTenantId, $"inventory-products/{ProductId}");
        Assert.Equal(1_250_000, inventory.RootElement.GetProperty("rate")
            .GetProperty("amountMinor").GetInt64());
        Assert.Equal("AVAILABLE", inventory.RootElement.GetProperty("availability")
            .GetProperty("status").GetString());
        using var hidden = await ReadAsync(buyer, BuyerTenantId, "marketplace-listings");
        Assert.Empty(hidden.RootElement.GetProperty("items").EnumerateArray());
        using var draft = await CommandAsync(
            supplier, SupplierTenantId, "marketplace-listings", "marketplace-listing-create",
            null, new { productId = ProductId, terms = "Subject to final human booking approval." });
        var listingId = draft.RootElement.GetProperty("id").GetGuid();
        Assert.Equal("DRAFT", draft.RootElement.GetProperty("status").GetString());
        using var published = await CommandAsync(
            supplier, SupplierTenantId, $"marketplace-listings/{listingId}:publish",
            "marketplace-listing-publish", 1, new { });
        var currentVersion = published.RootElement.GetProperty("currentVersion");
        var listingVersionId = currentVersion.GetProperty("id").GetGuid();
        Assert.Equal(RateId, currentVersion.GetProperty("rateId").GetGuid());
        Assert.Equal(AvailabilityId, currentVersion.GetProperty("availabilityId").GetGuid());
        Assert.Equal(1_250_000, currentVersion.GetProperty("amountMinor").GetInt64());
        Assert.Equal("AVAILABLE", currentVersion.GetProperty("availability").GetString());

        using var visible = await ReadAsync(
            buyer, BuyerTenantId, "marketplace-listings?channel=OOH&geography=Johannesburg");
        Assert.Single(visible.RootElement.GetProperty("items").EnumerateArray());
        var projection = visible.RootElement.GetRawText();
        Assert.DoesNotContain("sourceLocator", projection, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("protectedObjectKey", projection, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("address", projection, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("latitude", projection, StringComparison.OrdinalIgnoreCase);
        return new ListingFixture(listingId, listingVersionId);
    }

    private static async Task CompleteAcceptedExchangeAsync(
        HttpClient buyer, HttpClient supplier, HttpClient other,
        Guid listingVersionId, AdjustableMarketplaceClock clock)
    {
        using var created = await CreateRfqAsync(
            buyer, listingVersionId, "September Johannesburg launch", "marketplace-rfq-create",
            clock.GetUtcNow().AddDays(3));
        var rfqId = created.RootElement.GetProperty("id").GetGuid();
        using var sent = await CommandAsync(
            buyer, BuyerTenantId, $"marketplace-rfqs/{rfqId}:send",
            "marketplace-rfq-send", 1, new { reason = "Approved for supplier review." });
        Assert.Equal("SENT", sent.RootElement.GetProperty("status").GetString());

        using var unrelatedRead = await other.GetAsync(
            $"/api/v1/tenants/{OtherTenantId}/marketplace-rfqs/{rfqId}");
        await AssertProblemAsync(unrelatedRead, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");
        using var unrelatedWrite = await RawCommandAsync(
            other, OtherTenantId, $"marketplace-rfqs/{rfqId}/responses",
            "marketplace-other-response", 0, ResponseBody(clock.GetUtcNow().AddDays(1)));
        await AssertProblemAsync(unrelatedWrite, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");
        using var invalidResponse = await RawCommandAsync(
            supplier, SupplierTenantId, $"marketplace-rfqs/{rfqId}/responses",
            "marketplace-invalid-response", 0,
            new
            {
                amountMinor = 1_250_000,
                currency = "ZAR",
                availability = "NOT_A_STATUS",
                terms = "Invalid governed status must fail before persistence.",
                validUntilUtc = clock.GetUtcNow().AddDays(1),
                evidenceReferences = HumanVerifiedEvidence,
            });
        await AssertProblemAsync(invalidResponse, HttpStatusCode.BadRequest, "VALIDATION_FAILED");

        using var response = await CommandAsync(
            supplier, SupplierTenantId, $"marketplace-rfqs/{rfqId}/responses",
            "marketplace-response-submit", 0, new
            {
                amountMinor = 1_250_000,
                currency = "ZAR",
                availability = "UNAVAILABLE",
                terms = "Supplier quote is retained but cannot be accepted while unavailable.",
                validUntilUtc = clock.GetUtcNow().AddDays(1),
                evidenceReferences = HumanVerifiedEvidence,
            });
        var firstResponse = response.RootElement.GetProperty("response");
        var firstResponseId = firstResponse.GetProperty("id").GetGuid();
        Assert.Equal(1, firstResponse.GetProperty("responseVersion").GetInt32());
        using var unavailableAcceptance = await RawCommandAsync(
            buyer, BuyerTenantId, $"marketplace-responses/{firstResponseId}:accept",
            "marketplace-response-unavailable-accept", 1,
            new { reason = "Unavailable supply must not be commercially accepted." });
        await AssertProblemAsync(
            unavailableAcceptance, HttpStatusCode.Conflict, "MARKETPLACE_RESPONSE_UNAVAILABLE");

        using var revised = await CommandAsync(
            supplier, SupplierTenantId, $"marketplace-rfqs/{rfqId}/responses",
            "marketplace-response-revise", 1, new
            {
                amountMinor = 1_180_000,
                currency = "ZAR",
                availability = "AVAILABLE",
                terms = "Revised supplier quote after commercial review; booking still requires buyer acceptance.",
                validUntilUtc = clock.GetUtcNow().AddDays(1),
                evidenceReferences = HumanVerifiedEvidence,
            });
        var latestResponse = revised.RootElement.GetProperty("response");
        var responseId = latestResponse.GetProperty("id").GetGuid();
        Assert.Equal(2, latestResponse.GetProperty("responseVersion").GetInt32());
        Assert.Equal(1_180_000, latestResponse.GetProperty("amountMinor").GetInt64());

        using var history = await ReadAsync(
            buyer, BuyerTenantId, $"marketplace-rfqs/{rfqId}/responses");
        var quotes = history.RootElement.GetProperty("responses").EnumerateArray().ToArray();
        Assert.Equal(2, quotes.Length);
        Assert.Equal([1, 2], quotes.Select(item => item.GetProperty("responseVersion").GetInt32()).ToArray());
        Assert.Equal([1_250_000L, 1_180_000L],
            quotes.Select(item => item.GetProperty("amountMinor").GetInt64()).ToArray());

        using var staleAcceptance = await RawCommandAsync(
            buyer, BuyerTenantId, $"marketplace-responses/{firstResponseId}:accept",
            "marketplace-response-stale-accept", 1,
            new { reason = "A superseded quote must not remain acceptible." });
        await AssertProblemAsync(
            staleAcceptance, HttpStatusCode.Conflict, "INVALID_LIFECYCLE_TRANSITION");

        var acceptancePath = $"marketplace-responses/{responseId}:accept";
        var attempts = await Task.WhenAll(
            RawCommandAsync(buyer, BuyerTenantId, acceptancePath,
                "marketplace-response-accept-a", 2,
                new { reason = "Buyer approved this exact response." }),
            RawCommandAsync(buyer, BuyerTenantId, acceptancePath,
                "marketplace-response-accept-b", 2,
                new { reason = "Concurrent duplicate must fail closed." }));
        using var accepted = attempts.Single(item => item.IsSuccessStatusCode);
        using var rejected = attempts.Single(item => !item.IsSuccessStatusCode);
        await AssertProblemAsync(
            rejected, HttpStatusCode.Conflict, "INVALID_LIFECYCLE_TRANSITION");
        using var acceptedJson = JsonDocument.Parse(
            await accepted.Content.ReadAsStringAsync());
        Assert.Equal("ACCEPTED",
            acceptedJson.RootElement.GetProperty("status").GetString());
        Assert.Equal(BuyerUserId, acceptedJson.RootElement.GetProperty("response")
            .GetProperty("acceptedBy").GetGuid());
        var negotiation = acceptedJson.RootElement.GetProperty("negotiation");
        Assert.Equal(2, negotiation.GetProperty("quoteVersionCount").GetInt32());
        Assert.Equal(1_250_000, negotiation.GetProperty("listedAmountMinor").GetInt64());
        Assert.Equal(1_250_000, negotiation.GetProperty("firstQuoteAmountMinor").GetInt64());
        Assert.Equal(1_180_000, negotiation.GetProperty("currentQuoteAmountMinor").GetInt64());
        Assert.Equal(1_180_000, negotiation.GetProperty("acceptedAmountMinor").GetInt64());
        Assert.Equal(-5.6m, negotiation.GetProperty("firstToCurrentVariancePercent").GetDecimal());
        Assert.Equal(-5.6m, negotiation.GetProperty("firstToAcceptedVariancePercent").GetDecimal());
        Assert.Equal(2, negotiation.GetProperty("acceptedResponseVersion").GetInt32());
        Assert.Equal(JsonValueKind.Null, negotiation.GetProperty("comparabilityLimitation").ValueKind);
    }

    private static async Task AcceptPlanLineRfqAsync(
        HttpClient buyer,
        HttpClient supplier,
        Guid listingVersionId,
        AdjustableMarketplaceClock clock,
        string keyPrefix)
    {
        using var created = await CommandAsync(
            buyer, BuyerTenantId, "marketplace-rfqs", $"{keyPrefix}-create", null,
            new
            {
                listingVersionId,
                subject = "Confirm the exact client-selected plan line",
                requestedStart = new DateOnly(2026, 9, 1),
                requestedEnd = new DateOnly(2026, 9, 30),
                quantity = 1000,
                dueAtUtc = clock.GetUtcNow().AddDays(3),
            });
        var rfqId = created.RootElement.GetProperty("id").GetGuid();
        using var sent = await CommandAsync(
            buyer, BuyerTenantId, $"marketplace-rfqs/{rfqId}:send",
            $"{keyPrefix}-send", 1,
            new { reason = "Request supplier confirmation before funding readiness." });
        using var response = await CommandAsync(
            supplier, SupplierTenantId, $"marketplace-rfqs/{rfqId}/responses",
            $"{keyPrefix}-respond", 0,
            new
            {
                amountMinor = 1_250_000,
                currency = "ZAR",
                availability = "AVAILABLE",
                terms = "Accepted supplier terms for the exact selected plan line.",
                validUntilUtc = clock.GetUtcNow().AddDays(2),
                evidenceReferences = HumanVerifiedEvidence,
            });
        var responseId = response.RootElement.GetProperty("response").GetProperty("id").GetGuid();
        using var accepted = await CommandAsync(
            buyer, BuyerTenantId, $"marketplace-responses/{responseId}:accept",
            $"{keyPrefix}-accept", 1,
            new { reason = "Buyer accepts the exact supplier quote before funding." });
        Assert.Equal("ACCEPTED", accepted.RootElement.GetProperty("status").GetString());
    }

    private static async Task AssertCommercialMemoryAsync(HttpClient buyer)
    {
        using var response = await ReadAsync(
            buyer, BuyerTenantId, "reporting/commercial-memory?channel=OOH");
        var metrics = response.RootElement.GetProperty("metrics").EnumerateArray()
            .ToDictionary(item => item.GetProperty("code").GetString()!);
        var versions = metrics["supplier_quote_versions_per_rfq"];
        Assert.Equal(2m, versions.GetProperty("value").GetDecimal());
        Assert.Equal(1, versions.GetProperty("sampleSize").GetInt32());
        Assert.False(versions.GetProperty("isRobustBenchmark").GetBoolean());
        Assert.Contains("not presented as a robust benchmark",
            versions.GetProperty("limitation").GetString());
        var listedVariance = metrics["listed_to_first_quote_variance_percent"];
        Assert.Equal(0m, listedVariance.GetProperty("value").GetDecimal());
        var negotiatedVariance = metrics["first_to_accepted_quote_variance_percent"];
        Assert.Equal(-5.6m, negotiatedVariance.GetProperty("value").GetDecimal());
        Assert.Equal("marketplace_supplier_response", negotiatedVariance.GetProperty("sources")[0]
            .GetProperty("resourceType").GetString());
        var acceptance = metrics["supplier_quote_acceptance_rate_percent"];
        Assert.Equal(100m, acceptance.GetProperty("value").GetDecimal());
        Assert.Equal(1, acceptance.GetProperty("sources").GetArrayLength());
        Assert.Equal("marketplace_rfq", acceptance.GetProperty("sources")[0]
            .GetProperty("resourceType").GetString());
        var selection = metrics["shortlist_candidate_selection_rate_percent"];
        Assert.True(selection.GetProperty("sampleSize").GetInt32() > 0);
        Assert.All(selection.GetProperty("sources").EnumerateArray(), source =>
            Assert.Equal("inventory_shortlist_version",
                source.GetProperty("resourceType").GetString()));
        var cancellation = metrics["booking_cancellation_rate_percent"];
        Assert.Equal(JsonValueKind.Null, cancellation.GetProperty("value").ValueKind);
        Assert.Contains("does not define a cancellation transition",
            cancellation.GetProperty("limitation").GetString());
        var performance = metrics["campaign_performance_observations_count"];
        Assert.Equal(JsonValueKind.Null, performance.GetProperty("value").ValueKind);
        Assert.Contains("not safely attributable",
            performance.GetProperty("limitation").GetString());
    }

    private static async Task AssertCommercialMemoryBookingConversionAsync(HttpClient buyer)
    {
        using var response = await ReadAsync(
            buyer, BuyerTenantId, "reporting/commercial-memory?channel=OOH");
        var metrics = response.RootElement.GetProperty("metrics").EnumerateArray()
            .ToDictionary(item => item.GetProperty("code").GetString()!);
        Assert.Equal(100m, metrics["accepted_quote_booking_conversion_percent"]
            .GetProperty("value").GetDecimal());
        Assert.Equal(100m, metrics["accepted_quote_booking_confirmation_rate_percent"]
            .GetProperty("value").GetDecimal());
        Assert.Equal(1, metrics["accepted_quote_to_booking_request_hours"]
            .GetProperty("sampleSize").GetInt32());
        Assert.Equal(100m, metrics["proposal_approval_resolution_rate_percent"]
            .GetProperty("value").GetDecimal());
        Assert.Equal(100m, metrics["proposal_approval_success_rate_percent"]
            .GetProperty("value").GetDecimal());
        Assert.Equal(0.5m, metrics["proposal_approval_resolution_time_hours"]
            .GetProperty("value").GetDecimal());
        Assert.Equal(100m, metrics["proposal_decision_selection_rate_percent"]
            .GetProperty("value").GetDecimal());
        Assert.Equal(0.75m, metrics["proposal_share_to_decision_hours"]
            .GetProperty("value").GetDecimal());
    }

    private static async Task AssertExpiredResponseCannotBeAcceptedAsync(
        HttpClient buyer, HttpClient supplier, Guid listingVersionId,
        AdjustableMarketplaceClock clock)
    {
        using var created = await CreateRfqAsync(
            buyer, listingVersionId, "Time-sensitive availability check",
            "marketplace-expired-rfq-create", clock.GetUtcNow().AddDays(2));
        var rfqId = created.RootElement.GetProperty("id").GetGuid();
        using var sent = await CommandAsync(
            buyer, BuyerTenantId, $"marketplace-rfqs/{rfqId}:send",
            "marketplace-expired-rfq-send", 1, new { reason = "Send for timed response." });
        using var response = await CommandAsync(
            supplier, SupplierTenantId, $"marketplace-rfqs/{rfqId}/responses",
            "marketplace-expired-response", 0, ResponseBody(clock.GetUtcNow().AddHours(1)));
        var responseId = response.RootElement.GetProperty("response").GetProperty("id").GetGuid();
        clock.Advance(TimeSpan.FromHours(2));
        using var acceptance = await RawCommandAsync(
            buyer, BuyerTenantId, $"marketplace-responses/{responseId}:accept",
            "marketplace-expired-accept", 1, new { reason = "Should be rejected." });
        await AssertProblemAsync(
            acceptance, HttpStatusCode.Conflict, "MARKETPLACE_RESPONSE_EXPIRED");
    }

    private static async Task AssertFilteredRequestPagingAsync(
        HttpClient buyer,
        Guid listingVersionId,
        AdjustableMarketplaceClock clock)
    {
        using var newerDraftOne = await CreateRfqAsync(
            buyer, listingVersionId, "Newer draft one",
            "marketplace-filter-draft-one", clock.GetUtcNow().AddDays(3));
        using var newerDraftTwo = await CreateRfqAsync(
            buyer, listingVersionId, "Newer draft two",
            "marketplace-filter-draft-two", clock.GetUtcNow().AddDays(3));

        using var accepted = await ReadAsync(
            buyer, BuyerTenantId, "marketplace-rfqs?status=ACCEPTED&pageSize=1");
        var items = accepted.RootElement.GetProperty("items").EnumerateArray().ToArray();
        Assert.Single(items);
        Assert.Equal("ACCEPTED", items[0].GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null,
            accepted.RootElement.GetProperty("nextCursor").ValueKind);
    }

    private static async Task AssertInvalidMarketplaceFiltersAsync(HttpClient buyer)
    {
        using var invalidChannel = await buyer.GetAsync(
            $"/api/v1/tenants/{BuyerTenantId}/marketplace-listings?channel=NOT_A_CHANNEL");
        await AssertProblemAsync(
            invalidChannel, HttpStatusCode.BadRequest, "VALIDATION_FAILED");
        using var invalidStatus = await buyer.GetAsync(
            $"/api/v1/tenants/{BuyerTenantId}/marketplace-rfqs?status=NOT_A_STATUS");
        await AssertProblemAsync(
            invalidStatus, HttpStatusCode.BadRequest, "VALIDATION_FAILED");
    }

    private static Task<JsonDocument> CreateRfqAsync(
        HttpClient buyer, Guid listingVersionId, string subject, string key,
        DateTimeOffset dueAtUtc) => CommandAsync(
            buyer, BuyerTenantId, "marketplace-rfqs", key, null,
            new
            {
                listingVersionId,
                subject,
                requestedStart = new DateOnly(2026, 9, 15),
                requestedEnd = new DateOnly(2026, 10, 15),
                quantity = 1,
                dueAtUtc,
            });

    private static object ResponseBody(DateTimeOffset validUntilUtc) => new
    {
        amountMinor = 1_250_000,
        currency = "ZAR",
        availability = "AVAILABLE",
        terms = "Rate remains subject to a separate human-approved booking.",
        validUntilUtc,
        evidenceReferences = HumanVerifiedEvidence,
    };

    private static async Task ArchiveListingAsync(
        HttpClient supplier, HttpClient buyer, Guid listingId)
    {
        using var stale = await RawCommandAsync(
            supplier, SupplierTenantId, $"marketplace-listings/{listingId}:archive",
            "marketplace-listing-stale-archive", 1, new { reason = "Stale request." });
        await AssertProblemAsync(stale, HttpStatusCode.Conflict, "VERSION_CONFLICT");
        using var archived = await CommandAsync(
            supplier, SupplierTenantId, $"marketplace-listings/{listingId}:archive",
            "marketplace-listing-archive", 2, new { reason = "Supplier withdrew availability." });
        Assert.Equal("ARCHIVED", archived.RootElement.GetProperty("status").GetString());
        using var hidden = await ReadAsync(buyer, BuyerTenantId, "marketplace-listings");
        Assert.Empty(hidden.RootElement.GetProperty("items").EnumerateArray());
    }

    private static async Task AssertRetainedEvidenceAsync(
        string connectionString, Guid listingVersionId, int expectedCommands)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        Assert.Equal(expectedCommands, await CountAsync(connection,
            "SELECT count(*)::integer FROM commercial.audit_events WHERE action_code LIKE 'marketplace_%'"));
        Assert.Equal(expectedCommands, await CountAsync(connection,
            "SELECT count(*)::integer FROM commercial.outbox_messages WHERE event_type_code LIKE 'Marketplace%'"));
        await using var mutate = new NpgsqlCommand(
            "UPDATE commercial.marketplace_listing_versions SET terms = 'changed' WHERE id = $1",
            connection);
        mutate.Parameters.AddWithValue(listingVersionId);
        var exception = await Assert.ThrowsAsync<PostgresException>(mutate.ExecuteNonQueryAsync);
        Assert.Equal(PostgresErrorCodes.RaiseException, exception.SqlState);
    }

    private static async Task<int> CountAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return (int)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Marketplace evidence count was unavailable."));
    }

    private sealed record ListingFixture(Guid ListingId, Guid ListingVersionId);
}

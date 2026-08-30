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
            "marketplace-other-response", null, ResponseBody(clock.GetUtcNow().AddDays(1)));
        await AssertProblemAsync(unrelatedWrite, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");
        using var invalidResponse = await RawCommandAsync(
            supplier, SupplierTenantId, $"marketplace-rfqs/{rfqId}/responses",
            "marketplace-invalid-response", null,
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
            "marketplace-response-submit", null, ResponseBody(clock.GetUtcNow().AddDays(1)));
        var responseId = response.RootElement.GetProperty("response").GetProperty("id").GetGuid();
        var acceptancePath = $"marketplace-responses/{responseId}:accept";
        var attempts = await Task.WhenAll(
            RawCommandAsync(buyer, BuyerTenantId, acceptancePath,
                "marketplace-response-accept-a", 1,
                new { reason = "Buyer approved this exact response." }),
            RawCommandAsync(buyer, BuyerTenantId, acceptancePath,
                "marketplace-response-accept-b", 1,
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
            "marketplace-expired-response", null, ResponseBody(clock.GetUtcNow().AddHours(1)));
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

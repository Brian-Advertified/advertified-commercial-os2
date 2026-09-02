using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Advertified.Commercial.Application.EmailAutomation;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.EmailAutomation;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class CanonicalPlanningAcceptanceTests
{
    private const string AudienceFieldPath = "audiences";
    private static readonly string[] OohAutomationChannels = ["OOH", "DOOH"];
    private static readonly string[] AllowedClientSenderDomains = ["client.example"];

    [Fact]
    [Trait("Category", "Migration")]
    public async Task ConfiguredOohInboxCreatesStpPlanPdfAndSendsWithoutUserInput()
    {
        await using var postgres = CreatePostgres();
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DisposableDatabaseRoles.ProvisionAsync(connectionString);
        await SeedAsync(connectionString);
        await using var factory = CreateFactory(
            connectionString, OperatorId, enableEmailAutomation: true,
            configureServices: ConfigureDeterministicEmailInventorySelection);
        await using var reviewerFactory = CreateFactory(connectionString, ReviewerId);
        using var client = factory.CreateClient();
        using var reviewer = reviewerFactory.CreateClient();
        var provider = factory.Services.GetRequiredService<DeterministicEmailProviderClient>();

        await ConfigureMailboxAsync(client);
        var receivedAt = DateTimeOffset.UtcNow;
        provider.Register(CreateEmail(
            "email-ooh-001",
            "message-ooh-001",
            OohBriefBody,
            receivedAt));

        using var receipt = await SendWebhookAsync(
            client, "event-ooh-001", "email-ooh-001");
        var inboundEmailId = receipt.RootElement.GetProperty("inboundEmailId").GetGuid();
        using var detail = await GetJsonAsync(client, Path(
            $"email-automation/messages/{inboundEmailId}"));
        Assert.True(receipt.RootElement.GetProperty("status").GetString() == "SENT",
            detail.RootElement.GetRawText());
        Assert.False(receipt.RootElement.GetProperty("duplicate").GetBoolean());
        var automationRunId = receipt.RootElement.GetProperty("automationRunId").GetGuid();

        var run = detail.RootElement.GetProperty("run");
        Assert.Equal(automationRunId, run.GetProperty("id").GetGuid());
        Assert.Equal("OOH_ONLY", run.GetProperty("campaignMode").GetString());
        Assert.Equal("SENT", run.GetProperty("status").GetString());
        Assert.Equal("SENT", run.GetProperty("checkpoint").GetString());
        Assert.True(run.GetProperty("briefVersionId").GetGuid() != Guid.Empty);
        Assert.True(run.GetProperty("stpVersionId").GetGuid() != Guid.Empty);
        Assert.True(run.GetProperty("mediaMixVersionId").GetGuid() != Guid.Empty);
        Assert.True(run.GetProperty("shortlistVersionId").GetGuid() != Guid.Empty);
        Assert.True(run.GetProperty("mediaPlanVersionId").GetGuid() != Guid.Empty);
        Assert.True(run.GetProperty("proposalVersionId").GetGuid() != Guid.Empty);
        Assert.True(run.GetProperty("documentId").GetGuid() != Guid.Empty);
        Assert.Equal(0, run.GetProperty("incrementalAiCostMinor").GetInt64());
        var clientAccountId = run.GetProperty("clientAccountId").GetGuid();
        Assert.NotEqual(Guid.Empty, clientAccountId);
        await AssertCreatedClientAsync(connectionString, clientAccountId);

        var briefVersionId = run.GetProperty("briefVersionId").GetGuid();
        using var planning = await GetJsonAsync(client, Path(
            $"brief-versions/{briefVersionId}/planning"));
        Assert.Equal("OOH_ONLY", planning.RootElement.GetProperty("campaignMode")
            .GetProperty("mode").GetString());
        var stp = planning.RootElement.GetProperty("audience");
        Assert.NotEmpty(stp.GetProperty("definitions").EnumerateArray());
        Assert.False(string.IsNullOrWhiteSpace(
            stp.GetProperty("targetingRationale").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(
            stp.GetProperty("positioningStatement").GetString()));
        Assert.All(planning.RootElement.GetProperty("mediaMix").GetProperty("allocations")
            .EnumerateArray(), allocation => Assert.Contains(
                allocation.GetProperty("channel").GetString(), OohAutomationChannels));

        var delivery = Assert.Single(provider.Deliveries);
        Assert.Equal("brief@client.example", delivery.To);
        Assert.Equal("proposals@advertified.test", delivery.From);
        Assert.Equal("message-ooh-001", delivery.InReplyTo);
        Assert.StartsWith("application/pdf", delivery.MediaType, StringComparison.Ordinal);
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(delivery.Attachment),
            StringComparison.Ordinal);
        await AssertAutomationConsequencesAsync(connectionString, automationRunId);
        await AssertExternalDecisionRecordingAsync(client, reviewer, run);

        using var duplicate = await SendWebhookAsync(
            client, "event-ooh-duplicate", "email-ooh-001");
        Assert.True(duplicate.RootElement.GetProperty("duplicate").GetBoolean());
        Assert.Equal(inboundEmailId,
            duplicate.RootElement.GetProperty("inboundEmailId").GetGuid());
        Assert.Single(provider.Deliveries);

        provider.Register(CreateEmail(
            "email-multichannel-001",
            "message-multichannel-001",
            MultiChannelBriefBody,
            receivedAt));
        using var rejected = await SendWebhookAsync(
            client, "event-multichannel-001", "email-multichannel-001");
        Assert.Equal("REVIEW_REQUIRED",
            rejected.RootElement.GetProperty("status").GetString());
        var rejectedId = rejected.RootElement.GetProperty("inboundEmailId").GetGuid();
        using var rejectedDetail = await GetJsonAsync(client, Path(
            $"email-automation/messages/{rejectedId}"));
        Assert.Equal("NON_OOH_REQUEST", rejectedDetail.RootElement.GetProperty("run")
            .GetProperty("failureCode").GetString());
        Assert.Single(provider.Deliveries);

        provider.Register(CreateEmail(
            "email-incomplete-001",
            "message-incomplete-001",
            IncompleteBriefBody,
            receivedAt));
        using var incomplete = await SendWebhookAsync(
            client, "event-incomplete-001", "email-incomplete-001");
        Assert.Equal("REVIEW_REQUIRED",
            incomplete.RootElement.GetProperty("status").GetString());
        var incompleteId = incomplete.RootElement.GetProperty("inboundEmailId").GetGuid();
        using var incompleteDetail = await GetJsonAsync(client, Path(
            $"email-automation/messages/{incompleteId}"));
        var incompleteRun = incompleteDetail.RootElement.GetProperty("run");
        Assert.Equal("INCOMPLETE_BRIEF",
            incompleteRun.GetProperty("failureCode").GetString());
        Assert.Contains(incompleteDetail.RootElement.GetProperty("questions").EnumerateArray(),
            question => question.GetProperty("fieldPath").GetString() == AudienceFieldPath);

        using var firstPage = await GetJsonAsync(
            client, Path("email-automation/messages?pageSize=2"));
        var firstItems = firstPage.RootElement.GetProperty("items")
            .EnumerateArray().ToArray();
        Assert.Equal(2, firstItems.Length);
        var nextCursor = firstPage.RootElement.GetProperty("nextCursor").GetString();
        Assert.False(string.IsNullOrWhiteSpace(nextCursor));
        using var secondPage = await GetJsonAsync(client, Path(
            $"email-automation/messages?pageSize=2&cursor={Uri.EscapeDataString(nextCursor!)}"));
        var messageIds = firstItems.Select(item => item.GetProperty("id").GetGuid())
            .Concat(secondPage.RootElement.GetProperty("items").EnumerateArray()
                .Select(item => item.GetProperty("id").GetGuid()))
            .ToHashSet();
        Assert.Equal(3, messageIds.Count);
        Assert.Contains(inboundEmailId, messageIds);
        Assert.Contains(rejectedId, messageIds);
        Assert.Contains(incompleteId, messageIds);

        using var corrected = await RetryWithClarificationAsync(
            client,
            incompleteId,
            incompleteRun.GetProperty("version").GetInt64(),
            AudienceFieldPath,
            "Local business decision makers");
        Assert.Equal("SENT", corrected.RootElement.GetProperty("status").GetString());
        Assert.Equal(2, provider.Deliveries.Count);
    }

    private static async Task ConfigureMailboxAsync(HttpClient client)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, Path("email-automation/mailbox"))
        {
            Content = JsonContent.Create(new
            {
                address = "ooh@planning.example",
                provider = "DETERMINISTIC",
                ownerUserId = OperatorId,
                defaultClientAccountId = (Guid?)null,
                autoSendEnabled = true,
                allowedSenderDomains = AllowedClientSenderDomains,
            }),
        };
        request.Headers.Add("Idempotency-Key", "configure-ooh-email-automation");
        request.Headers.Add("X-Correlation-ID", Guid.NewGuid().ToString());
        using var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode,
            $"Mailbox configuration returned {(int)response.StatusCode}: {content}");
    }

    private static async Task AssertCreatedClientAsync(
        string connectionString,
        Guid clientAccountId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT trading_name FROM commercial.client_accounts " +
            "WHERE tenant_id = @tenantId AND id = @clientId",
            connection);
        command.Parameters.AddWithValue("tenantId", TenantId);
        command.Parameters.AddWithValue("clientId", clientAccountId);
        Assert.Equal("Email OOH Client", await command.ExecuteScalarAsync());
    }

    private static async Task AssertAutomationConsequencesAsync(
        string connectionString,
        Guid automationRunId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var audit = new NpgsqlCommand(
            "SELECT count(*) FROM commercial.audit_events " +
            "WHERE tenant_id = @tenantId AND resource_type_code = @resourceType " +
            "AND resource_id = @runId AND action_code = ANY(@actions)",
            connection);
        audit.Parameters.AddWithValue("tenantId", TenantId);
        audit.Parameters.AddWithValue(
            "resourceType",
            MasterDataCodes.CommercialResourceTypes.EmailProposalAutomationRun);
        audit.Parameters.AddWithValue("runId", automationRunId);
        audit.Parameters.AddWithValue("actions", new[] {
            MasterDataCodes.CommercialActions.EmailAutomationStarted,
            MasterDataCodes.CommercialActions.EmailAutomationSent,
        });
        Assert.Equal(2L, (long)(await audit.ExecuteScalarAsync())!);

        await using var outbox = new NpgsqlCommand(
            "SELECT count(*) FROM commercial.outbox_messages " +
            "WHERE tenant_id = @tenantId AND aggregate_type_code = @resourceType " +
            "AND aggregate_id = @runId AND event_type_code = ANY(@events)",
            connection);
        outbox.Parameters.AddWithValue("tenantId", TenantId);
        outbox.Parameters.AddWithValue(
            "resourceType",
            MasterDataCodes.CommercialResourceTypes.EmailProposalAutomationRun);
        outbox.Parameters.AddWithValue("runId", automationRunId);
        outbox.Parameters.AddWithValue("events", new[] {
            MasterDataCodes.CommercialEventTypes.EmailProposalAutomationStarted,
            MasterDataCodes.CommercialEventTypes.EmailProposalAutomationSent,
        });
        Assert.Equal(2L, (long)(await outbox.ExecuteScalarAsync())!);
    }

    private static RetrievedInboundEmail CreateEmail(
        string providerEmailId,
        string providerMessageId,
        string body,
        DateTimeOffset receivedAt) => new(
            providerEmailId,
            providerMessageId,
            ["ooh@planning.example"],
            "brief@client.example",
            "Client Planner",
            ["brief@client.example"],
            "Johannesburg OOH request",
            body,
            null,
            new Dictionary<string, string>(),
            Array.Empty<InboundAttachmentReference>(),
            receivedAt);

    private static async Task<JsonDocument> RetryWithClarificationAsync(
        HttpClient client,
        Guid inboundEmailId,
        long version,
        string fieldPath,
        string value)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            Path($"email-automation/messages/{inboundEmailId}:retry"))
        {
            Content = JsonContent.Create(new
            {
                reason = "Apply the confirmed missing Brief detail.",
                clarifications = new[] { new { fieldPath, value } },
            }),
        };
        request.Headers.Add("Idempotency-Key", $"retry-{inboundEmailId:N}");
        request.Headers.Add("X-Correlation-ID", Guid.NewGuid().ToString());
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        using var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode,
            $"Retry returned {(int)response.StatusCode}: {content}");
        return JsonDocument.Parse(content);
    }

    private static async Task<JsonDocument> SendWebhookAsync(
        HttpClient client,
        string eventId,
        string emailId)
    {
        var payload = JsonSerializer.Serialize(new
        {
            type = "email.received",
            data = new { email_id = emailId },
        });
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            .ToString(CultureInfo.InvariantCulture);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/tenants/{TenantId}/email-automation/webhooks/DETERMINISTIC")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("svix-id", eventId);
        request.Headers.TryAddWithoutValidation("svix-timestamp", timestamp);
        request.Headers.TryAddWithoutValidation(
            "svix-signature", CreateSignature(eventId, timestamp, payload));
        using var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode,
            $"Inbound webhook returned {(int)response.StatusCode}: {content}");
        return JsonDocument.Parse(content);
    }

    private static string CreateSignature(
        string eventId,
        string timestamp,
        string payload)
    {
        var secret = Convert.FromBase64String(
            EmailWebhookSecret["whsec_".Length..]);
        var signed = Encoding.UTF8.GetBytes(
            string.Concat(eventId, ".", timestamp, ".", payload));
        var signature = HMACSHA256.HashData(secret, signed);
        return string.Concat("v1,", Convert.ToBase64String(signature));
    }

    private static async Task<JsonDocument> GetJsonAsync(
        HttpClient client,
        string path)
    {
        using var response = await client.GetAsync(path);
        var content = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode,
            $"GET {path} returned {(int)response.StatusCode}: {content}");
        return JsonDocument.Parse(content);
    }

    private const string OohBriefBody = """
        Client: Email OOH Client
        Objective: Increase qualified enquiries
        Audience: Local business decision makers
        Geography: Johannesburg
        Timing: 2026-09-01 to 2026-09-30
        Budget: R10 000
        Media: OOH billboard
        Measurement: Qualified enquiries
        Constraints: OOH only
        Client is VAT registered.
        """;

    private const string IncompleteBriefBody = """
        Client: Email OOH Client
        Objective: Increase qualified enquiries
        Geography: Johannesburg
        Timing: 2026-09-01 to 2026-09-30
        Budget: R10 000
        Media: OOH billboard
        Measurement: Qualified enquiries
        Constraints: OOH only
        Client is VAT registered.
        """;

    private const string MultiChannelBriefBody = """
        Client: Email OOH Client
        Objective: Increase qualified enquiries
        Audience: Local business decision makers
        Geography: Johannesburg
        Timing: 2026-09-01 to 2026-09-30
        Budget: R10 000
        Media: OOH billboard, Radio
        Measurement: Qualified enquiries
        Constraints: Include radio support
        Client is VAT registered.
        """;
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Advertified.Commercial.Application.EmailAutomation;
using Advertified.Commercial.Infrastructure.EmailAutomation;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class CanonicalPlanningAcceptanceTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Category", "Migration")]
    public async Task AmbiguousDeliveryAfterRestartNeverSubmitsAgain(
        bool providerCanConfirmAcceptance)
    {
        await using var postgres = CreatePostgres();
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DisposableDatabaseRoles.ProvisionAsync(connectionString);
        await SeedAsync(connectionString);
        var reconciliation = providerCanConfirmAcceptance
            ? EmailDeliveryReconciliationOutcome.Accepted
            : EmailDeliveryReconciliationOutcome.Unknown;
        var ledger = new AmbiguousEmailDeliveryLedger(reconciliation);
        var suffix = providerCanConfirmAcceptance ? "accepted" : "unknown";

        Guid inboundEmailId;
        Guid runId;
        Guid proposalId;
        long ambiguousVersion;
        DateTimeOffset requestedAt;
        await using (var firstFactory = CreateDurabilityFactory(connectionString, ledger))
        {
            using var firstClient = firstFactory.CreateClient();
            await ConfigureMailboxAsync(firstClient);
            var inbound = firstFactory.Services
                .GetRequiredService<DeterministicEmailProviderClient>();
            inbound.Register(CreateEmail(
                $"email-durability-{suffix}",
                $"message-durability-{suffix}",
                OohBriefBody,
                DateTimeOffset.UtcNow));
            using var receipt = await SendWebhookAsync(
                firstClient,
                $"event-durability-{suffix}",
                $"email-durability-{suffix}");
            Assert.Equal("REVIEW_REQUIRED",
                receipt.RootElement.GetProperty("status").GetString());
            inboundEmailId = receipt.RootElement.GetProperty("inboundEmailId").GetGuid();

            using var detail = await GetJsonAsync(
                firstClient, Path($"email-automation/messages/{inboundEmailId}"));
            var run = detail.RootElement.GetProperty("run");
            runId = run.GetProperty("id").GetGuid();
            proposalId = run.GetProperty("proposalVersionId").GetGuid();
            ambiguousVersion = run.GetProperty("version").GetInt64();
            requestedAt = run.GetProperty("deliveryRequestedAtUtc").GetDateTimeOffset();
            AssertAmbiguousRun(run);
            await AssertProposalStatusAsync(connectionString, proposalId, "APPROVED");
            await AssertRetryBlockedAsync(
                firstClient, inboundEmailId, ambiguousVersion, suffix);
        }

        Assert.Equal(1, ledger.SendAttempts);
        Assert.Equal(0, ledger.ReconciliationAttempts);
        await using var secondFactory = CreateDurabilityFactory(connectionString, ledger);
        using var secondClient = secondFactory.CreateClient();
        using var processed = await ProcessMessageAsync(
            secondClient, inboundEmailId, $"process-durability-{suffix}");
        var recovered = processed.RootElement;

        Assert.Equal(1, ledger.SendAttempts);
        Assert.True(
            ledger.ReconciliationAttempts == 1,
            $"Expected one reconciliation call. Recovered run: {recovered.GetRawText()}");
        Assert.Equal(requestedAt,
            recovered.GetProperty("deliveryRequestedAtUtc").GetDateTimeOffset());
        if (providerCanConfirmAcceptance)
        {
            Assert.Equal("SENT", recovered.GetProperty("status").GetString());
            Assert.Equal("SENT", recovered.GetProperty("checkpoint").GetString());
            Assert.Equal(ledger.Receipt.ProviderMessageId,
                recovered.GetProperty("deliveryProviderId").GetString());
            Assert.Equal(ledger.Receipt.AcceptedAtUtc,
                recovered.GetProperty("deliveryAcceptedAtUtc").GetDateTimeOffset());
            await AssertProposalStatusAsync(connectionString, proposalId, "SENT");

            var completedVersion = recovered.GetProperty("version").GetInt64();
            using var repeated = await ProcessMessageAsync(
                secondClient, inboundEmailId, $"repeat-durability-{suffix}");
            Assert.Equal(completedVersion,
                repeated.RootElement.GetProperty("version").GetInt64());
            Assert.Equal(1, ledger.ReconciliationAttempts);
        }
        else
        {
            AssertAmbiguousRun(recovered);
            await AssertRetryBlockedAsync(
                secondClient,
                inboundEmailId,
                recovered.GetProperty("version").GetInt64(),
                $"restart-{suffix}");
            await AssertProposalStatusAsync(connectionString, proposalId, "APPROVED");
        }

        await AssertDeliveryConsequencesAsync(
            connectionString, runId,
            accepted: providerCanConfirmAcceptance,
            sent: providerCanConfirmAcceptance);
    }

    [Fact]
    [Trait("Category", "Migration")]
    public async Task AcceptedDeliveryWithLocalFinalizationFailureRemainsAccepted()
    {
        await using var postgres = CreatePostgres();
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DisposableDatabaseRoles.ProvisionAsync(connectionString);
        await SeedAsync(connectionString);
        var ledger = new AmbiguousEmailDeliveryLedger(
            EmailDeliveryReconciliationOutcome.Accepted);

        Guid inboundEmailId;
        Guid runId;
        Guid proposalId;
        DateTimeOffset requestedAt;
        await using (var firstFactory = CreateDurabilityFactory(connectionString, ledger))
        {
            using var firstClient = firstFactory.CreateClient();
            await ConfigureMailboxAsync(firstClient);
            var inbound = firstFactory.Services
                .GetRequiredService<DeterministicEmailProviderClient>();
            inbound.Register(CreateEmail(
                "email-accepted-local-failure",
                "message-accepted-local-failure",
                OohBriefBody,
                DateTimeOffset.UtcNow));
            using var receipt = await SendWebhookAsync(
                firstClient,
                "event-accepted-local-failure",
                "email-accepted-local-failure");
            inboundEmailId = receipt.RootElement.GetProperty("inboundEmailId").GetGuid();
            using var detail = await GetJsonAsync(
                firstClient, Path($"email-automation/messages/{inboundEmailId}"));
            var run = detail.RootElement.GetProperty("run");
            runId = run.GetProperty("id").GetGuid();
            proposalId = run.GetProperty("proposalVersionId").GetGuid();
            requestedAt = run.GetProperty("deliveryRequestedAtUtc").GetDateTimeOffset();
            AssertAmbiguousRun(run);
        }

        await SetProposalStatusAsync(connectionString, proposalId, "DRAFT");
        await using var secondFactory = CreateDurabilityFactory(connectionString, ledger);
        using var secondClient = secondFactory.CreateClient();
        using var processed = await ProcessMessageAsync(
            secondClient, inboundEmailId, "process-accepted-local-failure");
        var recovered = processed.RootElement;

        Assert.Equal("REVIEW_REQUIRED", recovered.GetProperty("status").GetString());
        Assert.Equal("DELIVERY_ACCEPTED", recovered.GetProperty("checkpoint").GetString());
        Assert.Equal("DELIVERY_RECORDING_REQUIRED",
            recovered.GetProperty("failureCode").GetString());
        Assert.Equal(requestedAt,
            recovered.GetProperty("deliveryRequestedAtUtc").GetDateTimeOffset());
        Assert.Equal(ledger.Receipt.ProviderMessageId,
            recovered.GetProperty("deliveryProviderId").GetString());
        Assert.Equal(ledger.Receipt.AcceptedAtUtc,
            recovered.GetProperty("deliveryAcceptedAtUtc").GetDateTimeOffset());
        Assert.Equal(1, ledger.SendAttempts);
        Assert.Equal(1, ledger.ReconciliationAttempts);
        await AssertRetryBlockedAsync(
            secondClient, inboundEmailId,
            recovered.GetProperty("version").GetInt64(),
            "accepted-local-failure");
        await AssertProposalStatusAsync(connectionString, proposalId, "DRAFT");
        await AssertDeliveryConsequencesAsync(
            connectionString, runId, accepted: true, sent: false);
    }

    [Fact]
    [Trait("Category", "Migration")]
    public async Task ConcurrentFailureCannotDowngradePersistedDeliveryIntent()
    {
        await using var postgres = CreatePostgres();
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DisposableDatabaseRoles.ProvisionAsync(connectionString);
        await SeedAsync(connectionString);
        var ledger = new AmbiguousEmailDeliveryLedger(
            EmailDeliveryReconciliationOutcome.Unknown);
        await using var factory = CreateStaleContextFactory(connectionString, ledger);
        using var client = factory.CreateClient();
        await ConfigureMailboxAsync(client);
        var inbound = factory.Services.GetRequiredService<DeterministicEmailProviderClient>();
        var resolver = factory.Services.GetRequiredService<StaleContextEmailProviderResolver>();
        inbound.Register(CreateEmail(
            "email-concurrent-intent",
            "message-concurrent-intent",
            OohBriefBody,
            DateTimeOffset.UtcNow));
        using var receipt = await SendWebhookAsync(
            client, "event-concurrent-intent", "email-concurrent-intent");
        Assert.Equal("FAILED", receipt.RootElement.GetProperty("status").GetString());
        var inboundEmailId = receipt.RootElement.GetProperty("inboundEmailId").GetGuid();
        using var preparedDetail = await GetJsonAsync(
            client, Path($"email-automation/messages/{inboundEmailId}"));
        var preparedRun = preparedDetail.RootElement.GetProperty("run");
        Assert.Equal("DOCUMENT_RENDERED", preparedRun.GetProperty("checkpoint").GetString());
        var runId = preparedRun.GetProperty("id").GetGuid();
        await ResetRunForConcurrentProcessingAsync(connectionString, inboundEmailId);

        var staleProcess = ProcessMessageAsync(
            client, inboundEmailId, "process-concurrent-stale");
        Assert.True(resolver.WaitForBlockedCall(TimeSpan.FromSeconds(30)));
        try
        {
            using var winningProcess = await ProcessMessageAsync(
                client, inboundEmailId, "process-concurrent-winner");
            AssertAmbiguousRun(winningProcess.RootElement);
        }
        finally
        {
            resolver.ReleaseBlockedCall();
        }
        using var staleResult = await staleProcess;
        AssertAmbiguousRun(staleResult.RootElement);

        using var finalDetail = await GetJsonAsync(
            client, Path($"email-automation/messages/{inboundEmailId}"));
        var finalRun = finalDetail.RootElement.GetProperty("run");
        AssertAmbiguousRun(finalRun);
        Assert.Equal(1, ledger.SendAttempts);
        Assert.Equal(0, ledger.ReconciliationAttempts);
        await AssertRetryBlockedAsync(
            client, inboundEmailId,
            finalRun.GetProperty("version").GetInt64(),
            "concurrent-intent");
        await AssertDeliveryConsequencesAsync(
            connectionString, runId, accepted: false, sent: false);
    }

    private static WebApplicationFactory<Program> CreateDurabilityFactory(
        string connectionString,
        AmbiguousEmailDeliveryLedger ledger) =>
        CreateFactory(
            connectionString,
            OperatorId,
            enableEmailAutomation: true,
            services =>
            {
                ConfigureDeterministicEmailInventorySelection(services);
                services.RemoveAll<IEmailProviderClient>();
                services.AddSingleton<IEmailProviderClient>(provider =>
                    new AmbiguousEmailProviderClient(
                        provider.GetRequiredService<DeterministicEmailProviderClient>(),
                        ledger));
            });

    private static WebApplicationFactory<Program> CreateStaleContextFactory(
        string connectionString,
        AmbiguousEmailDeliveryLedger ledger) =>
        CreateFactory(
            connectionString,
            OperatorId,
            enableEmailAutomation: true,
            services =>
            {
                ConfigureDeterministicEmailInventorySelection(services);
                services.RemoveAll<IEmailProviderClient>();
                services.AddSingleton<IEmailProviderClient>(provider =>
                    new AmbiguousEmailProviderClient(
                        provider.GetRequiredService<DeterministicEmailProviderClient>(),
                        ledger));
                services.RemoveAll<IEmailProviderResolver>();
                services.AddSingleton<StaleContextEmailProviderResolver>();
                services.AddSingleton<IEmailProviderResolver>(provider =>
                    provider.GetRequiredService<StaleContextEmailProviderResolver>());
            });

    private static void AssertAmbiguousRun(JsonElement run)
    {
        Assert.Equal("REVIEW_REQUIRED", run.GetProperty("status").GetString());
        Assert.Equal("DELIVERY_REQUESTED", run.GetProperty("checkpoint").GetString());
        Assert.Equal("DELIVERY_AMBIGUOUS", run.GetProperty("failureCode").GetString());
        Assert.Equal("DETERMINISTIC", run.GetProperty("deliveryProviderCode").GetString());
        Assert.Equal(JsonValueKind.Null, run.GetProperty("deliveryProviderId").ValueKind);
        Assert.Equal(JsonValueKind.Null, run.GetProperty("deliveryAcceptedAtUtc").ValueKind);
    }

    private static async Task<JsonDocument> ProcessMessageAsync(
        HttpClient client,
        Guid inboundEmailId,
        string requestKey)
    {
        using var detail = await GetJsonAsync(
            client, Path($"email-automation/messages/{inboundEmailId}"));
        var version = detail.RootElement.GetProperty("run").GetProperty("version").GetInt64();
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            Path($"email-automation/messages/{inboundEmailId}:process"));
        request.Headers.Add("Idempotency-Key", requestKey);
        request.Headers.Add("X-Correlation-ID", Guid.NewGuid().ToString());
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        using var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode,
            $"Process returned {(int)response.StatusCode}: {content}");
        return JsonDocument.Parse(content);
    }

    private static async Task AssertRetryBlockedAsync(
        HttpClient client,
        Guid inboundEmailId,
        long version,
        string suffix)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            Path($"email-automation/messages/{inboundEmailId}:retry"))
        {
            Content = JsonContent.Create(new
            {
                reason = "Do not repeat a delivery whose acceptance is unknown.",
            }),
        };
        request.Headers.Add("Idempotency-Key", $"retry-ambiguous-{suffix}");
        request.Headers.Add("X-Correlation-ID", Guid.NewGuid().ToString());
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("EMAIL_AUTOMATION_NOT_RETRYABLE",
            problem.RootElement.GetProperty("code").GetString());
    }
}

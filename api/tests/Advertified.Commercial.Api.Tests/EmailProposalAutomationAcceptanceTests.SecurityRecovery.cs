using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Advertified.Commercial.Application.EmailAutomation;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.EmailAutomation;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class CanonicalPlanningAcceptanceTests
{
    [Fact]
    [Trait("Category", "Migration")]
    public async Task UntrustedReplyAddressIsPreservedButNeverUsedForAutomaticDelivery()
    {
        await using var postgres = CreatePostgres();
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DisposableDatabaseRoles.ProvisionAsync(connectionString);
        await SeedAsync(connectionString);
        await using var factory = CreateFactory(
            connectionString, OperatorId, enableEmailAutomation: true);
        using var client = factory.CreateClient();
        var provider = factory.Services.GetRequiredService<DeterministicEmailProviderClient>();
        await ConfigureMailboxAsync(client);
        provider.Register(CreateEmail(
            "email-untrusted-reply",
            "message-untrusted-reply",
            OohBriefBody,
            DateTimeOffset.UtcNow) with
        {
            ReplyTo = ["attacker@outside.example"],
        });

        using var receipt = await SendWebhookAsync(
            client, "event-untrusted-reply", "email-untrusted-reply");
        var inboundEmailId = receipt.RootElement.GetProperty("inboundEmailId").GetGuid();
        using var detail = await GetJsonAsync(
            client, Path($"email-automation/messages/{inboundEmailId}"));
        var run = detail.RootElement.GetProperty("run");

        Assert.Equal("REVIEW_REQUIRED", run.GetProperty("status").GetString());
        Assert.Equal("INVALID_RECIPIENT", run.GetProperty("failureCode").GetString());
        Assert.Equal("attacker@outside.example", detail.RootElement.GetProperty("email")
            .GetProperty("replyToEmail").GetString());
        Assert.Empty(provider.Deliveries);
    }

    [Fact]
    [Trait("Category", "Migration")]
    public async Task DisabledModeStopsManualProcessingBeforeAnyProviderCall()
    {
        await using var postgres = CreatePostgres();
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DisposableDatabaseRoles.ProvisionAsync(connectionString);
        await SeedAsync(connectionString);

        Guid inboundEmailId;
        await using (var intakeFactory = CreateFactory(
            connectionString, OperatorId, enableEmailAutomation: true))
        {
            using var intakeClient = intakeFactory.CreateClient();
            var intakeProvider = intakeFactory.Services
                .GetRequiredService<DeterministicEmailProviderClient>();
            await ConfigureMailboxAsync(
                intakeClient, autoSendEnabled: false, "configure-disabled-mode");
            intakeProvider.Register(CreateEmail(
                "email-disabled-mode",
                "message-disabled-mode",
                OohBriefBody,
                DateTimeOffset.UtcNow));
            using var receipt = await SendWebhookAsync(
                intakeClient, "event-disabled-mode", "email-disabled-mode");
            inboundEmailId = receipt.RootElement.GetProperty("inboundEmailId").GetGuid();
            Assert.Empty(intakeProvider.Deliveries);
        }

        await using var disabledFactory = CreateFactory(connectionString, OperatorId);
        using var disabledClient = disabledFactory.CreateClient();
        using var processed = await ProcessMessageAsync(
            disabledClient, inboundEmailId, "process-disabled-mode");
        Assert.Equal("REVIEW_REQUIRED",
            processed.RootElement.GetProperty("status").GetString());
        Assert.Equal("INVALID_MAILBOX",
            processed.RootElement.GetProperty("failureCode").GetString());
        Assert.Empty(disabledFactory.Services
            .GetRequiredService<DeterministicEmailProviderClient>().Deliveries);
    }

    [Fact]
    [Trait("Category", "Migration")]
    public async Task ConfirmedProviderRejectionStaysFailedAndCannotBlindlyResend()
    {
        await using var postgres = CreatePostgres();
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DisposableDatabaseRoles.ProvisionAsync(connectionString);
        await SeedAsync(connectionString);
        var ledger = new RejectedEmailDeliveryLedger();
        await using var factory = CreateRejectedFactory(connectionString, ledger);
        using var client = factory.CreateClient();
        await ConfigureMailboxAsync(client);
        factory.Services.GetRequiredService<DeterministicEmailProviderClient>().Register(
            CreateEmail(
                "email-rejected-delivery",
                "message-rejected-delivery",
                OohBriefBody,
                DateTimeOffset.UtcNow));

        using var receipt = await SendWebhookAsync(
            client, "event-rejected-delivery", "email-rejected-delivery");
        var inboundEmailId = receipt.RootElement.GetProperty("inboundEmailId").GetGuid();
        using var detail = await GetJsonAsync(
            client, Path($"email-automation/messages/{inboundEmailId}"));
        var run = detail.RootElement.GetProperty("run");
        Assert.Equal("FAILED", run.GetProperty("status").GetString());
        Assert.Equal("DELIVERY_FAILED", run.GetProperty("failureCode").GetString());
        Assert.NotEqual(JsonValueKind.Null,
            run.GetProperty("deliveryRequestedAtUtc").ValueKind);
        Assert.Equal(1, ledger.SendAttempts);

        using var process = await RawProcessMessageAsync(
            client, inboundEmailId, "process-rejected-delivery");
        await AssertProblemAsync(
            process, HttpStatusCode.Conflict, "EMAIL_AUTOMATION_NOT_RETRYABLE");
        await AssertRetryBlockedAsync(
            client, inboundEmailId, run.GetProperty("version").GetInt64(),
            "rejected-delivery");
        Assert.Equal(1, ledger.SendAttempts);
        Assert.Equal(0, ledger.ReconciliationAttempts);
    }

    [Fact]
    [Trait("Category", "Migration")]
    public async Task ManualReconciliationRetainsOperatorAndCommandIdempotency()
    {
        await using var postgres = CreatePostgres();
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DisposableDatabaseRoles.ProvisionAsync(connectionString);
        await SeedAsync(connectionString);
        await AddSecondTenantAdministratorAsync(connectionString);
        var ledger = new AmbiguousEmailDeliveryLedger(
            EmailDeliveryReconciliationOutcome.Unknown);

        Guid inboundEmailId;
        Guid runId;
        await using (var intakeFactory = CreateDurabilityFactory(connectionString, ledger))
        {
            using var intakeClient = intakeFactory.CreateClient();
            await ConfigureMailboxAsync(intakeClient);
            intakeFactory.Services.GetRequiredService<DeterministicEmailProviderClient>()
                .Register(CreateEmail(
                    "email-manual-operator",
                    "message-manual-operator",
                    OohBriefBody,
                    DateTimeOffset.UtcNow));
            using var receipt = await SendWebhookAsync(
                intakeClient, "event-manual-operator", "email-manual-operator");
            inboundEmailId = receipt.RootElement.GetProperty("inboundEmailId").GetGuid();
            using var detail = await GetJsonAsync(
                intakeClient, Path($"email-automation/messages/{inboundEmailId}"));
            runId = detail.RootElement.GetProperty("run").GetProperty("id").GetGuid();
        }

        await using var operatorFactory = CreateFactory(
            connectionString,
            OtherUserId,
            enableEmailAutomation: true,
            services => ConfigureAmbiguousProvider(services, ledger));
        using var operatorClient = operatorFactory.CreateClient();
        const string key = "process-manual-second-operator";
        using var first = await ProcessMessageAsync(operatorClient, inboundEmailId, key);
        using var replay = await ProcessMessageAsync(operatorClient, inboundEmailId, key);

        AssertAmbiguousRun(first.RootElement);
        AssertAmbiguousRun(replay.RootElement);
        Assert.Equal(1, ledger.SendAttempts);
        Assert.Equal(1, ledger.ReconciliationAttempts);
        Assert.Equal(1, await CountAuditAsync(
            connectionString, runId, OtherUserId,
            MasterDataCodes.CommercialActions.EmailAutomationStarted));
        Assert.Equal(1, await CountAuditAsync(
            connectionString, runId, OtherUserId, "command.duplicate_received"));
    }

    [Fact]
    [Trait("Category", "Migration")]
    public async Task ProcessingRunResumesFromItsPersistedCheckpoint()
    {
        await using var postgres = CreatePostgres();
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DisposableDatabaseRoles.ProvisionAsync(connectionString);
        await SeedAsync(connectionString);
        await using var factory = CreateFactory(
                connectionString, OperatorId, enableEmailAutomation: true)
            .WithWebHostBuilder(builder =>
                builder.UseSetting("EmailAutomation:ProcessInline", "false"));
        using var client = factory.CreateClient();
        var provider = factory.Services.GetRequiredService<DeterministicEmailProviderClient>();
        await ConfigureMailboxAsync(client);
        provider.Register(CreateEmail(
            "email-resume-processing",
            "message-resume-processing",
            OohBriefBody,
            DateTimeOffset.UtcNow));
        using var receipt = await SendWebhookAsync(
            client, "event-resume-processing", "email-resume-processing");
        var inboundEmailId = receipt.RootElement.GetProperty("inboundEmailId").GetGuid();
        await SetRunProcessingAsync(connectionString, inboundEmailId);

        using var processed = await ProcessMessageAsync(
            client, inboundEmailId, "process-resume-processing");

        Assert.Equal("SENT", processed.RootElement.GetProperty("status").GetString());
        Assert.Single(provider.Deliveries);
    }

    private static Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>
        CreateRejectedFactory(string connectionString, RejectedEmailDeliveryLedger ledger) =>
        CreateFactory(
            connectionString,
            OperatorId,
            enableEmailAutomation: true,
            services =>
            {
                services.RemoveAll<IEmailProviderClient>();
                services.AddSingleton<IEmailProviderClient>(provider =>
                    new RejectedEmailProviderClient(
                        provider.GetRequiredService<DeterministicEmailProviderClient>(),
                        ledger));
            });

    private static void ConfigureAmbiguousProvider(
        IServiceCollection services,
        AmbiguousEmailDeliveryLedger ledger)
    {
        services.RemoveAll<IEmailProviderClient>();
        services.AddSingleton<IEmailProviderClient>(provider =>
            new AmbiguousEmailProviderClient(
                provider.GetRequiredService<DeterministicEmailProviderClient>(),
                ledger));
    }

    private static async Task ConfigureMailboxAsync(
        HttpClient client,
        bool autoSendEnabled,
        string key)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, Path("email-automation/mailbox"))
        {
            Content = JsonContent.Create(new
            {
                address = "ooh@planning.example",
                provider = MasterDataCodes.EmailProviders.Deterministic,
                ownerUserId = OperatorId,
                defaultClientAccountId = (Guid?)null,
                autoSendEnabled,
                allowedSenderDomains = AllowedClientSenderDomains,
            }),
        };
        request.Headers.Add("Idempotency-Key", key);
        request.Headers.Add("X-Correlation-ID", Guid.NewGuid().ToString());
        using var response = await client.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode,
            $"Mailbox configuration failed: {await response.Content.ReadAsStringAsync()}");
    }

    private static async Task<HttpResponseMessage> RawProcessMessageAsync(
        HttpClient client,
        Guid inboundEmailId,
        string key)
    {
        using var detail = await GetJsonAsync(
            client, Path($"email-automation/messages/{inboundEmailId}"));
        var version = detail.RootElement.GetProperty("run").GetProperty("version").GetInt64();
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            Path($"email-automation/messages/{inboundEmailId}:process"));
        request.Headers.Add("Idempotency-Key", key);
        request.Headers.Add("X-Correlation-ID", Guid.NewGuid().ToString());
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        return await client.SendAsync(request);
    }

    private static async Task AddSecondTenantAdministratorAsync(string connectionString)
    {
        var options = new DbContextOptionsBuilder<GovernanceDbContext>()
            .UseNpgsql(connectionString).Options;
        await using var db = new GovernanceDbContext(options);
        db.Memberships.Add(CreateMembership(
            TenantId, OtherUserId, MasterDataCodes.Roles.AgencyAdmin, 3));
        await db.SaveChangesAsync();
    }

    private static async Task SetRunProcessingAsync(
        string connectionString,
        Guid inboundEmailId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            UPDATE commercial.email_proposal_automation_runs
            SET status_code = 'PROCESSING', version = version + 1
            WHERE tenant_id = $1 AND inbound_email_id = $2
            """, connection);
        command.Parameters.AddWithValue(TenantId);
        command.Parameters.AddWithValue(inboundEmailId);
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }

    private static async Task<long> CountAuditAsync(
        string connectionString,
        Guid runId,
        Guid actorId,
        string action)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT count(*) FROM commercial.audit_events
            WHERE tenant_id = $1 AND resource_id = $2
              AND actor_id = $3 AND action_code = $4
            """, connection);
        command.Parameters.AddWithValue(TenantId);
        command.Parameters.AddWithValue(runId);
        command.Parameters.AddWithValue(actorId);
        command.Parameters.AddWithValue(action);
        return (long)(await command.ExecuteScalarAsync())!;
    }
}

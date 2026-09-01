using Advertified.Commercial.Application.EmailAutomation;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.EmailAutomation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class CanonicalPlanningAcceptanceTests
{
    [Fact]
    [Trait("Category", "Migration")]
    public async Task HostInterruptionAfterDurableDeliveryRequestNeverSendsAgain()
    {
        await using var postgres = CreatePostgres();
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DisposableDatabaseRoles.ProvisionAsync(connectionString);
        await SeedAsync(connectionString);
        var ledger = new InterruptedEmailDeliveryLedger();

        Guid inboundEmailId;
        await using (var firstFactory = CreateInterruptedFactory(connectionString, ledger))
        {
            using var client = firstFactory.CreateClient();
            await ConfigureMailboxAsync(
                client, autoSendEnabled: false, "configure-interrupted-host");
            firstFactory.Services.GetRequiredService<DeterministicEmailProviderClient>()
                .Register(CreateEmail(
                    "email-interrupted-host",
                    "message-interrupted-host",
                    OohBriefBody,
                    DateTimeOffset.UtcNow));
            using var receipt = await SendWebhookAsync(
                client, "event-interrupted-host", "email-interrupted-host");
            inboundEmailId = receipt.RootElement.GetProperty("inboundEmailId").GetGuid();
            await EnableMailboxAutoSendAsync(connectionString);

            using var scope = firstFactory.Services.CreateScope();
            var processor = scope.ServiceProvider
                .GetRequiredService<IEmailProposalAutomationProcessor>();
            await Assert.ThrowsAsync<OperationCanceledException>(() => processor.ProcessAsync(
                new TenantId(TenantId),
                new ActorId(OperatorId),
                inboundEmailId,
                new CorrelationId(Guid.NewGuid()),
                CancellationToken.None));

            using var interrupted = await GetJsonAsync(
                client, Path($"email-automation/messages/{inboundEmailId}"));
            var run = interrupted.RootElement.GetProperty("run");
            Assert.Equal("PROCESSING", run.GetProperty("status").GetString());
            Assert.Equal("DELIVERY_REQUESTED", run.GetProperty("checkpoint").GetString());
            Assert.NotEqual(System.Text.Json.JsonValueKind.Null,
                run.GetProperty("deliveryRequestedAtUtc").ValueKind);
        }

        Assert.Equal(1, ledger.SendAttempts);
        Assert.Equal(0, ledger.ReconciliationAttempts);
        await using var secondFactory = CreateInterruptedFactory(connectionString, ledger);
        using var secondClient = secondFactory.CreateClient();
        using var recovered = await ProcessMessageAsync(
            secondClient, inboundEmailId, "process-interrupted-host");

        AssertAmbiguousRun(recovered.RootElement);
        Assert.Equal(1, ledger.SendAttempts);
        Assert.Equal(1, ledger.ReconciliationAttempts);
    }

    private static Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>
        CreateInterruptedFactory(
            string connectionString,
            InterruptedEmailDeliveryLedger ledger) =>
        CreateFactory(
            connectionString,
            OperatorId,
            enableEmailAutomation: true,
            services =>
            {
                services.RemoveAll<IEmailProviderClient>();
                services.AddSingleton<IEmailProviderClient>(provider =>
                    new InterruptedEmailProviderClient(
                        provider.GetRequiredService<DeterministicEmailProviderClient>(),
                        ledger));
            });

    private static async Task EnableMailboxAutoSendAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            UPDATE commercial.inbound_mailboxes
            SET auto_send_enabled = true, version = version + 1
            WHERE tenant_id = $1
            """, connection);
        command.Parameters.AddWithValue(TenantId);
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }
}

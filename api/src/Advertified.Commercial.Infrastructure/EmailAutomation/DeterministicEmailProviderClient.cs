using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Advertified.Commercial.Application.EmailAutomation;
using Advertified.Commercial.Domain.MasterData;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Infrastructure.EmailAutomation;

public sealed class DeterministicEmailProviderClient(
    IOptions<EmailAutomationOptions> options,
    TimeProvider timeProvider) : IEmailProviderClient
{
    private const string ReceivedEventType = "email.received";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly ConcurrentDictionary<string, RetrievedInboundEmail> received =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ProposalEmailDelivery> deliveries =
        new(StringComparer.Ordinal);

    public string ProviderCode => MasterDataCodes.EmailProviders.Deterministic;

    public IReadOnlyCollection<ProposalEmailDelivery> Deliveries =>
        deliveries.Values.OrderBy(value => value.IdempotencyKey, StringComparer.Ordinal).ToArray();

    public void Register(RetrievedInboundEmail email) =>
        received[email.ProviderEmailId] = email;

    public bool VerifyWebhook(
        string rawPayload,
        string messageId,
        string timestamp,
        string signature,
        DateTimeOffset now)
    {
        var secret = options.Value.ResendWebhookSecret;
        return !string.IsNullOrWhiteSpace(secret) && ResendWebhookVerifier.Verify(
            rawPayload,
            messageId,
            timestamp,
            signature,
            secret,
            TimeSpan.FromSeconds(options.Value.WebhookToleranceSeconds),
            now);
    }

    public InboundEmailNotification ParseNotification(string rawPayload)
    {
        var notification = JsonSerializer.Deserialize<FixtureNotification>(rawPayload, Json)
            ?? throw new InvalidEmailWebhookException();
        if (notification.Type != ReceivedEventType || notification.Data is null)
        {
            throw new InvalidEmailWebhookException();
        }
        var email = received.GetValueOrDefault(notification.Data.EmailId)
            ?? throw new EmailPayloadUnavailableException();
        return new InboundEmailNotification(
            ProviderCode,
            email.ProviderEmailId,
            email.ProviderMessageId,
            email.Recipients,
            email.SenderEmail,
            email.Subject,
            email.ReceivedAtUtc,
            email.Attachments);
    }

    public Task<RetrievedInboundEmail> RetrieveAsync(
        string providerEmailId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(received.GetValueOrDefault(providerEmailId)
            ?? throw new EmailPayloadUnavailableException());
    }

    public Task<EmailDeliveryReceipt> SendAsync(
        ProposalEmailDelivery delivery,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        deliveries.TryAdd(delivery.IdempotencyKey, delivery);
        var providerId = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(delivery.IdempotencyKey)))
            .ToLowerInvariant()[..24];
        return Task.FromResult(new EmailDeliveryReceipt(
            string.Concat("deterministic-", providerId),
            timeProvider.GetUtcNow()));
    }

    private sealed record FixtureNotification(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("data")] FixtureNotificationData? Data);

    private sealed record FixtureNotificationData(
        [property: JsonPropertyName("email_id")] string EmailId);
}

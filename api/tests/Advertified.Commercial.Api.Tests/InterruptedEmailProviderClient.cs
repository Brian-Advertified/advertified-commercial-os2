using Advertified.Commercial.Application.EmailAutomation;
using Advertified.Commercial.Infrastructure.EmailAutomation;

namespace Advertified.Commercial.Api.Tests;

internal sealed class InterruptedEmailProviderClient(
    DeterministicEmailProviderClient inbound,
    InterruptedEmailDeliveryLedger ledger) : IEmailProviderClient
{
    public string ProviderCode => inbound.ProviderCode;

    public InboundEmailIdentityAssessment AssessInboundIdentity(
        RetrievedInboundEmail email) => inbound.AssessInboundIdentity(email);

    public bool VerifyWebhook(
        string rawPayload,
        string messageId,
        string timestamp,
        string signature,
        DateTimeOffset now) =>
        inbound.VerifyWebhook(rawPayload, messageId, timestamp, signature, now);

    public InboundEmailNotification ParseNotification(string rawPayload) =>
        inbound.ParseNotification(rawPayload);

    public Task<RetrievedInboundEmail> RetrieveAsync(
        string providerEmailId,
        CancellationToken cancellationToken) =>
        inbound.RetrieveAsync(providerEmailId, cancellationToken);

    public Task<EmailDeliveryReceipt> SendAsync(
        ProposalEmailDelivery delivery,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ledger.RecordInterruptedSend(delivery.IdempotencyKey);
        throw new OperationCanceledException("The host stopped after durable dispatch.");
    }

    public Task<EmailDeliveryReconciliationResult> ReconcileDeliveryAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ledger.RecordReconciliation(idempotencyKey);
        return Task.FromResult(new EmailDeliveryReconciliationResult(
            EmailDeliveryReconciliationOutcome.Unknown,
            null));
    }
}

internal sealed class InterruptedEmailDeliveryLedger
{
    private readonly object sync = new();
    private string? idempotencyKey;

    internal int SendAttempts { get; private set; }

    internal int ReconciliationAttempts { get; private set; }

    internal void RecordInterruptedSend(string key)
    {
        lock (sync)
        {
            SendAttempts++;
            idempotencyKey ??= key;
            EnsureKey(key);
        }
    }

    internal void RecordReconciliation(string key)
    {
        lock (sync)
        {
            ReconciliationAttempts++;
            EnsureKey(key);
        }
    }

    private void EnsureKey(string key)
    {
        if (idempotencyKey != key)
        {
            throw new InvalidOperationException(
                "Recovery did not use the original delivery request key.");
        }
    }
}

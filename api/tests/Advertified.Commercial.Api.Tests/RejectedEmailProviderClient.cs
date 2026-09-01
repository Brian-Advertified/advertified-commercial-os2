using Advertified.Commercial.Application.EmailAutomation;
using Advertified.Commercial.Infrastructure.EmailAutomation;

namespace Advertified.Commercial.Api.Tests;

internal sealed class RejectedEmailProviderClient(
    DeterministicEmailProviderClient inbound,
    RejectedEmailDeliveryLedger ledger) : IEmailProviderClient
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
        ledger.RecordRejection(delivery.IdempotencyKey);
        throw new EmailDeliveryFailedException();
    }

    public Task<EmailDeliveryReconciliationResult> ReconcileDeliveryAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ledger.RecordReconciliation();
        return Task.FromResult(new EmailDeliveryReconciliationResult(
            EmailDeliveryReconciliationOutcome.NotFound,
            null));
    }
}

internal sealed class RejectedEmailDeliveryLedger
{
    private readonly object sync = new();
    private string? idempotencyKey;

    internal int SendAttempts { get; private set; }

    internal int ReconciliationAttempts { get; private set; }

    internal void RecordRejection(string key)
    {
        lock (sync)
        {
            SendAttempts++;
            idempotencyKey ??= key;
            if (idempotencyKey != key)
            {
                throw new InvalidOperationException(
                    "A rejected delivery was retried with a different request key.");
            }
        }
    }

    internal void RecordReconciliation()
    {
        lock (sync)
        {
            ReconciliationAttempts++;
        }
    }
}

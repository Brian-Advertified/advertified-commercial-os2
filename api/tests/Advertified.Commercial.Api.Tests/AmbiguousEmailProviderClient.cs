using Advertified.Commercial.Application.EmailAutomation;
using Advertified.Commercial.Infrastructure.EmailAutomation;

namespace Advertified.Commercial.Api.Tests;

internal sealed class AmbiguousEmailProviderClient(
    DeterministicEmailProviderClient inbound,
    AmbiguousEmailDeliveryLedger ledger) : IEmailProviderClient
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
        ledger.Accept(delivery);
        throw new EmailDeliveryAcceptanceUnknownException();
    }

    public Task<EmailDeliveryReconciliationResult> ReconcileDeliveryAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ledger.Reconcile(idempotencyKey));
    }
}

internal sealed class AmbiguousEmailDeliveryLedger(
    EmailDeliveryReconciliationOutcome reconciliationOutcome)
{
    private static readonly DateTimeOffset AcceptedAt =
        new(2026, 8, 31, 20, 0, 0, TimeSpan.Zero);
    private readonly object sync = new();
    private ProposalEmailDelivery? delivery;
    private EmailDeliveryReceipt? receipt;

    internal int SendAttempts { get; private set; }

    internal int ReconciliationAttempts { get; private set; }

    internal ProposalEmailDelivery Delivery => delivery
        ?? throw new InvalidOperationException("No delivery was accepted.");

    internal EmailDeliveryReceipt Receipt => receipt
        ?? throw new InvalidOperationException("No delivery receipt was retained.");

    internal void Accept(ProposalEmailDelivery value)
    {
        lock (sync)
        {
            SendAttempts++;
            delivery ??= value;
            if (delivery.IdempotencyKey != value.IdempotencyKey)
            {
                throw new InvalidOperationException("The provider received a different request key.");
            }
            receipt ??= new EmailDeliveryReceipt(
                string.Concat("ambiguous-", value.IdempotencyKey[^24..]),
                AcceptedAt);
        }
    }

    internal EmailDeliveryReconciliationResult Reconcile(string idempotencyKey)
    {
        lock (sync)
        {
            ReconciliationAttempts++;
            if (receipt is null || delivery?.IdempotencyKey != idempotencyKey)
            {
                return new EmailDeliveryReconciliationResult(
                    EmailDeliveryReconciliationOutcome.NotFound,
                    null);
            }
            return reconciliationOutcome == EmailDeliveryReconciliationOutcome.Accepted
                ? new EmailDeliveryReconciliationResult(reconciliationOutcome, receipt)
                : new EmailDeliveryReconciliationResult(reconciliationOutcome, null);
        }
    }
}

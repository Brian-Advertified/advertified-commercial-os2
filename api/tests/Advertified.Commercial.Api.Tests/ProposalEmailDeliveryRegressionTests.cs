using Advertified.Commercial.Application.EmailAutomation;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.EmailAutomation;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class ProposalEmailDeliveryRegressionTests
{
    private static readonly ProposalEmailDelivery Delivery = new("client@example.test", "owner@example.test",
        "Approved proposal", "Approved content", "proposal.pdf", "application/pdf", [1], null, "retained-key");

    [Theory]
    [InlineData(EmailDeliveryReconciliationOutcome.NotFound)]
    [InlineData(EmailDeliveryReconciliationOutcome.Unknown)]
    public async Task RetainedIntentNeverBlindlyResends(EmailDeliveryReconciliationOutcome outcome)
    {
        var provider = new Provider { Outcome = outcome };
        await Assert.ThrowsAsync<EmailDeliveryAcceptanceUnknownException>(() =>
            DurableProposalEmailDelivery.SendOrReconcileAsync(provider, Delivery, false, CancellationToken.None));
        Assert.Equal(0, provider.Sends);
        Assert.Equal(1, provider.Reconciliations);
    }

    [Fact]
    public async Task LostSendResponseIsUnknownAndRetryReconcilesTheSameKey()
    {
        var provider = new Provider { LoseSendResponse = true };
        await Assert.ThrowsAsync<EmailDeliveryAcceptanceUnknownException>(() =>
            DurableProposalEmailDelivery.SendOrReconcileAsync(provider, Delivery, true, CancellationToken.None));
        var receipt = await DurableProposalEmailDelivery.SendOrReconcileAsync(provider, Delivery, false, CancellationToken.None);
        Assert.Equal(provider.Receipt, receipt);
        Assert.Equal(1, provider.Sends);
        Assert.Equal(Delivery.IdempotencyKey, provider.LastKey);
    }

    private sealed class Provider : IEmailProviderClient
    {
        public string ProviderCode => MasterDataCodes.EmailProviders.Deterministic;
        public EmailDeliveryReceipt Receipt { get; } = new("accepted-message", DateTimeOffset.UnixEpoch);
        public EmailDeliveryReconciliationOutcome Outcome { get; init; } = EmailDeliveryReconciliationOutcome.Accepted;
        public bool LoseSendResponse { get; init; }
        public int Sends { get; private set; }
        public int Reconciliations { get; private set; }
        public string? LastKey { get; private set; }
        public Task<EmailDeliveryReceipt> SendAsync(ProposalEmailDelivery delivery, CancellationToken cancellationToken)
        {
            Sends++;
            LastKey = delivery.IdempotencyKey;
            return LoseSendResponse ? Task.FromException<EmailDeliveryReceipt>(new TimeoutException()) : Task.FromResult(Receipt);
        }
        public Task<EmailDeliveryReconciliationResult> ReconcileDeliveryAsync(string key, CancellationToken cancellationToken)
        {
            Reconciliations++;
            Assert.Equal(Delivery.IdempotencyKey, key);
            return Task.FromResult(new EmailDeliveryReconciliationResult(Outcome,
                Outcome == EmailDeliveryReconciliationOutcome.Accepted ? Receipt : null));
        }
        public InboundEmailIdentityAssessment AssessInboundIdentity(RetrievedInboundEmail email) => throw new NotSupportedException();
        public bool VerifyWebhook(string rawPayload, string messageId, string timestamp, string signature,
            DateTimeOffset now) => throw new NotSupportedException();
        public InboundEmailNotification ParseNotification(string rawPayload) => throw new NotSupportedException();
        public Task<RetrievedInboundEmail> RetrieveAsync(string id, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}

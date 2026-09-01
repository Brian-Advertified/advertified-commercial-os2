using Advertified.Commercial.Application.EmailAutomation;
using Advertified.Commercial.Infrastructure.EmailAutomation;
using Microsoft.Extensions.Options;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class DeterministicEmailProviderClientTests
{
    [Fact]
    public async Task ReusesAndReconcilesOneStableReceiptPerDeliveryKey()
    {
        var provider = new DeterministicEmailProviderClient(
            Options.Create(new EmailAutomationOptions()),
            new FixedTimeProvider());
        var delivery = new ProposalEmailDelivery(
            "client@example.test",
            "proposals@example.test",
            "OOH proposal",
            "Attached is the approved proposal.",
            "proposal.pdf",
            "application/pdf",
            [1, 2, 3],
            "source-message-1",
            "delivery-key-1");

        var first = await provider.SendAsync(delivery, CancellationToken.None);
        var repeated = await provider.SendAsync(delivery, CancellationToken.None);
        var reconciled = await provider.ReconcileDeliveryAsync(
            delivery.IdempotencyKey, CancellationToken.None);

        Assert.Equal(first, repeated);
        Assert.Single(provider.Deliveries);
        Assert.Equal(EmailDeliveryReconciliationOutcome.Accepted, reconciled.Outcome);
        Assert.Equal(first, reconciled.Receipt);
    }

    [Fact]
    public async Task MissingDeliveryReconcilesAsNotFoundWithoutSending()
    {
        var provider = new DeterministicEmailProviderClient(
            Options.Create(new EmailAutomationOptions()),
            new FixedTimeProvider());

        var reconciled = await provider.ReconcileDeliveryAsync(
            "missing-delivery-key", CancellationToken.None);

        Assert.Equal(EmailDeliveryReconciliationOutcome.NotFound, reconciled.Outcome);
        Assert.Null(reconciled.Receipt);
        Assert.Empty(provider.Deliveries);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(2026, 8, 31, 18, 30, 0, TimeSpan.Zero);
    }
}

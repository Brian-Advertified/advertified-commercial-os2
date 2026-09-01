using System.Net;
using System.Text;
using Advertified.Commercial.Application.EmailAutomation;
using Advertified.Commercial.Infrastructure.EmailAutomation;
using Microsoft.Extensions.Options;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class ResendEmailProviderClientTests
{
    private static readonly DateTimeOffset AcceptedAt =
        new(2026, 8, 31, 18, 30, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task AmbiguousDeliveryResponseNeverBecomesConfirmedFailure(
        HttpStatusCode statusCode)
    {
        using var client = CreateClient(_ => Response(statusCode, "{}"));

        await Assert.ThrowsAsync<EmailDeliveryAcceptanceUnknownException>(
            () => client.SendAsync(Delivery(), CancellationToken.None));
    }

    [Fact]
    public async Task MalformedSuccessNeverBecomesAccepted()
    {
        using var client = CreateClient(_ => Response(HttpStatusCode.OK, "{}"));

        await Assert.ThrowsAsync<EmailDeliveryAcceptanceUnknownException>(
            () => client.SendAsync(Delivery(), CancellationToken.None));
    }

    [Fact]
    public async Task ConfirmedClientRejectionRemainsFailed()
    {
        using var client = CreateClient(_ => Response(HttpStatusCode.BadRequest, "{}"));

        await Assert.ThrowsAsync<EmailDeliveryFailedException>(
            () => client.SendAsync(Delivery(), CancellationToken.None));
    }

    [Fact]
    public async Task TransportFailureAfterDispatchIsAcceptanceUnknown()
    {
        using var client = CreateClient(_ => throw new HttpRequestException("connection lost"));

        await Assert.ThrowsAsync<EmailDeliveryAcceptanceUnknownException>(
            () => client.SendAsync(Delivery(), CancellationToken.None));
    }

    [Fact]
    public async Task CanceledRequestAfterDispatchIsAcceptanceUnknown()
    {
        using var client = CreateClient(_ => throw new OperationCanceledException());

        await Assert.ThrowsAsync<EmailDeliveryAcceptanceUnknownException>(
            () => client.SendAsync(Delivery(), CancellationToken.None));
    }

    [Fact]
    public async Task AcceptedResponseRetainsProviderReceiptAndIdempotencyKey()
    {
        string? key = null;
        using var client = CreateClient(request =>
        {
            key = request.Headers.GetValues("Idempotency-Key").Single();
            return Response(HttpStatusCode.OK, "{\"id\":\"resend-message-1\"}");
        });

        var receipt = await client.SendAsync(Delivery(), CancellationToken.None);

        Assert.Equal("delivery-key-1", key);
        Assert.Equal("resend-message-1", receipt.ProviderMessageId);
        Assert.Equal(AcceptedAt, receipt.AcceptedAtUtc);
    }

    [Fact]
    public async Task ReconciliationIsUnknownWithoutAnotherHttpRequest()
    {
        var requests = 0;
        using var client = CreateClient(_ =>
        {
            requests++;
            return Response(HttpStatusCode.OK, "{}");
        });

        var result = await client.ReconcileDeliveryAsync(
            "delivery-key-1", CancellationToken.None);

        Assert.Equal(EmailDeliveryReconciliationOutcome.Unknown, result.Outcome);
        Assert.Null(result.Receipt);
        Assert.Equal(0, requests);
    }

    [Fact]
    public void InboundSenderIdentityIsFailClosedWithoutTrustedProviderEvidence()
    {
        using var client = CreateClient(_ => Response(HttpStatusCode.OK, "{}"));

        var assessment = client.AssessInboundIdentity(new RetrievedInboundEmail(
            "email-1",
            "message-1",
            ["ooh@example.test"],
            "brief@client.example",
            "Client Planner",
            ["brief@client.example"],
            "OOH request",
            "Client: Example",
            null,
            new Dictionary<string, string> { ["Authentication-Results"] = "spf=pass" },
            [],
            AcceptedAt));

        Assert.False(assessment.SenderAuthenticated);
    }

    private static ResendTestClient CreateClient(
        Func<HttpRequestMessage, HttpResponseMessage> send) => new(send);

    private static HttpResponseMessage Response(HttpStatusCode status, string content) =>
        new(status)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json"),
        };

    private static ProposalEmailDelivery Delivery() => new(
        "client@example.test",
        "proposals@example.test",
        "OOH proposal",
        "Attached is the approved proposal.",
        "proposal.pdf",
        "application/pdf",
        [1, 2, 3],
        "source-message-1",
        "delivery-key-1");

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> send) :
        HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(send(request));
        }
    }

    private sealed class ResendTestClient : IDisposable
    {
        private readonly HttpClient httpClient;
        private readonly ResendEmailProviderClient provider;

        public ResendTestClient(Func<HttpRequestMessage, HttpResponseMessage> send)
        {
            httpClient = new HttpClient(new StubHandler(send))
            {
                BaseAddress = new Uri("https://resend.test/"),
            };
            provider = new ResendEmailProviderClient(
                httpClient,
                Options.Create(new EmailAutomationOptions
                {
                    ResendApiKey = "local-test-key",
                }),
                new FixedTimeProvider());
        }

        public Task<EmailDeliveryReceipt> SendAsync(
            ProposalEmailDelivery delivery,
            CancellationToken cancellationToken) =>
            provider.SendAsync(delivery, cancellationToken);

        public Task<EmailDeliveryReconciliationResult> ReconcileDeliveryAsync(
            string idempotencyKey,
            CancellationToken cancellationToken) =>
            provider.ReconcileDeliveryAsync(idempotencyKey, cancellationToken);

        public InboundEmailIdentityAssessment AssessInboundIdentity(
            RetrievedInboundEmail email) => provider.AssessInboundIdentity(email);

        public void Dispose() => httpClient.Dispose();
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => AcceptedAt;
    }
}

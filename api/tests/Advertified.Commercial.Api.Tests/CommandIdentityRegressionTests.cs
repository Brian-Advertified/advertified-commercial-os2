using System.Text.Json;
using Advertified.Commercial.Api.Endpoints;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.Persistence.Records;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class CommandIdentityRegressionTests
{
    [Fact]
    public void IdentityBindsOperationResourceActorTenantAndOriginalPrecondition()
    {
        var first = Create("ExecuteInventoryImport", "import-a", 1);
        Assert.Equal(first.PayloadHash, Create("ExecuteInventoryImport", "import-a", 1).PayloadHash);
        Assert.NotEqual(first.PayloadHash, Create("ExecuteInventoryImport", "import-b", 1).PayloadHash);
        Assert.NotEqual(first.PayloadHash, Create("CancelInventoryExtraction", "import-a", 1).PayloadHash);
        Assert.NotEqual(first.PayloadHash, Create("ExecuteInventoryImport", "import-a", 2).PayloadHash);
        Assert.NotEqual(first.PayloadHash, Create("ExecuteInventoryImport", "import-a", 1,
            actor: GovernanceTestData.OtherActor).PayloadHash);
        Assert.NotEqual(first.PayloadHash, Create("ExecuteInventoryImport", "import-a", 1,
            tenant: GovernanceTestData.OtherTenant).PayloadHash);
    }

    [Fact]
    public void DictionaryOrderDoesNotChangeCanonicalCommandIdentity()
    {
        var first = new Dictionary<string, object> { ["z"] = 1, ["a"] = new { x = 2 } };
        var reordered = new Dictionary<string, object> { ["a"] = new { x = 2 }, ["z"] = 1 };
        Assert.Equal(CommandPayloadDigest.Create(first), CommandPayloadDigest.Create(reordered));
    }

    [Fact]
    public async Task ResourceAuthorizationIsRequiredAgainBeforeReplay()
    {
        using var unit = new InMemoryCommandUnitOfWork();
        var envelope = GovernanceTestData.Envelope();
        var calls = 0;
        Task<CommandOutcome> Apply(CancellationToken _)
        {
            calls++;
            return Task.FromResult(GovernanceTestData.Outcome(envelope));
        }
        await unit.ExecuteOnceAsync(envelope, Apply, CancellationToken.None, _ => Task.CompletedTask);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => unit.ExecuteOnceAsync(
            envelope, Apply, CancellationToken.None, _ => throw new UnauthorizedAccessException()));
        Assert.Equal(1, calls);
    }

    [Fact]
    public void ResponseOnlyClaimMaterialIsAbsentFromStoredOutcomeAndOutbox()
    {
        const string secret = "fixture-response-only-claim";
        var baseline = GovernanceTestData.Outcome(GovernanceTestData.Envelope());
        var outcome = new CommandOutcome(JsonSerializer.SerializeToElement(new { token = secret }),
            baseline.AggregateVersion, baseline.Audit, baseline.Outbox,
            persistedData: JsonSerializer.SerializeToElement(new { token = (string?)null }));
        var serialized = JsonSerializer.Serialize(StoredCommandOutcome.FromDomain(outcome));
        Assert.Contains(secret, outcome.Data.GetRawText());
        Assert.DoesNotContain(secret, serialized);
        Assert.DoesNotContain(secret, outcome.Outbox.Payload.GetRawText());
        Assert.Equal(JsonValueKind.Null, StoredCommandOutcome.FromDomain(outcome).ToDomain().Data.GetProperty("token").ValueKind);
    }

    private static CommandEnvelope<TestCommand> Create(string operation, string resource, long version,
        ActorId? actor = null, TenantId? tenant = null)
    {
        var context = new DefaultHttpContext { TraceIdentifier = Guid.NewGuid().ToString() };
        context.Request.Headers["Idempotency-Key"] = "same-logical-key";
        context.Request.Headers["If-Match"] = $"\"{version}\"";
        context.Request.RouteValues["importId"] = resource;
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new EndpointNameMetadata(operation)), operation));
        return CommandEnvelopeFactory.Create(context, tenant ?? GovernanceTestData.Tenant,
            actor ?? GovernanceTestData.Actor, new TestCommand("execute"), TimeProvider.System, true);
    }
}

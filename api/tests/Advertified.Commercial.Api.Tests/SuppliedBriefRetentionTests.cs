using Advertified.Commercial.Application.Brief;
using Advertified.Commercial.Application.Security;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.Brief;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class SuppliedBriefRetentionTests
{
    [Fact]
    public async Task UnsupportedClarificationFieldFailsBeforeReservation()
    {
        var store = new MemoryStore();
        var service = Service(new Provider(store, null, false), store);
        var request = new UnderstandSuppliedBriefRequest(
            "Request",
            "Promote the launch.",
            [new BriefClarificationInput("arbitrary.path", "override")]);

        await Assert.ThrowsAsync<ArgumentException>(() => service.UnderstandAsync(
            new(Guid.NewGuid()), new(Guid.NewGuid()), request, default));

        Assert.Empty(store.Events);
    }

    [Fact]
    public async Task GeneratedContractExposesRetainedSourceAndCorrectionReferences()
    {
        await using var factory = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseDeterministicTestDependencies());
        using var client = factory.CreateClient();
        var json = await client.GetStringAsync("/swagger/v1/swagger.json");
        using var document = System.Text.Json.JsonDocument.Parse(json);
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        Assert.True(schemas.GetProperty("UnderstandSuppliedBriefRequest").GetProperty("properties")
            .TryGetProperty("parentInterpretationId", out _));
        Assert.True(schemas.GetProperty("SuppliedBriefUnderstandingView").GetProperty("properties")
            .TryGetProperty("interpretation", out _));
        var directory = Path.Combine(Path.GetTempPath(), "advertified-contracts");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "advertified-commercial-api.v1.json"), json);
        var migration = new Advertified.Commercial.Infrastructure.Migrations.SuppliedBriefInterpretation();
        var sql = string.Join(Environment.NewLine, migration.UpOperations
            .OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.SqlOperation>().Select(item => item.Sql));
        await File.WriteAllTextAsync(Path.Combine(directory, "supplied-brief-migration.sql"), sql);
    }

    [Fact]
    public async Task ExactSourceIsRetainedBeforeInvocationAndCompletionSurvivesCallerCancellation()
    {
        using var caller = new CancellationTokenSource();
        var store = new MemoryStore();
        var provider = new Provider(store, caller, false);
        var service = Service(provider, store);
        const string source = "  Media: OOH billboards and radio\r\n";
        var result = await service.UnderstandAsync(new(Guid.NewGuid()), new(Guid.NewGuid()),
            new("Original request", source, InterpretationId: Guid.NewGuid()), caller.Token);
        Assert.Equal(source, store.Input!.SourceContent);
        Assert.Equal(store.Reference, result.Interpretation);
        Assert.Same(result, store.Completed);
        Assert.False(store.CompletionWasCancelled);
        Assert.Equal(["reserve", "invoke", "complete"], store.Events);
    }

    [Fact]
    public async Task RetainedReplayDoesNotInvokeProviderAgain()
    {
        var store = new MemoryStore();
        var service = Service(new Provider(store, null, false), store);
        var actor = new ActorId(Guid.NewGuid());
        var tenant = new TenantId(Guid.NewGuid());
        var request = new UnderstandSuppliedBriefRequest("Original request", "Media: OOH and radio");
        var first = await service.UnderstandAsync(actor, tenant, request, default);
        var replay = await service.UnderstandAsync(actor, tenant, request, default);
        Assert.Same(first, replay);
        Assert.Equal(1, store.Events.Count(value => value == "invoke"));
    }

    [Fact]
    public async Task FailedProviderRetainsFailureAndNeverInventsAnUnderstanding()
    {
        var store = new MemoryStore();
        var service = Service(new Provider(store, null, true), store);
        await Assert.ThrowsAsync<HttpRequestException>(() => service.UnderstandAsync(
            new(Guid.NewGuid()), new(Guid.NewGuid()), new("Request", "Unknown source"), default));
        Assert.Null(store.Completed);
        Assert.Equal(["reserve", "invoke", "fail"], store.Events);
    }

    [Fact]
    public async Task RevokedMembershipCannotReadReplayOrCreateRetainedSource()
    {
        var store = new MemoryStore();
        var service = new SuppliedBriefUnderstandingService(null!,
            new Authorizer(false), store);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.UnderstandAsync(
            new(Guid.NewGuid()), new(Guid.NewGuid()), new("Request", "Source"), default));
        Assert.Empty(store.Events);
    }

    private static SuppliedBriefUnderstandingService Service(ISuppliedBriefAgentClient provider, MemoryStore store) =>
        new(provider, new Authorizer(true), store);

    private sealed class Authorizer(bool allowed) : ITenantAuthorizer
    {
        public Task<AuthorizationDecision> AuthorizeAsync(ActorId actorId, TenantId requestedTenantId,
            PermissionCode permission, CancellationToken cancellationToken) =>
            Task.FromResult(allowed ? AuthorizationDecision.Allowed : AuthorizationDecision.Denied);
    }

    private sealed class Provider(MemoryStore store, CancellationTokenSource? caller, bool fail) : ISuppliedBriefAgentClient
    {
        public async Task<SuppliedBriefUnderstandingView> UnderstandAsync(SuppliedBriefAgentInput input,
            CancellationToken cancellationToken)
        {
            Assert.NotNull(store.Input);
            store.Events.Add("invoke");
            if (fail) throw new HttpRequestException("Intercepted provider outage");
            var output = SuppliedBriefAgentFixture.Create(input);
            caller?.Cancel();
            return await Task.FromResult(output);
        }
    }

    private sealed class MemoryStore : ISuppliedBriefInterpretationStore
    {
        internal List<string> Events { get; } = [];
        internal SuppliedBriefAgentInput? Input { get; private set; }
        internal SuppliedBriefUnderstandingView? Completed { get; private set; }
        internal bool CompletionWasCancelled { get; private set; }
        internal SuppliedBriefInterpretationReference Reference { get; } = new(Guid.NewGuid(), null, 1, new string('a', 64));

        public Task<SuppliedBriefReservation> ReserveAsync(SuppliedBriefAgentInput input, Guid id,
            Guid? parentId, CancellationToken cancellationToken)
        {
            Input = input;
            Events.Add("reserve");
            return Task.FromResult(new SuppliedBriefReservation(Reference, Completed));
        }

        public Task CompleteAsync(SuppliedBriefAgentInput input, SuppliedBriefUnderstandingView result,
            CancellationToken cancellationToken)
        {
            Events.Add("complete");
            Completed = result;
            CompletionWasCancelled = cancellationToken.IsCancellationRequested;
            return Task.CompletedTask;
        }

        public Task FailAsync(SuppliedBriefAgentInput input, CancellationToken cancellationToken)
        {
            Events.Add("fail");
            return Task.CompletedTask;
        }

        public Task RejectAsync(SuppliedBriefAgentInput input, SuppliedBriefValidationException failure,
            CancellationToken cancellationToken)
        {
            Events.Add("reject");
            return Task.CompletedTask;
        }
    }
}

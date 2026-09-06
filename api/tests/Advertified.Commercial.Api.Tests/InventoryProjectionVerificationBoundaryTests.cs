using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Application.Security;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.Inventory;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.Extensions.Options;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class InventoryProjectionVerificationBoundaryTests
{
    [Fact]
    public async Task VerificationRejectsExecutionWhenProcessingIsNotPaused()
    {
        var adapter = new RecordingAdapter();
        var authorizer = new RecordingAuthorizer();
        var verifier = new InventoryProjectionVerificationService(
            null!,
            authorizer,
            adapter,
            Options.Create(new InventoryProcessingOptions { Paused = false }),
            Options.Create(new InventoryProtectionOptions()),
            Options.Create(new InventorySemanticOptions()));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            verifier.VerifyAsync(
                new ActorId(Guid.NewGuid()),
                new TenantId(Guid.NewGuid()),
                new InventorySourceFile(
                    "source.pdf", "application/pdf", "%PDF-"u8.ToArray()),
                CancellationToken.None));

        Assert.False(authorizer.WasCalled);
        Assert.False(adapter.WasCalled);
    }

    [Fact]
    public void VerificationServiceHasNoCommercialMutationDependencies()
    {
        var parameterTypes = typeof(InventoryProjectionVerificationService)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(parameter => parameter.ParameterType.Name)
            .ToArray();

        Assert.DoesNotContain(parameterTypes, name =>
            name.Contains("Command", StringComparison.Ordinal) ||
            name.Contains("Outbox", StringComparison.Ordinal) ||
            name.Contains("Notification", StringComparison.Ordinal) ||
            name.Contains("Publish", StringComparison.Ordinal) ||
            name.Contains("Release", StringComparison.Ordinal));
    }

    [Fact]
    public void VerificationContractExposesProviderArtifactAndEveryPipelineBoundary()
    {
        Assert.NotNull(typeof(InventoryProjectionVerificationView)
            .GetProperty(nameof(InventoryProjectionVerificationView.ProviderJson)));
        var stages = InventoryProjectionVerificationService.VerificationStages();
        Assert.Equal(10, stages.Length);
        Assert.Equal(10, stages.Select(stage => stage.Stage).Distinct().Count());
        Assert.Contains(stages, stage =>
            stage.Stage == InventoryProjectionVerificationStages.DocumentProjection &&
            stage.State == InventoryExtractionTraceCodes.Mapped);
        Assert.Contains(stages, stage =>
            stage.Stage == InventoryProjectionVerificationStages.Persistence &&
            stage.State == InventoryExtractionTraceCodes.NotEvaluated);
    }

    private sealed class RecordingAuthorizer : ITenantAuthorizer
    {
        internal bool WasCalled { get; private set; }

        public Task<AuthorizationDecision> AuthorizeAsync(
            ActorId actorId,
            TenantId tenantId,
            PermissionCode permission,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult(AuthorizationDecision.Allowed);
        }
    }

    private sealed class RecordingAdapter : IInventoryDocumentExtractionAdapter
    {
        internal bool WasCalled { get; private set; }

        public Task<InventoryExtractionResult> ExtractAsync(
            InventoryExtractionRequest request,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            throw new InvalidOperationException("Not expected.");
        }
    }
}

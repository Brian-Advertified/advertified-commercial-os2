using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Foundation;
using Advertified.Commercial.Infrastructure.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed class InventoryResearchCommands(
    GovernanceDbContext dbContext,
    CommandDispatcher dispatcher,
    TimeProvider timeProvider) : IInventoryResearchCommands
{
    private readonly InventoryResearchStore store = new(dbContext);

    public async Task<CommandResult<InventoryResearchDatasetView>> RegisterAsync(
        CommandEnvelope<RegisterInventoryResearchDatasetCommand> envelope,
        CancellationToken cancellationToken)
    {
        InventoryResearchPolicy.Validate(envelope.Command);
        var receipt = await dispatcher.DispatchAsync(
            envelope, MasterDataReferences.Permissions.InventoryReview,
            token => RegisterOutcomeAsync(envelope, token), cancellationToken);
        return CommandOutcomeFactory.ToResult<InventoryResearchDatasetView>(receipt);
    }

    public async Task<CommandResult<InventoryResearchDatasetView>> ReviewAsync(
        Guid matchId, CommandEnvelope<ReviewInventoryResearchMatchCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, MasterDataReferences.Permissions.InventoryReview,
            token => ReviewOutcomeAsync(matchId, envelope, token), cancellationToken);
        return CommandOutcomeFactory.ToResult<InventoryResearchDatasetView>(receipt);
    }

    private async Task<CommandOutcome> RegisterOutcomeAsync(
        CommandEnvelope<RegisterInventoryResearchDatasetCommand> envelope,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var datasetId = await store.InsertDatasetAsync(
            envelope.TenantId, envelope.Command, envelope.ActorId.Value, now, cancellationToken);
        var view = await InventoryResearchProjection.BuildAsync(
            store, envelope.TenantId, datasetId, cancellationToken);
        return CommandOutcomeFactory.Create(
            envelope, view, datasetId, view.Version,
            MasterDataReferences.CommercialResourceTypes.EvidenceSource,
            MasterDataReferences.CommercialActions.EvidenceRegistered,
            MasterDataReferences.CommercialEventTypes.EvidenceRegistered, now);
    }

    private async Task<CommandOutcome> ReviewOutcomeAsync(
        Guid matchId, CommandEnvelope<ReviewInventoryResearchMatchCommand> envelope,
        CancellationToken cancellationToken)
    {
        var decision = InventoryResearchPolicy.ReviewDecision(envelope.Command.Decision);
        var reason = InventoryResearchPolicy.ReviewReason(envelope.Command.Reason);
        var match = await store.FindMatchAsync(
            envelope.TenantId, matchId, true, cancellationToken)
            ?? throw new UnauthorizedAccessException("Research match access denied.");
        if (match.Status != MasterDataCodes.LifecycleStatuses.InReview)
            throw new InvalidLifecycleTransitionException();
        if (match.Version != envelope.ExpectedVersion) throw new VersionConflictException();
        if (match.CreatedBy == envelope.ActorId.Value) throw new ApprovalRequiredException();
        Guid? appliedVersionId = null;
        if (decision == MasterDataCodes.InventoryReviewDecisions.Approve)
            appliedVersionId = await store.ApplyAudienceProfileAsync(
                envelope.TenantId, match, envelope.ActorId.Value,
                timeProvider.GetUtcNow(), cancellationToken);
        var now = timeProvider.GetUtcNow();
        await store.UpdateMatchReviewAsync(
            envelope.TenantId, match, decision, reason, envelope.ActorId.Value,
            now, appliedVersionId, cancellationToken);
        var view = await InventoryResearchProjection.BuildAsync(
            store, envelope.TenantId, match.DatasetId, cancellationToken);
        return CommandOutcomeFactory.Create(
            envelope, view, match.Id, match.Version + 1,
            MasterDataReferences.CommercialResourceTypes.EvidenceItem,
            MasterDataReferences.CommercialActions.EvidenceReviewed,
            MasterDataReferences.CommercialEventTypes.EvidenceReviewed, now,
            auditMetadata: System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                matchId, decision, reason, previousProductVersionId = match.ProductVersionId,
                appliedProductVersionId = appliedVersionId,
            }));
    }
}

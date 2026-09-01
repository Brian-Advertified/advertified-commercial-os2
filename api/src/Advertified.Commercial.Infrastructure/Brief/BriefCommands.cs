using Advertified.Commercial.Application.Brief;
using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.Foundation;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Brief;

public sealed partial class BriefCommands(
    BriefRecordStore store,
    BriefClientResolver clientResolver,
    CommandDispatcher dispatcher,
    TimeProvider timeProvider) : IBriefCommands
{

    public async Task<CommandResult<CampaignBriefSummaryView>> CreateAsync(
        CommandEnvelope<CreateBriefCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, MasterDataReferences.Permissions.BriefCreate,
            token => CreateOutcomeAsync(envelope, token), cancellationToken);
        return CommandOutcomeFactory.ToResult<CampaignBriefSummaryView>(receipt);
    }

    public async Task<CommandResult<BriefVersionView>> CreateVersionAsync(
        Guid briefId,
        CommandEnvelope<CreateBriefVersionCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, MasterDataReferences.Permissions.BriefEdit,
            token => CreateVersionOutcomeAsync(briefId, envelope, token), cancellationToken);
        return CommandOutcomeFactory.ToResult<BriefVersionView>(receipt);
    }

    public async Task<CommandResult<BriefVersionView>> SubmitAsync(
        Guid versionId,
        CommandEnvelope<SubmitBriefVersionCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, MasterDataReferences.Permissions.BriefSubmit,
            token => SubmitOutcomeAsync(versionId, envelope, token), cancellationToken);
        return CommandOutcomeFactory.ToResult<BriefVersionView>(receipt);
    }

    public async Task<CommandResult<BriefVersionView>> MarkReadyAsync(
        Guid versionId,
        CommandEnvelope<MarkBriefVersionReadyCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, MasterDataReferences.Permissions.BriefSubmit,
            token => MarkReadyOutcomeAsync(versionId, envelope, token), cancellationToken);
        return CommandOutcomeFactory.ToResult<BriefVersionView>(receipt);
    }

    public async Task<CommandResult<BriefVersionView>> ApproveAsync(
        Guid versionId,
        CommandEnvelope<ApproveBriefVersionCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, MasterDataReferences.Permissions.BriefApprove,
            token => ApproveOutcomeAsync(versionId, envelope, token), cancellationToken);
        return CommandOutcomeFactory.ToResult<BriefVersionView>(receipt);
    }

    public async Task<CommandResult<BriefVersionView>> RejectAsync(
        Guid versionId,
        CommandEnvelope<RejectBriefVersionCommand> envelope,
        CancellationToken cancellationToken)
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, MasterDataReferences.Permissions.BriefApprove,
            token => RejectOutcomeAsync(versionId, envelope, token), cancellationToken);
        return CommandOutcomeFactory.ToResult<BriefVersionView>(receipt);
    }

    private async Task<CommandOutcome> CreateOutcomeAsync(
        CommandEnvelope<CreateBriefCommand> envelope,
        CancellationToken cancellationToken)
    {
        var command = envelope.Command;
        if (command.OwnerUserId != envelope.ActorId.Value)
        {
            throw new UnauthorizedAccessException("Brief assignment denied.");
        }
        var now = timeProvider.GetUtcNow();
        var client = await clientResolver.ResolveAsync(
            envelope.TenantId,
            envelope.ActorId,
            command.ClientId,
            command.ClientName,
            now,
            cancellationToken);
        var sourceType = string.IsNullOrWhiteSpace(command.SourceType)
            ? MasterDataCodes.BriefSourceTypes.SuppliedText
            : command.SourceType.Trim().ToUpperInvariant();
        await OpportunityCommandSupport.EnsureCodeAsync(
            store.DbContext,
            MasterDataCodes.BriefSourceTypes.Collection,
            sourceType,
            cancellationToken);
        var title = OpportunityCommandSupport.Required(command.Title, 300, nameof(command.Title));
        var sourceTitle = OpportunityCommandSupport.Required(
            command.SourceTitle, 300, nameof(command.SourceTitle));
        var locator = OpportunityCommandSupport.Required(
            command.SourceLocator, 2048, nameof(command.SourceLocator));
        var content = OpportunityCommandSupport.Required(
            command.SourceContent, 262_144, nameof(command.SourceContent));
        var id = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        await BriefPersistence.InsertAggregateAndSourceAsync(
            store.DbContext,
            new BriefAggregateWrite(
                id, envelope.TenantId, client.Id, null, title, command.OwnerUserId,
                MasterDataCodes.LifecycleStatuses.Created, 1, now),
            new BriefSourceWrite(
                sourceId, sourceType, locator, sourceTitle, content,
                OpportunityCommandSupport.Hash(content), envelope.ActorId.Value, now),
            cancellationToken);
        var view = new CampaignBriefSummaryView(
            id, envelope.TenantId.Value, client.Id, client.Name, null, title, command.OwnerUserId,
            MasterDataCodes.LifecycleStatuses.Created, null, null, null, 1, now);
        return OpportunityCommandSupport.Outcome(
            envelope, view, id, 1, MasterDataReferences.CommercialResourceTypes.CampaignBrief,
            MasterDataReferences.CommercialActions.CampaignBriefCreated, MasterDataReferences.CommercialEventTypes.CampaignBriefCreated, now);
    }

}

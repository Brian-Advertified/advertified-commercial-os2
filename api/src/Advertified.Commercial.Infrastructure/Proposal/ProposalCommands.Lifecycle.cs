using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Application.Proposal;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Proposal;

public sealed partial class ProposalCommands
{
    private async Task<CommandOutcome> UpdateOutcomeAsync(
        Guid proposalVersionId,
        CommandEnvelope<UpdateProposalCommand> envelope,
        CancellationToken cancellationToken)
    {
        var proposal = await LoadOwnedProposalAsync(
            proposalVersionId, envelope, cancellationToken);
        if (proposal.Status != MasterDataCodes.LifecycleStatuses.Draft ||
            envelope.Command.ExpiryAtUtc <= timeProvider.GetUtcNow())
        {
            throw new InvalidLifecycleTransitionException();
        }
        var existing = await store.ListOptionsAsync(
            envelope.TenantId, proposalVersionId, cancellationToken);
        var edits = ValidateOptionEdits(existing, envelope.Command.Options);
        var title = OpportunityCommandSupport.Required(
            envelope.Command.Title, 300, nameof(envelope.Command.Title));
        var summary = OpportunityCommandSupport.Required(
            envelope.Command.ExecutiveSummary, 5_000,
            nameof(envelope.Command.ExecutiveSummary));
        var terms = OpportunityCommandSupport.Required(
            envelope.Command.Terms, 10_000, nameof(envelope.Command.Terms));
        var snapshots = BuildEditedSnapshots(existing, edits);
        var inputHash = BuildProposalHash(
            proposal.BriefVersionId, title, summary, snapshots,
            terms, envelope.Command.ExpiryAtUtc);
        var edited = await ProposalOptionPersistence.UpdateAsync(
            store.DbContext, envelope.TenantId, proposalVersionId,
            edits, cancellationToken);
        if (edited != edits.Length) throw new VersionConflictException();
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.proposal_versions
            SET title = {title}, executive_summary = {summary}, terms = {terms},
                expiry_at_utc = {envelope.Command.ExpiryAtUtc}, input_hash = {inputHash},
                version = version + 1
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {proposalVersionId}
              AND status_code = {MasterDataCodes.LifecycleStatuses.Draft}
              AND inventory_review_status_code =
                    {MasterDataCodes.ProposalInventoryReviewStatuses.Current}
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1) throw new VersionConflictException();
        var updated = proposal with
        {
            Title = title,
            ExecutiveSummary = summary,
            Terms = terms,
            ExpiryAtUtc = envelope.Command.ExpiryAtUtc,
            InputHash = inputHash,
            Version = proposal.Version + 1,
        };
        var view = await store.BuildViewAsync(
            envelope.TenantId, updated, cancellationToken);
        return ProposalOutcome(
            envelope, view, proposalVersionId, updated.Version,
            MasterDataReferences.CommercialActions.ProposalUpdated,
            MasterDataReferences.CommercialEventTypes.ProposalUpdated,
            timeProvider.GetUtcNow());
    }

    private Task<CommandOutcome> ApproveOutcomeAsync(
        Guid proposalVersionId,
        CommandEnvelope<ApproveProposalCommand> envelope,
        CancellationToken cancellationToken) =>
        ApproveWithGovernanceAsync(proposalVersionId, envelope, cancellationToken);

    private async Task<ProposalRow> LoadOwnedProposalAsync<TCommand>(
        Guid proposalVersionId,
        CommandEnvelope<TCommand> envelope,
        CancellationToken cancellationToken)
        where TCommand : notnull
    {
        var proposal = await store.FindProposalAsync(
            envelope.TenantId, proposalVersionId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Proposal access denied.");
        EnsureProposalInventoryCurrent(proposal);
        var brief = await store.FindPlanningReadyBriefAsync(
            envelope.TenantId, proposal.BriefId, cancellationToken)
            ?? throw new ProposalStaleException();
        if (brief.OwnerUserId != envelope.ActorId.Value ||
            brief.BriefVersionId != proposal.BriefVersionId)
        {
            throw new UnauthorizedAccessException("Proposal assignment denied.");
        }
        return proposal;
    }

    private static ProposalOptionEdit[] ValidateOptionEdits(
        List<ProposalOptionRow> existing,
        IReadOnlyList<ProposalOptionEdit> edits)
    {
        if (edits.Count != existing.Count ||
            edits.Select(item => item.OptionId).Distinct().Count() != edits.Count ||
            !edits.Select(item => item.OptionId).ToHashSet()
                .SetEquals(existing.Select(item => item.Id)))
        {
            throw new ArgumentException(
                "Proposal option edits do not match the current proposal.");
        }
        return edits.Select(edit => new ProposalOptionEdit(
            edit.OptionId,
            OpportunityCommandSupport.Required(edit.Label, 200, nameof(edit.Label)),
            OpportunityCommandSupport.Required(edit.Outcome, 2_000, nameof(edit.Outcome))))
            .ToArray();
    }

    private static ProposalOptionSnapshot[] BuildEditedSnapshots(
        IEnumerable<ProposalOptionRow> existing,
        IEnumerable<ProposalOptionEdit> edits)
    {
        var byId = edits.ToDictionary(item => item.OptionId);
        return existing.Select(row =>
        {
            var edit = byId[row.Id];
            var inventory = ProposalRecordStore.ReadInventory(row.InventoryJson);
            return new ProposalOptionSnapshot(
                edit.Label,
                edit.Outcome,
                new ProposalPlanSnapshot(
                    row.PlanVersionId, row.PlanVersionNumber, row.BudgetMinor,
                    row.Currency, string.Empty,
                    ProposalRecordStore.Read<string[]>(row.ChannelsJson),
                    ProposalRecordStore.Read<ProposalRunningPeriodView[]>(row.RunningPeriodsJson),
                    inventory.Names, inventory.Lines,
                    row.PlanSignature, []),
                row.DisplayOrder);
        }).ToArray();
    }
}

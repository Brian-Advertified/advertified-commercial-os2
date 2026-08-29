using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Proposal;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.Opportunity;

namespace Advertified.Commercial.Infrastructure.Proposal;

public sealed partial class ProposalCommands
{
    private async Task EnsurePlanInputsCurrentAsync(
        TenantId tenantId,
        IReadOnlyList<ProposalPlanSnapshot> plans,
        CancellationToken cancellationToken)
    {
        var current = await planningStore.ListInventoryAsync(tenantId, cancellationToken);
        var byProduct = current.ToDictionary(item => item.ProductId);
        if (plans.SelectMany(item => item.Lines).Any(line =>
                !byProduct.TryGetValue(line.InventoryProductId, out var item) ||
                item.ProductVersionId != line.ProductVersionId ||
                item.RateId != line.RateId || item.AvailabilityId != line.AvailabilityId))
        {
            throw new ProposalStaleException();
        }
    }

    private async Task EnsureProposalPlansCurrentAsync(
        TenantId tenantId,
        Guid proposalVersionId,
        CancellationToken cancellationToken)
    {
        var options = await store.ListOptionsAsync(tenantId, proposalVersionId, cancellationToken);
        var plans = new List<ProposalPlanSnapshot>(options.Count);
        foreach (var option in options)
        {
            var plan = await planningStore.FindPlanAsync(tenantId, option.PlanVersionId, cancellationToken)
                ?? throw new ProposalStaleException();
            if (plan.Status != Advertified.Commercial.Domain.MasterData.MasterDataCodes.LifecycleStatuses.Approved ||
                plan.VersionNumber != option.PlanVersionNumber)
            {
                throw new ProposalStaleException();
            }
            var view = await planningStore.BuildPlanViewAsync(tenantId, plan, cancellationToken);
            var snapshot = ToSnapshot(view);
            if (!string.Equals(snapshot.Signature, option.PlanSignature, StringComparison.Ordinal))
            {
                throw new ProposalStaleException();
            }
            plans.Add(snapshot);
        }
        await EnsurePlanInputsCurrentAsync(tenantId, plans, cancellationToken);
    }

    private static CommandOutcome ProposalOutcome<TCommand>(
        CommandEnvelope<TCommand> envelope,
        ProposalVersionView view,
        Guid resourceId,
        long version,
        ActionCode action,
        EventTypeCode eventType,
        DateTimeOffset now)
        where TCommand : notnull => OpportunityCommandSupport.Outcome(
            envelope, view, resourceId, version,
            Advertified.Commercial.Domain.MasterData.MasterDataReferences.CommercialResourceTypes.ProposalVersion,
            action, eventType, now);
}

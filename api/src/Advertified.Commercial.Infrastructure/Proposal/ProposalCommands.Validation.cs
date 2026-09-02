using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Application.Proposal;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;
using Advertified.Commercial.Infrastructure.Planning;

namespace Advertified.Commercial.Infrastructure.Proposal;

public sealed partial class ProposalCommands
{
    private async Task EnsurePlanInputsCurrentAsync(
        TenantId tenantId,
        IReadOnlyList<ProposalPlanSnapshot> plans,
        CancellationToken cancellationToken)
    {
        var current = await planningStore.ListInventoryAsync(tenantId, cancellationToken);
        var byProduct = current.ToDictionary(InventoryKey.For);
        if (plans.SelectMany(item => item.Lines).Any(line =>
                !byProduct.TryGetValue(new InventoryKey(
                    line.InventoryTenantId, line.InventoryProductId,
                    line.MarketplaceListingVersionId), out var item) ||
                item.ProductVersionId != line.ProductVersionId ||
                item.RateId != line.RateId ||
                item.AvailabilityId != line.AvailabilityId ||
                item.MarketplaceListingVersionId != line.MarketplaceListingVersionId ||
                !InventoryAvailabilityPolicy.IsAvailable(
                    item, line.RunningPeriods.Select(period =>
                        new MediaRunningPeriodView(
                            period.Start, period.End)).ToArray())))
        {
            throw new ProposalStaleException();
        }
    }

    private async Task EnsureProposalPlansCurrentAsync(
        TenantId tenantId,
        Guid proposalVersionId,
        CancellationToken cancellationToken)
    {
        var options = await store.ListOptionsAsync(
            tenantId, proposalVersionId, cancellationToken);
        var planIds = options.Select(item => item.PlanVersionId).Distinct().ToArray();
        var rows = await planningStore.ListPlansAsync(
            tenantId, planIds, cancellationToken);
        if (rows.Count != planIds.Length)
        {
            throw new ProposalStaleException();
        }
        var byId = rows.ToDictionary(item => item.Id);
        var ordered = options.Select(option => byId[option.PlanVersionId]).ToArray();
        if (ordered.Zip(options).Any(item =>
                item.First.Status != MasterDataCodes.LifecycleStatuses.Approved ||
                item.First.VersionNumber != item.Second.PlanVersionNumber))
        {
            throw new ProposalStaleException();
        }
        var views = await planningStore.BuildPlanViewsAsync(
            tenantId, ordered, cancellationToken);
        var plans = views.Select(ToSnapshot).ToArray();
        if (plans.Zip(options).Any(item => !string.Equals(
                item.First.Signature, item.Second.PlanSignature,
                StringComparison.Ordinal)))
        {
            throw new ProposalStaleException();
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
            MasterDataReferences.CommercialResourceTypes.ProposalVersion,
            action, eventType, now);
}

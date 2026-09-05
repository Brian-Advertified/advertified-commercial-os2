using Advertified.Commercial.Application.Proposal;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;

namespace Advertified.Commercial.Infrastructure.Proposal;

internal static class ProposalPlanProjection
{
    internal static ProposalPlanSnapshot ToSnapshot(Advertified.Commercial.Application.Planning.MediaPlanVersionView plan)
    {
        var channels = plan.Lines.Select(item => item.Channel)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var periods = plan.Lines.SelectMany(line => line.RunningPeriods.Select(period =>
                new ProposalRunningPeriodView(line.Channel, period.Start, period.End)))
            .Distinct().OrderBy(item => item.Channel, StringComparer.Ordinal)
            .ThenBy(item => item.Start).ToArray();
        var inventoryNames = plan.Lines.Select(item => item.Name)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var inventory = plan.Lines.Select(ToProposalInventory).ToArray();
        var signature = OpportunityCommandSupport.Hash(
            $"{plan.Id:N}|{plan.Version}|{plan.InputHash}|{plan.TotalMinor}|" +
            string.Join('|', plan.Lines.Select(line =>
                $"{line.InventoryTenantId:N}:{line.MarketplaceListingVersionId:N}:" +
                $"{line.ProductVersionId:N}:{line.RateId:N}:" +
                $"{line.AvailabilityId:N}:{line.Quantity}:" +
                string.Join(',', line.RunningPeriods.Select(period => $"{period.Start:O}-{period.End:O}")))));
        return new ProposalPlanSnapshot(
            plan.Id, plan.VersionNumber, plan.TotalMinor, plan.Currency, plan.InputHash,
            channels, periods, inventoryNames, inventory, signature, plan.Lines.Select(line =>
                new ProposalPlanLineReference(
                    line.InventoryTenantId, line.MarketplaceListingVersionId,
                    line.InventoryProductId, line.ProductVersionId,
                    line.RateId, line.AvailabilityId,
                    line.RunningPeriods.Select(period => new ProposalRunningPeriodView(
                        line.Channel, period.Start, period.End)).ToArray())).ToArray());
    }

    private static ProposalInventoryLineView ToProposalInventory(
        Advertified.Commercial.Application.Planning.MediaPlanLineView line) => new(
        line.InventoryTenantId, line.MarketplaceListingVersionId,
        line.InventoryProductId, line.ProductVersionId, line.RateId,
        line.AvailabilityId, line.Name, line.Channel, line.Geography,
        line.RunningPeriods.Select(period => new ProposalRunningPeriodView(
            line.Channel, period.Start, period.End)).ToArray(),
        line.Quantity, line.ClientPriceMinor, line.FeesMinor, line.VatMinor,
        line.Availability, line.RateFreshness, line.SupplyConfidence,
        line.SupplySource, line.LastConfirmedAtUtc, Uncertainties(line),
        line.SupplierCommercial, line.CommercialTerms, line.Deliverable,
        line.Spatial, line.LogoAssetId);

    private static string[] Uncertainties(
        Advertified.Commercial.Application.Planning.MediaPlanLineView line)
    {
        var values = new List<string>();
        if (line.SupplyConfidence != MasterDataCodes.SupplyConfidenceStatuses.Confirmed)
        {
            values.Add("Supply is not confirmed for the full campaign period.");
        }
        if (line.RateFreshness != MasterDataCodes.RateFreshnessStatuses.Current)
        {
            values.Add("Rate validity requires reconfirmation.");
        }
        return values.ToArray();
    }
}

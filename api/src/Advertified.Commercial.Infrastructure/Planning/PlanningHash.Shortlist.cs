using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Infrastructure.Opportunity;

namespace Advertified.Commercial.Infrastructure.Planning;

internal static partial class PlanningHash
{
    internal static string ForShortlist(
        MediaMixRow mix,
        AudienceDefinitionSetView audience,
        IReadOnlyList<PlanningInventoryRow> inventory) => OpportunityCommandSupport.Hash(
            $"{mix.Id:N}|{mix.Version}|{mix.InputHash}|{audience.Id:N}|" +
            $"{audience.VersionNumber}|{audience.InputHash}|" + string.Join('|',
                audience.Definitions.OrderBy(item => item.Id).Select(item =>
                    $"{item.Id:N}:{item.Language}:{item.LifeStage}:{item.LsmSem}:" +
                    $"{item.LsmSemTaxonomy}:{item.LsmSemTaxonomyVersion}:" +
                    $"{string.Join(',', item.EvidenceItemIds.Order())}")) + "|" +
            string.Join('|', inventory.Select(item =>
                $"{item.InventoryTenantId:N}:{item.MarketplaceListingVersionId:N}:" +
                $"{item.ProductVersionId:N}:{item.RateId:N}:{item.AvailabilityId:N}:" +
                $"{item.AudienceProfileJson}")));

    internal static string ForInventory(PlanningInventoryRow item, string shortlistHash) =>
        OpportunityCommandSupport.Hash(
            $"{shortlistHash}|{item.InventoryTenantId:N}|" +
            $"{item.MarketplaceListingVersionId:N}|{item.ProductVersionId:N}|{item.RateId:N}|" +
            $"{item.AvailabilityId:N}|{item.RateAmountMinor}|{item.Currency}|" +
            $"{item.AudienceProfileJson}");
}

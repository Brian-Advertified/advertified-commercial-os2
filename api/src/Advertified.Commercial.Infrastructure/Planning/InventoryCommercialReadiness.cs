using System.Text.Json;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Planning;

internal static class InventoryCommercialReadiness
{
    private static readonly JsonSerializerOptions StoredJson =
        new(JsonSerializerDefaults.Web);

    internal static InventoryCommercialReadinessView Evaluate(PlanningInventoryRow value)
    {
        var gaps = new List<string>();
        var supplier = ReadSupplier(value.SupplierCommercialJson);
        if (string.IsNullOrWhiteSpace(value.SupplierVatStatus))
        {
            gaps.Add("inventory.supplierCommercial.vatStatus");
        }
        if (string.IsNullOrWhiteSpace(value.VatTreatment))
        {
            gaps.Add("inventory.rate.vatTreatment");
        }
        if (string.IsNullOrWhiteSpace(value.RateSource))
        {
            gaps.Add("inventory.rate.sourceEvidence");
        }
        if (!value.EffectiveFrom.HasValue || !value.EffectiveTo.HasValue)
        {
            gaps.Add("inventory.rate.validity");
        }
        if (string.IsNullOrWhiteSpace(value.AvailabilitySource))
        {
            gaps.Add("inventory.availability.sourceEvidence");
        }
        if (value.SupplierVatStatus == MasterDataCodes.VatStatuses.Registered &&
            string.IsNullOrWhiteSpace(supplier?.VatNumber))
        {
            gaps.Add("inventory.supplierCommercial.vatNumber");
        }
        if ((value.SupplierVatStatus == MasterDataCodes.VatStatuses.Registered &&
                value.VatTreatment == MasterDataCodes.VatTreatments.NotApplicable) ||
            (value.SupplierVatStatus is MasterDataCodes.VatStatuses.Exempt or
                    MasterDataCodes.VatStatuses.NotApplicable &&
                value.VatTreatment is MasterDataCodes.VatTreatments.Inclusive or
                    MasterDataCodes.VatTreatments.Exclusive))
        {
            gaps.Add("inventory.rate.vatTreatmentConsistency");
        }
        return new(value.SupplierVatStatus, value.VatTreatment, gaps, supplier?.VatNumber);
    }

    private static InventorySupplierCommercialValues? ReadSupplier(string? json) =>
        json is null ? null : JsonSerializer.Deserialize<InventorySupplierCommercialValues>(
            json, StoredJson);
}

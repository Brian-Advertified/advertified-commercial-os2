using Advertified.Commercial.Application.Inventory;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static partial class InventoryCandidateValueNormalization
{
    private static InventorySupplierCommercialValues? NormalizeSupplier(
        InventorySupplierCommercialValues? value) => value is null ? null : value with
    {
        VatStatus = Code(value.VatStatus), VatNumber = Text(value.VatNumber),
        CommissionTerms = Text(value.CommissionTerms), PaymentTerms = Text(value.PaymentTerms),
        CancellationTerms = Text(value.CancellationTerms),
        BookingDeadlineTerms = Text(value.BookingDeadlineTerms),
    };

    private static InventorySupplierContactValue[] NormalizeContacts(
        IReadOnlyList<InventorySupplierContactValue>? values) => (values ?? [])
        .Select(value => value with
        {
            Name = Text(value.Name), Role = Text(value.Role), Region = Text(value.Region),
            Email = Text(value.Email)?.ToLowerInvariant(), Phone = Text(value.Phone),
            Website = Text(value.Website), SocialHandle = Text(value.SocialHandle),
        })
        .Where(value => ContactKey(value) is not null)
        .GroupBy(ContactKey, StringComparer.OrdinalIgnoreCase)
        .Select(group => group.First()).ToArray();

    private static string? ContactKey(InventorySupplierContactValue value) =>
        value.Email ?? value.Phone ?? value.Website ?? value.SocialHandle ?? value.Name;

    private static InventoryCommercialTermsValues? NormalizeCommercial(
        InventoryCommercialTermsValues? value) => value is null ? null : value with
    {
        VatTreatment = Code(value.VatTreatment), DiscountTerms = Text(value.DiscountTerms),
        Inclusions = Texts(value.Inclusions), Exclusions = Texts(value.Exclusions),
        Conditions = Texts(value.Conditions), CancellationTerms = Text(value.CancellationTerms),
    };

    private static InventoryDeliverableValues? NormalizeDeliverable(
        InventoryDeliverableValues? value) => value is null ? null : value with
    {
        Format = Text(value.Format), BuyingUnit = Text(value.BuyingUnit),
        Dimensions = Text(value.Dimensions), Placement = Text(value.Placement),
        Programme = Text(value.Programme), Daypart = Text(value.Daypart),
        CreativeSpecification = Text(value.CreativeSpecification),
    };

    private static InventorySpatialValues? NormalizeSpatial(
        InventorySpatialValues? value) => value is null ? null : value with
    {
        Country = Text(value.Country), Province = Text(value.Province),
        Municipality = Text(value.Municipality), Locality = Text(value.Locality),
        Venue = Text(value.Venue), Road = Text(value.Road), Route = Text(value.Route),
        TrafficDirection = Text(value.TrafficDirection),
        PointsOfInterest = (value.PointsOfInterest ?? [])
            .Select(item => item with
            {
                Name = Text(item.Name) ?? string.Empty, Category = Text(item.Category),
            }).Where(item => item.Name.Length > 0)
            .GroupBy(item => $"{item.Name}|{item.Latitude}|{item.Longitude}",
                StringComparer.OrdinalIgnoreCase).Select(group => group.First()).ToArray(),
        CoverageGeoJson = Text(value.CoverageGeoJson),
        CatchmentGeoJson = Text(value.CatchmentGeoJson),
        RouteGeoJson = Text(value.RouteGeoJson),
        DirectionGeoJson = Text(value.DirectionGeoJson),
    };

    private static InventoryPackageValues? NormalizePackage(
        InventoryPackageValues? value) => value is null ? null : value with
    {
        PackageCode = Text(value.PackageCode), PackageName = Text(value.PackageName),
        ComponentProductCodes = Texts(value.ComponentProductCodes),
        DiscountRule = Text(value.DiscountRule), Conditions = Texts(value.Conditions),
    };

    private static string[] Texts(IReadOnlyList<string>? values) => (values ?? [])
        .Select(Text).Where(value => value is not null).Select(value => value!)
        .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    private static void EnsureStructuredLimits(InventoryCandidateValues values)
    {
        var supplier = values.SupplierCommercial;
        if (supplier is not null)
        {
            Limit(supplier.VatStatus, 100, nameof(values.SupplierCommercial));
            Limit(supplier.VatNumber, 100, nameof(values.SupplierCommercial));
            Limit(supplier.CommissionTerms, 2_000, nameof(values.SupplierCommercial));
            Limit(supplier.PaymentTerms, 2_000, nameof(values.SupplierCommercial));
            Limit(supplier.CancellationTerms, 2_000, nameof(values.SupplierCommercial));
            Limit(supplier.BookingDeadlineTerms, 2_000, nameof(values.SupplierCommercial));
        }
        foreach (var contact in values.SupplierContacts ?? [])
        {
            Limit(contact.Name, 300, nameof(values.SupplierContacts));
            Limit(contact.Role, 200, nameof(values.SupplierContacts));
            Limit(contact.Region, 200, nameof(values.SupplierContacts));
            Limit(contact.Email, 320, nameof(values.SupplierContacts));
            Limit(contact.Phone, 100, nameof(values.SupplierContacts));
            Limit(contact.Website, 1_000, nameof(values.SupplierContacts));
            Limit(contact.SocialHandle, 300, nameof(values.SupplierContacts));
        }
        EnsureCommercialLimits(values.CommercialTerms);
        EnsureDeliverableLimits(values.Deliverable);
        EnsureSpatialLimits(values.Spatial);
        EnsurePackageLimits(values.Package);
    }

    private static void EnsureCommercialLimits(InventoryCommercialTermsValues? value)
    {
        if (value is null) return;
        Limit(value.VatTreatment, 100, nameof(value));
        Limit(value.DiscountTerms, 2_000, nameof(value));
        Limit(value.CancellationTerms, 2_000, nameof(value));
        foreach (var text in value.Inclusions.Concat(value.Exclusions).Concat(value.Conditions))
            Limit(text, 1_000, nameof(value));
    }

    private static void EnsureDeliverableLimits(InventoryDeliverableValues? value)
    {
        if (value is null) return;
        foreach (var text in new[] { value.Format, value.BuyingUnit, value.Dimensions,
                     value.Placement, value.Programme, value.Daypart })
            Limit(text, 500, nameof(value));
        Limit(value.CreativeSpecification, 4_000, nameof(value));
    }

    private static void EnsureSpatialLimits(InventorySpatialValues? value)
    {
        if (value is null) return;
        foreach (var text in new[] { value.Country, value.Province, value.Municipality,
                     value.Locality, value.Venue, value.Road, value.Route, value.TrafficDirection })
            Limit(text, 500, nameof(value));
        foreach (var point in value.PointsOfInterest)
        {
            Limit(point.Name, 500, nameof(value)); Limit(point.Category, 200, nameof(value));
        }
        foreach (var json in new[] { value.CoverageGeoJson, value.CatchmentGeoJson,
                     value.RouteGeoJson, value.DirectionGeoJson })
            Limit(json, 100_000, nameof(value));
    }

    private static void EnsurePackageLimits(InventoryPackageValues? value)
    {
        if (value is null) return;
        Limit(value.PackageCode, 200, nameof(value)); Limit(value.PackageName, 500, nameof(value));
        Limit(value.DiscountRule, 2_000, nameof(value));
        foreach (var text in value.ComponentProductCodes) Limit(text, 200, nameof(value));
        foreach (var text in value.Conditions) Limit(text, 1_000, nameof(value));
    }
}

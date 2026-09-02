using System.Globalization;
using Advertified.Commercial.Application.Inventory;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static partial class InventoryCandidateNormalizer
{
    private static InventorySupplierCommercialValues? SupplierCommercial(
        Dictionary<string, string> values)
    {
        var result = new InventorySupplierCommercialValues(
            Code(values, "supplier_vat_status"), Text(values, "supplier_vat_number"),
            Text(values, "supplier_commission_terms"), Text(values, "supplier_payment_terms"),
            Text(values, "supplier_cancellation_terms"),
            Text(values, "supplier_booking_deadline_terms"));
        return result.GetType().GetProperties().Any(property =>
            property.GetValue(result) is not null) ? result : null;
    }

    private static InventorySupplierContactValue[] SupplierContacts(
        Dictionary<string, string> values)
    {
        var contact = new InventorySupplierContactValue(
            Text(values, "contact_name"), Text(values, "contact_role"),
            Text(values, "contact_region"), Text(values, "contact_email")?.ToLowerInvariant(),
            Text(values, "contact_phone"), Text(values, "contact_website"),
            Text(values, "contact_social"));
        return contact.GetType().GetProperties().Any(property =>
            property.GetValue(contact) is not null) ? [contact] : [];
    }

    private static InventoryCommercialTermsValues? CommercialTerms(
        Dictionary<string, string> values)
    {
        var result = new InventoryCommercialTermsValues(
            Code(values, "vat_treatment"), Date(values, "rate_valid_from"),
            Date(values, "rate_valid_to"), Long(values, "production_cost_minor"),
            Long(values, "installation_cost_minor"), Int(values, "minimum_order"),
            Text(values, "discount_terms"), List(values, "inclusions"),
            List(values, "exclusions"), List(values, "conditions"),
            Int(values, "booking_lead_time_days"), Date(values, "booking_deadline"),
            Date(values, "material_deadline"), Text(values, "cancellation_terms"));
        return HasCommercialValue(result) ? result : null;
    }

    private static bool HasCommercialValue(InventoryCommercialTermsValues value) =>
        value.VatTreatment is not null || value.RateValidFrom.HasValue ||
        value.RateValidTo.HasValue || value.ProductionCostMinor.HasValue ||
        value.InstallationCostMinor.HasValue || value.MinimumOrder.HasValue ||
        value.DiscountTerms is not null || value.Inclusions.Count > 0 ||
        value.Exclusions.Count > 0 || value.Conditions.Count > 0 ||
        value.BookingLeadTimeDays.HasValue || value.BookingDeadline.HasValue ||
        value.MaterialDeadline.HasValue || value.CancellationTerms is not null;

    private static InventoryDeliverableValues? Deliverable(
        Dictionary<string, string> values)
    {
        var result = new InventoryDeliverableValues(
            Text(values, "format"), Text(values, "buying_unit"),
            Text(values, "dimensions"), Text(values, "placement"),
            Text(values, "programme"), Text(values, "daypart"),
            Int(values, "spot_length_seconds"), Int(values, "loop_length_seconds"),
            Int(values, "slot_length_seconds"), Int(values, "plays_per_loop"),
            Int(values, "deliverable_quantity"), Text(values, "creative_specification"));
        return result.GetType().GetProperties().Any(property =>
            property.GetValue(result) is not null) ? result : null;
    }

    private static InventorySpatialValues? Spatial(Dictionary<string, string> values)
    {
        var points = List(values, "points_of_interest")
            .Select(name => new InventoryPointOfInterestValue(name, null, null, null))
            .ToList();
        var poiName = Text(values, "poi_name");
        if (poiName is not null)
        {
            points.Add(new(poiName, Text(values, "poi_category"),
                Decimal(values, "poi_latitude"), Decimal(values, "poi_longitude")));
        }
        var result = new InventorySpatialValues(
            Text(values, "country"), Text(values, "province"),
            Text(values, "municipality"), Text(values, "locality"),
            Text(values, "venue"), Text(values, "road"), Text(values, "route"),
            Text(values, "traffic_direction"), Decimal(values, "facing_bearing_degrees"),
            points, Text(values, "coverage_geojson"), Text(values, "catchment_geojson"),
            Text(values, "route_geojson"), Text(values, "direction_geojson"));
        return HasSpatialValue(result) ? result : null;
    }

    private static bool HasSpatialValue(InventorySpatialValues value) =>
        value.Country is not null || value.Province is not null ||
        value.Municipality is not null || value.Locality is not null ||
        value.Venue is not null || value.Road is not null || value.Route is not null ||
        value.TrafficDirection is not null || value.FacingBearingDegrees.HasValue ||
        value.PointsOfInterest.Count > 0 || value.CoverageGeoJson is not null ||
        value.CatchmentGeoJson is not null || value.RouteGeoJson is not null ||
        value.DirectionGeoJson is not null;

    private static InventoryPackageValues? Package(Dictionary<string, string> values)
    {
        var result = new InventoryPackageValues(
            Text(values, "package_code"), Text(values, "package_name"),
            List(values, "package_component_codes"), Text(values, "package_discount_rule"),
            List(values, "package_conditions"));
        return result.PackageCode is not null || result.PackageName is not null ||
            result.ComponentProductCodes.Count > 0 || result.DiscountRule is not null ||
            result.Conditions.Count > 0 ? result : null;
    }

    private static DateOnly? Date(Dictionary<string, string> values, string field) =>
        values.TryGetValue(field, out var raw) && DateOnly.TryParse(
            raw, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var value)
            ? value : null;

    private static int? Int(Dictionary<string, string> values, string field) =>
        values.TryGetValue(field, out var raw) && int.TryParse(
            raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value : null;

    private static long? Long(Dictionary<string, string> values, string field) =>
        values.TryGetValue(field, out var raw) && long.TryParse(
            raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value : null;

    private static string[] List(Dictionary<string, string> values, string field) =>
        !values.TryGetValue(field, out var raw) ? [] : raw.Split(
            [';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static void AddStructuredAliases(Dictionary<string, string> aliases)
    {
        AddSupplierAliases(aliases);
        AddCommercialAliases(aliases);
        AddDeliverableAliases(aliases);
        AddSpatialAliases(aliases);
        AddPackageAliases(aliases);
    }

    private static void AddSupplierAliases(Dictionary<string, string> result)
    {
        Add(result, "description", "description", "productdescription", "sitedescription");
        Add(result, "supplier_vat_status", "suppliervatstatus", "vatstatus");
        Add(result, "supplier_vat_number", "suppliervatnumber", "vatnumber");
        Add(result, "supplier_commission_terms", "commissionterms", "suppliercommission");
        Add(result, "supplier_payment_terms", "paymentterms", "supplierpaymentterms");
        Add(result, "supplier_cancellation_terms", "suppliercancellationterms");
        Add(result, "supplier_booking_deadline_terms", "supplierbookingdeadlineterms");
        Add(result, "contact_name", "contactname", "salescontact", "representative");
        Add(result, "contact_role", "contactrole", "contacttitle");
        Add(result, "contact_region", "contactregion", "salesregion");
        Add(result, "contact_email", "contactemail", "salesemail", "email");
        Add(result, "contact_phone", "contactphone", "salesphone", "telephone", "phone");
        Add(result, "contact_website", "website", "supplierwebsite");
        Add(result, "contact_social", "socialhandle", "socialmedia");
    }

    private static void AddCommercialAliases(Dictionary<string, string> result)
    {
        Add(result, "vat_treatment", "vattreatment", "vatbasis", "ratevatbasis");
        Add(result, "rate_valid_from", "ratevalidfrom", "validfrom", "effectivefrom");
        Add(result, "rate_valid_to", "ratevalidto", "validto", "effectiveto", "rateexpiry");
        Add(result, "production_cost_minor", "productioncostminor");
        Add(result, "installation_cost_minor", "installationcostminor", "installcostminor");
        Add(result, "minimum_order", "minimumorder", "minimumquantity");
        Add(result, "discount_terms", "discountterms", "discount");
        Add(result, "inclusions", "inclusions", "included");
        Add(result, "exclusions", "exclusions", "excluded");
        Add(result, "conditions", "conditions", "termsandconditions");
        Add(result, "booking_lead_time_days", "bookingleadtimedays", "leadtimedays");
        Add(result, "booking_deadline", "bookingdeadline");
        Add(result, "material_deadline", "materialdeadline", "creativedeadline");
        Add(result, "cancellation_terms", "cancellationterms");
    }

    private static void AddDeliverableAliases(Dictionary<string, string> result)
    {
        Add(result, "format", "format", "mediaformat", "siteformat");
        Add(result, "buying_unit", "buyingunit", "deliveryunit");
        Add(result, "dimensions", "dimensions", "size", "screensize");
        Add(result, "placement", "placement", "section", "position");
        Add(result, "programme", "programme", "program", "show");
        Add(result, "daypart", "daypart", "timeslot");
        Add(result, "spot_length_seconds", "spotlengthseconds", "spotdurationseconds");
        Add(result, "loop_length_seconds", "looplengthseconds", "loopdurationseconds");
        Add(result, "slot_length_seconds", "slotlengthseconds", "slotdurationseconds");
        Add(result, "plays_per_loop", "playsperloop", "shareofloop");
        Add(result, "deliverable_quantity", "deliverablequantity", "spots", "insertions");
        Add(result, "creative_specification", "creativespecification", "materialspecification");
    }

    private static void AddSpatialAliases(Dictionary<string, string> result)
    {
        Add(result, "country", "country"); Add(result, "province", "province", "region");
        Add(result, "municipality", "municipality", "district");
        Add(result, "locality", "locality", "suburb", "town", "city");
        Add(result, "venue", "venue", "mall"); Add(result, "road", "road", "street");
        Add(result, "route", "route", "transitroute");
        Add(result, "traffic_direction", "trafficdirection", "facingdirection");
        Add(result, "facing_bearing_degrees", "facingbearing", "bearingdegrees");
        Add(result, "points_of_interest", "pointsofinterest", "pois");
        Add(result, "poi_name", "poiname"); Add(result, "poi_category", "poicategory");
        Add(result, "poi_latitude", "poilatitude"); Add(result, "poi_longitude", "poilongitude");
        Add(result, "coverage_geojson", "coveragegeojson");
        Add(result, "catchment_geojson", "catchmentgeojson");
        Add(result, "route_geojson", "routegeojson");
        Add(result, "direction_geojson", "directiongeojson");
    }

    private static void AddPackageAliases(Dictionary<string, string> result)
    {
        Add(result, "package_code", "packagecode"); Add(result, "package_name", "packagename");
        Add(result, "package_component_codes", "packagecomponents", "componentproductcodes");
        Add(result, "package_discount_rule", "packagediscountrule");
        Add(result, "package_conditions", "packageconditions");
    }
}

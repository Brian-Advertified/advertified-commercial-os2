using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static partial class InventoryCandidateNormalizer
{
    private static Dictionary<string, string> BuildAliases()
    {
        var result = new Dictionary<string, string>(
            StringComparer.Ordinal);
        Add(
            result,
            "supplier_name",
            "supplier",
            "suppliername",
            "mediaowner",
            "mediaownername",
            "saleshouse",
            "publisher",
            "brand");
        Add(
            result,
            "product_code",
            "productcode",
            "code",
            "id",
            "siteid",
            "stationcode");
        Add(
            result,
            "name",
            "name",
            "product",
            "productname",
            "sitename",
            "station",
            "stationname",
            "platform",
            "platformname");
        Add(
            result,
            "channel",
            "channel",
            "mediachannel",
            "mediatype",
            "mediumtype");
        Add(result, "product_type", "producttype", "placementtype");
        Add(result, "geography", "geography", "location", "market", "area");
        Add(result, "address", "address", "siteaddress");
        Add(result, "latitude", "latitude", "lat");
        Add(result, "longitude", "longitude", "lon", "lng");
        Add(
            result,
            "rate_type",
            "ratetype",
            "pricingmodel",
            "rateperiod");
        Add(result, "currency", "currency", "currencycode");
        Add(
            result,
            "rate_minor",
            "rateamountminor",
            "rateminor",
            "priceminor");
        Add(
            result,
            "rate",
            "rate",
            "price",
            "amount",
            "cost",
            "packagecost",
            "investment",
            "netcost",
            "grosscost",
            "baseprice",
            "cpm",
            "netrate",
            "discountedrate",
            "ratecard");
        Add(result, "availability", "availability", "availabilitystatus");
        Add(
            result,
            "spoken_languages",
            "language",
            "languages",
            "spokenlanguage",
            "spokenlanguages",
            "audiencelanguage",
            "audiencelanguages");
        Add(
            result,
            "understood_languages",
            "understoodlanguage",
            "understoodlanguages");
        Add(
            result,
            "life_stages",
            "lifestage",
            "lifestages",
            "agegroup",
            "agegroups");
        Add(
            result,
            "lsm_sem_segments",
            "lsm",
            "sem",
            "lsmsem",
            "lsmsegments",
            "semsegments",
            "lsmsemsegments");
        Add(
            result,
            "audience_taxonomy",
            "audiencetaxonomy",
            "segmenttaxonomy");
        Add(
            result,
            "audience_taxonomy_version",
            "audiencetaxonomyversion",
            "segmenttaxonomyversion",
            "lsmversion",
            "semversion");
        Add(result, "audience_universe", "audienceuniverse", "universe");
        Add(
            result,
            "audience_measurement_source",
            "audiencesource",
            "measurementsource",
            "researchsource");
        Add(
            result,
            "audience_measurement_period",
            "audienceperiod",
            "measurementperiod",
            "researchperiod");
        Add(
            result,
            "audience_methodology",
            "audiencemethodology",
            "measurementmethodology",
            "researchmethodology");
        Add(
            result,
            "audience_limitations",
            "audiencelimitations",
            "measurementlimitations");
        Add(
            result,
            "audience_reach",
            MasterDataCodes.InventoryUnsupportedClaimTerms.Reach,
            "audiencereach",
            "monthlyreach");
        Add(
            result,
            "audience_reach_unit",
            "reachunit",
            "audiencereachunit");
        Add(
            result,
            "audience_listenership",
            "listenership",
            MasterDataCodes.InventoryUnsupportedClaimTerms.Listeners,
            "monthlylisteners");
        Add(
            result,
            "audience_listenership_unit",
            "listenershipunit",
            "listenersunit");
        Add(
            result,
            "audience_footfall",
            "footfall",
            "monthlyfootfall",
            "dailyfootfall");
        Add(result, "audience_footfall_unit", "footfallunit");
        Add(
            result,
            "audience_impressions",
            MasterDataCodes.InventoryUnsupportedClaimTerms.Impressions,
            "estimatedimpressions");
        Add(result, "audience_impressions_unit", "impressionsunit");
        AddStructuredAliases(result);
        return result;
    }

    private static void Add(
        Dictionary<string, string> aliases,
        string field,
        params string[] values)
    {
        foreach (var value in values)
        {
            if (!aliases.TryAdd(value, field))
            {
                throw new InvalidOperationException(
                    $"Inventory alias '{value}' has multiple owners.");
            }
        }
    }
}

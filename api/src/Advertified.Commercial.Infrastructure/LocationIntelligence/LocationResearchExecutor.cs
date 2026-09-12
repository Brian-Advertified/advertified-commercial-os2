using Advertified.Commercial.Application.Intelligence;
using Advertified.Commercial.Application.LocationIntelligence;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.LocationIntelligence;

internal sealed record LocationResearchExecutionResult(
    IReadOnlyList<PlaceResearchQueryProposal> ResearchQueries,
    IReadOnlyList<ResolvedLocationPlace> Places,
    IReadOnlyList<string> EvidenceGaps);

public sealed class LocationResearchExecutor(
    ILocationDiscoveryProvider locationDiscovery,
    IPoiDiscoveryProvider poiDiscovery)
{
    private const int MaximumPlacesPerQuery = 10;
    private const int MaximumResolvedPlaces = 20;
    private const int MaximumResearchQueries = 8;
    private const int MaximumDerivedAnchorsPerBroadQuery = 4;

    internal async Task<LocationResearchExecutionResult> ExecuteAsync(
        IReadOnlyList<PlaceResearchQueryProposal> queries,
        IReadOnlyList<ReferenceObservationFact> referenceEvidence,
        CancellationToken cancellationToken)
    {
        var effectiveQueries = new List<PlaceResearchQueryProposal>();
        var effectiveKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var resolved = new Dictionary<string, ResolvedLocationPlace>(StringComparer.Ordinal);
        var gaps = new List<string>();

        foreach (var request in queries)
        {
            if (effectiveQueries.Count >= MaximumResearchQueries || resolved.Count >= MaximumResolvedPlaces)
                break;

            var canonicalRequest = CanonicalizePurpose(request);
            var failure = await ExecuteOneAsync(
                canonicalRequest,
                effectiveQueries,
                effectiveKeys,
                resolved,
                gaps,
                cancellationToken);
            if (failure != PoiDiscoveryScopeFailure.TooBroad)
                continue;

            if (!string.Equals(
                    request.AnchorGeography.Trim(),
                    "South Africa",
                    StringComparison.OrdinalIgnoreCase))
            {
                gaps.Add($"{request.AnchorGeography} is too broad for bounded {request.PoiCategory} discovery and no governed decomposition is available.");
                continue;
            }

            var subGeographies = referenceEvidence
                .Where(item =>
                    !string.IsNullOrWhiteSpace(item.GeographyName) &&
                    !string.Equals(item.GeographyName, request.AnchorGeography, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(item.GeographyLevel, "COUNTRY", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(item.GeographyLevel, "NATIONAL", StringComparison.OrdinalIgnoreCase))
                .Select(item => item.GeographyName.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .Take(MaximumDerivedAnchorsPerBroadQuery)
                .ToArray();
            if (subGeographies.Length == 0)
            {
                gaps.Add($"{request.AnchorGeography} is too broad for bounded {request.PoiCategory} discovery and no governed sub-geographies were available.");
                continue;
            }

            gaps.Add(
                $"The broad {request.PoiCategory} request for {request.AnchorGeography} was decomposed into governed sub-geographies; those derived searches are hypotheses and do not prove audience presence.");
            foreach (var geography in subGeographies)
            {
                if (effectiveQueries.Count >= MaximumResearchQueries || resolved.Count >= MaximumResolvedPlaces)
                    break;
                var derived = new PlaceResearchQueryProposal(
                    request.PoiCategory,
                    geography,
                    CanonicalPurpose(request.PoiCategory, geography),
                    request.Priority,
                    MasterDataCodes.EvidenceClassifications.Hypothesis,
                    []);
                var derivedFailure = await ExecuteOneAsync(
                    derived,
                    effectiveQueries,
                    effectiveKeys,
                    resolved,
                    gaps,
                    cancellationToken);
                if (derivedFailure is not null)
                    gaps.Add($"Bounded {request.PoiCategory} research within {geography} could not be completed: {derivedFailure}.");
            }
        }

        if (resolved.Count >= MaximumResolvedPlaces)
            gaps.Add("Location Intelligence reached the governed overall POI cap; additional POIs were not retained.");
        if (effectiveQueries.Count >= MaximumResearchQueries && queries.Count > effectiveQueries.Count)
            gaps.Add("Location Intelligence reached the governed research-query cap; additional research requests were not executed.");

        return new LocationResearchExecutionResult(
            effectiveQueries.ToArray(),
            resolved.Values.ToArray(),
            gaps.Distinct(StringComparer.Ordinal).ToArray());
    }

    private async Task<PoiDiscoveryScopeFailure?> ExecuteOneAsync(
        PlaceResearchQueryProposal request,
        List<PlaceResearchQueryProposal> effectiveQueries,
        HashSet<string> effectiveKeys,
        Dictionary<string, ResolvedLocationPlace> resolved,
        List<string> gaps,
        CancellationToken cancellationToken)
    {
        _ = OpenStreetMapPoiCategoryCatalog.Resolve(request.PoiCategory);
        var key = $"{request.PoiCategory.Trim()}|{request.AnchorGeography.Trim()}";
        if (!effectiveKeys.Add(key))
            return null;

        var anchors = await locationDiscovery.SearchAsync(request.AnchorGeography, cancellationToken);
        var anchor = SelectAnchor(request.AnchorGeography, anchors);
        if (anchor is null)
        {
            gaps.Add($"Could not resolve the governed geography '{request.AnchorGeography}' to a verified South African anchor.");
            return null;
        }

        var remaining = MaximumResolvedPlaces - resolved.Count;
        if (remaining <= 0)
            return null;
        var requestedLimit = Math.Min(MaximumPlacesPerQuery, remaining);
        IReadOnlyList<DiscoveredPoi> places;
        try
        {
            places = await poiDiscovery.DiscoverAsync(
                new PoiDiscoveryRequest(request.PoiCategory, anchor, requestedLimit),
                cancellationToken);
        }
        catch (PoiDiscoveryScopeException exception)
        {
            effectiveKeys.Remove(key);
            if (exception.Failure != PoiDiscoveryScopeFailure.TooBroad)
                gaps.Add(exception.Message);
            return exception.Failure;
        }
        catch (PoiDiscoveryProviderException exception)
        {
            effectiveKeys.Remove(key);
            gaps.Add($"POI discovery for {request.PoiCategory} within {anchor.Name} is incomplete: {exception.Message}");
            return null;
        }

        effectiveQueries.Add(request);
        if (places.Count == 0)
        {
            gaps.Add($"No verified {request.PoiCategory} POIs were returned within {anchor.Name}; this does not prove that none exist.");
            return null;
        }
        if (places.Count >= requestedLimit)
            gaps.Add($"{request.PoiCategory} discovery within {anchor.Name} reached the governed result cap and is not a complete POI census.");

        foreach (var place in places)
        {
            if (resolved.Count >= MaximumResolvedPlaces)
                break;
            resolved.TryAdd(place.Id, new ResolvedLocationPlace(
                place.Id,
                $"{request.PoiCategory} in {anchor.Name}",
                request.Purpose,
                place.Name,
                place.DisplayLocation,
                place.Latitude,
                place.Longitude,
                place.SourceLocator,
                place.Attribution,
                place.GeometryBasis));
        }
        return null;
    }

    private static PlaceResearchQueryProposal CanonicalizePurpose(PlaceResearchQueryProposal request) =>
        request with { Purpose = CanonicalPurpose(request.PoiCategory, request.AnchorGeography) };

    private static string CanonicalPurpose(string poiCategory, string geography) =>
        $"Verify {poiCategory} POIs within {geography} as contextual places requested or permitted by the research frame. POI existence does not establish target-audience presence or visitation.";

    private static DiscoveredLocation? SelectAnchor(
        string requestedGeography,
        IReadOnlyList<DiscoveredLocation> candidates)
    {
        var geography = requestedGeography.Trim();
        return candidates.FirstOrDefault(item =>
                   item.Bounds is not null &&
                   string.Equals(item.Name.Trim(), geography, StringComparison.OrdinalIgnoreCase))
               ?? candidates.FirstOrDefault(item =>
                   item.Bounds is not null &&
                   item.Address.Contains(geography, StringComparison.OrdinalIgnoreCase));
    }
}

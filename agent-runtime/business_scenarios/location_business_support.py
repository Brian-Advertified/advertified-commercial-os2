"""Shared live Location Intelligence scenario support.

This module exists only to exercise the same governed provider shape used by the application:
Nominatim resolves named South African anchors and Overpass enumerates governed POI categories
inside bounded areas. It never searches for audiences and never invents narrower geographies.
"""

from __future__ import annotations

import json
import math
import time
import urllib.error
import urllib.parse
import urllib.request
from decimal import Decimal, InvalidOperation

from location_intelligence_contracts import (
    LocationCommercialContext,
    LocationPoiCategoryOption,
    LocationResearchPlanArtifact,
    PlaceResearchQuery,
    ResolvedPlace,
)

USER_AGENT = "Advertified-Commercial-OS2-BusinessScenario/1.0"

CATEGORY_DEFINITIONS = {
    "PLACE_OF_WORSHIP": ("Place of worship", (("amenity", "place_of_worship"),)),
    "MOSQUE": ("Mosque", (("amenity", "place_of_worship"), ("religion", "muslim"))),
    "HINDU_TEMPLE": ("Hindu temple", (("amenity", "place_of_worship"), ("religion", "hindu"))),
    "CHURCH": ("Church", (("amenity", "place_of_worship"), ("religion", "christian"))),
    "SYNAGOGUE": ("Synagogue", (("amenity", "place_of_worship"), ("religion", "jewish"))),
    "SHOPPING_CENTRE": ("Shopping centre / mall", (("shop", "mall"),)),
    "MARKET": ("Market / marketplace", (("amenity", "marketplace"),)),
    "SUPERMARKET": ("Supermarket", (("shop", "supermarket"),)),
    "TAXI_RANK": ("Taxi rank / taxi stand", (("amenity", "taxi"),)),
    "BUS_STATION": ("Bus station", (("amenity", "bus_station"),)),
    "RAILWAY_STATION": ("Railway station", (("railway", "station"),)),
    "AIRPORT": ("Airport / aerodrome", (("aeroway", "aerodrome"),)),
    "SCHOOL": ("School", (("amenity", "school"),)),
    "UNIVERSITY": ("University", (("amenity", "university"),)),
    "HOSPITAL": ("Hospital", (("amenity", "hospital"),)),
    "CLINIC": ("Clinic", (("amenity", "clinic"),)),
    "PHARMACY": ("Pharmacy", (("amenity", "pharmacy"),)),
    "STADIUM": ("Stadium", (("leisure", "stadium"),)),
    "PARK": ("Park", (("leisure", "park"),)),
    "COMMUNITY_CENTRE": ("Community centre", (("amenity", "community_centre"),)),
    "GOVERNMENT_OFFICE": ("Government office", (("office", "government"),)),
}


def available_poi_categories() -> tuple[LocationPoiCategoryOption, ...]:
    return tuple(
        LocationPoiCategoryOption(code=code, label=definition[0])
        for code, definition in sorted(CATEGORY_DEFINITIONS.items())
    )


def _caps_reached(effective_queries, resolved, maximum_queries: int, maximum_places: int) -> bool:
    return len(effective_queries) >= maximum_queries or len(resolved) >= maximum_places


def _resolve_query_bounds(item, key, executed, gaps):
    time.sleep(1.05)
    try:
        anchor, bounds = _resolve_anchor(item.anchor_geography)
    except (urllib.error.URLError, TimeoutError) as error:
        executed.remove(key)
        gaps.append(
            f"Named-geography resolution for {item.anchor_geography} is incomplete: {type(error).__name__}."
        )
        return None, "PROVIDER"
    if anchor is None or bounds is None:
        gaps.append(
            f"Could not resolve the governed geography '{item.anchor_geography}' to a verified South African anchor."
        )
        return None, "UNRESOLVED"
    if _too_broad(bounds):
        executed.remove(key)
        return None, "TOO_BROAD"
    if not _inside_south_africa(bounds):
        gaps.append(f"{item.anchor_geography} resolved outside the governed South Africa discovery boundary.")
        return None, "OUTSIDE"
    return bounds, None


def _discover_query_places(item, key, bounds, executed, effective_queries, resolved, gaps,
                           maximum_places: int, places_per_query: int) -> str | None:
    governed = item.model_copy(update={"purpose": (
        f"Verify {item.poi_category} POIs within {item.anchor_geography} as contextual places requested "
        "or permitted by the research frame. POI existence does not establish target-audience presence or visitation."
    )})
    time.sleep(1.05)
    records, provider_gap = _overpass(
        item.poi_category, bounds, min(places_per_query, maximum_places - len(resolved))
    )
    if provider_gap is not None:
        executed.remove(key)
        gaps.append(
            f"POI discovery for {item.poi_category} within {item.anchor_geography} is incomplete: {provider_gap}."
        )
        return "PROVIDER"
    effective_queries.append(governed)
    if not records:
        gaps.append(
            f"No verified {item.poi_category} POIs were returned within {item.anchor_geography}; "
            "this does not prove that none exist."
        )
        return None
    if len(records) >= places_per_query:
        gaps.append(
            f"{item.poi_category} discovery within {item.anchor_geography} reached the governed result cap "
            "and is not a complete POI census."
        )
    for record in records:
        if len(resolved) >= maximum_places:
            break
        place = _to_resolved_place(record, governed, bounds)
        if place is not None:
            resolved.setdefault(place.place_id, place)
    return None


def _execute_query(item, executed, effective_queries, resolved, gaps,
                   maximum_queries: int, maximum_places: int, places_per_query: int) -> str | None:
    if _caps_reached(effective_queries, resolved, maximum_queries, maximum_places):
        return "CAP"
    if item.poi_category not in CATEGORY_DEFINITIONS:
        gaps.append(f"Unsupported governed POI category {item.poi_category} was not executed.")
        return "UNSUPPORTED"
    key = (item.poi_category, item.anchor_geography.casefold().strip())
    if key in executed:
        return None
    executed.add(key)
    bounds, failure = _resolve_query_bounds(item, key, executed, gaps)
    if failure is not None:
        return failure
    return _discover_query_places(
        item, key, bounds, executed, effective_queries, resolved, gaps, maximum_places, places_per_query
    )


def _derived_query(item, geography: str) -> PlaceResearchQuery:
    return PlaceResearchQuery(
        poi_category=item.poi_category,
        anchor_geography=geography,
        purpose=(
            f"Verify {item.poi_category} POIs within {geography} as contextual places requested or permitted "
            "by the research frame. POI existence does not establish target-audience presence or visitation."
        ),
        priority=item.priority,
        classification="HYPOTHESIS",
        reference_observation_ids=(),
    )


def _effective_plan(plan, effective_queries, gaps):
    rationale = (
        "Execute only the governed bounded POI research requests: "
        + "; ".join(f"{item.poi_category} in {item.anchor_geography}" for item in effective_queries)
        + "."
        if effective_queries
        else "No bounded Location Intelligence POI research request could be executed safely."
    )
    return plan.model_copy(update={
        "queries": tuple(effective_queries),
        "rationale": rationale,
        "evidence_gaps": tuple(dict.fromkeys(gaps)),
    })


def resolve_bounded_pois(
    plan: LocationResearchPlanArtifact,
    context: LocationCommercialContext,
    *,
    maximum_queries: int = 8,
    maximum_places: int = 20,
    places_per_query: int = 5,
) -> tuple[LocationResearchPlanArtifact, tuple[ResolvedPlace, ...]]:
    resolved: dict[str, ResolvedPlace] = {}
    effective_queries: list[PlaceResearchQuery] = []
    executed: set[tuple[str, str]] = set()
    gaps = list(plan.evidence_gaps)
    governed_sub_geographies = _governed_sub_geographies(context)
    for item in plan.queries:
        if _caps_reached(effective_queries, resolved, maximum_queries, maximum_places):
            break
        failure = _execute_query(
            item, executed, effective_queries, resolved, gaps,
            maximum_queries, maximum_places, places_per_query,
        )
        if failure != "TOO_BROAD":
            continue
        if item.anchor_geography.casefold().strip() != "south africa":
            gaps.append(
                f"{item.anchor_geography} is too broad for bounded {item.poi_category} discovery and no governed "
                "decomposition is available."
            )
            continue
        if not governed_sub_geographies:
            gaps.append(
                f"South Africa is too broad for bounded {item.poi_category} discovery and no governed "
                "sub-geographies were available."
            )
            continue
        gaps.append(
            f"The broad {item.poi_category} request for South Africa was decomposed into governed sub-geographies; "
            "derived searches remain hypotheses and do not prove audience presence."
        )
        for geography in governed_sub_geographies[:4]:
            if _caps_reached(effective_queries, resolved, maximum_queries, maximum_places):
                break
            _execute_query(
                _derived_query(item, geography), executed, effective_queries, resolved, gaps,
                maximum_queries, maximum_places, places_per_query,
            )
    if len(resolved) >= maximum_places:
        gaps.append("Location Intelligence reached the governed overall POI cap; additional POIs were not retained.")
    if len(effective_queries) >= maximum_queries and len(plan.queries) > len(effective_queries):
        gaps.append("Location Intelligence reached the governed research-query cap; additional research was not executed.")
    return _effective_plan(plan, effective_queries, gaps), tuple(resolved.values())


def _governed_sub_geographies(context: LocationCommercialContext) -> tuple[str, ...]:
    result: list[str] = []
    seen: set[str] = set()
    for geography in context.geographies:
        value = geography.strip()
        key = value.casefold()
        if value and key != "south africa" and key not in seen:
            seen.add(key)
            result.append(value)
    for observation in context.reference_evidence:
        value = observation.geography_name.strip()
        key = value.casefold()
        if (
            value
            and key != "south africa"
            and observation.geography_level.casefold() not in {"country", "national"}
            and key not in seen
        ):
            seen.add(key)
            result.append(value)
    return tuple(result)


def _resolve_anchor(geography: str):
    url = "https://nominatim.openstreetmap.org/search?" + urllib.parse.urlencode({
        "format": "jsonv2",
        "countrycodes": "za",
        "addressdetails": "1",
        "limit": "10",
        "q": geography,
    })
    request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
    with urllib.request.urlopen(request, timeout=15) as response:
        records = json.loads(response.read(256 * 1024).decode("utf-8"))
    normalized = geography.casefold().strip()
    for record in records:
        address = record.get("address") or {}
        name = str(record.get("name") or "").strip()
        display = str(record.get("display_name") or "")
        bounds = record.get("boundingbox")
        if (
            address.get("country_code") == "za"
            and len(bounds or ()) == 4
            and (name.casefold() == normalized or normalized in display.casefold())
        ):
            south, north, west, east = (Decimal(str(value)) for value in bounds)
            return record, (south, north, west, east)
    return None, None


def _inside_south_africa(bounds) -> bool:
    south, north, west, east = bounds
    return (
        south >= Decimal("-35.2")
        and north <= Decimal("-21.7")
        and west >= Decimal("15.5")
        and east <= Decimal("33.5")
    )


def _too_broad(bounds) -> bool:
    south, north, west, east = bounds
    latitude_span = float(north - south)
    longitude_span = float(east - west)
    if latitude_span <= 0 or longitude_span <= 0:
        return True
    mid_latitude = float((north + south) / 2) * math.pi / 180
    area_km2 = latitude_span * 111.32 * longitude_span * 111.32 * math.cos(mid_latitude)
    return latitude_span > 7.5 or longitude_span > 8.0 or area_km2 > 200_000


def _overpass(category: str, bounds, limit: int):
    tags = CATEGORY_DEFINITIONS[category][1]
    south, north, west, east = bounds
    bbox = f"{south},{west},{north},{east}"
    filters = "".join(f'["{key}"="{value}"]' for key, value in tags)
    statements = "".join(f"{kind}{filters}({bbox});" for kind in ("node", "way", "relation"))
    query = f"[out:json][timeout:10];({statements});out center qt {limit};"
    payload = urllib.parse.urlencode({"data": query}).encode("utf-8")

    for attempt in range(2):
        request = urllib.request.Request(
            "https://overpass-api.de/api/interpreter",
            data=payload,
            headers={
                "User-Agent": USER_AGENT,
                "Content-Type": "application/x-www-form-urlencoded",
            },
            method="POST",
        )
        try:
            with urllib.request.urlopen(request, timeout=15) as response:
                payload = json.loads(response.read(512 * 1024).decode("utf-8"))
                return tuple(payload.get("elements", ())), None
        except urllib.error.HTTPError as error:
            if error.code not in {429, 502, 503, 504}:
                raise
            if attempt == 0:
                time.sleep(1.05)
                continue
            return (), f"provider returned HTTP {error.code} after bounded retry"
        except (urllib.error.URLError, TimeoutError) as error:
            if attempt == 0:
                time.sleep(1.05)
                continue
            return (), f"provider did not complete after bounded retry ({type(error).__name__})"
    return (), "provider did not complete bounded discovery"


def _to_resolved_place(record: dict, query: PlaceResearchQuery, bounds) -> ResolvedPlace | None:
    osm_type = record.get("type")
    osm_id = record.get("id")
    tags = record.get("tags") or {}
    centre = record if osm_type == "node" else record.get("center") or {}
    name = tags.get("name") or tags.get("name:en")
    if osm_type not in {"node", "way", "relation"} or type(osm_id) is not int or osm_id <= 0 or not name:
        return None
    if "lat" not in centre or "lon" not in centre:
        return None
    coordinates = _bounded_coordinates(centre, bounds)
    if coordinates is None:
        return None
    return ResolvedPlace(
        place_id=f"osm:{osm_type}:{osm_id}",
        query=f"{query.poi_category} in {query.anchor_geography}",
        purpose=query.purpose,
        name=str(name),
        address=_display_location(tags, query.anchor_geography),
        latitude=coordinates[0],
        longitude=coordinates[1],
        source_locator=f"https://www.openstreetmap.org/{osm_type}/{osm_id}",
        attribution="© OpenStreetMap contributors · ODbL",
        geometry_basis=(("Mapped POI point" if osm_type == "node" else "Mapped POI feature centre")
                        + "; administrative-area membership not verified"),
    )


def _bounded_coordinates(centre: dict, bounds):
    try:
        latitude, longitude = (Decimal(str(centre[key])) for key in ("lat", "lon"))
    except (InvalidOperation, KeyError):
        return None
    south, north, west, east = bounds
    if (latitude.is_finite() and longitude.is_finite()
            and -90 <= latitude <= 90 and -180 <= longitude <= 180
            and south <= latitude <= north and west <= longitude <= east):
        return latitude, longitude
    return None


def _display_location(tags: dict, anchor_geography: str) -> str:
    parts = [
        tags.get("addr:housenumber"),
        tags.get("addr:street"),
        tags.get("addr:suburb"),
        tags.get("addr:city"),
        tags.get("addr:province"),
    ]
    values = [str(item).strip() for item in parts if str(item or "").strip()]
    return ", ".join(values) if values else f"Search bounds for {anchor_geography}; administrative-area membership not verified"

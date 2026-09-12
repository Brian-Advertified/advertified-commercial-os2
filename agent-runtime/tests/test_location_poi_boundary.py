"""Deterministic checks for bounded POI evidence, without network calls."""
from decimal import Decimal

import pytest

from business_scenarios.location_business_support import _to_resolved_place
from location_intelligence_contracts import PlaceResearchQuery

BOUNDS = tuple(Decimal(value) for value in ("-27", "-25", "27", "29"))
QUERY = PlaceResearchQuery(
    poi_category="SCHOOL", anchor_geography="Synthetic district",
    purpose="Verify contextual school existence.", priority="REQUIRED",
    classification="HYPOTHESIS", reference_observation_ids=(),
)


@pytest.mark.parametrize("latitude,longitude", [
    ("-28", "28"), ("-26", "30"), ("NaN", "28"),
    ("-26", "Infinity"), ("invalid", "28"),
])
def test_unbounded_or_invalid_poi_coordinates_do_not_become_facts(latitude, longitude):
    record = {"type": "node", "id": 1, "lat": latitude, "lon": longitude,
              "tags": {"name": "Synthetic school"}}
    assert _to_resolved_place(record, QUERY, BOUNDS) is None


@pytest.mark.parametrize("kind", ["node", "way", "relation"])
def test_search_rectangle_does_not_establish_administrative_membership(kind):
    record = {"type": kind, "id": 1, "tags": {"name": "Synthetic school"}}
    coordinates = {"lat": "-26", "lon": "28"}
    record.update(coordinates if kind == "node" else {"center": coordinates})
    result = _to_resolved_place(record, QUERY, BOUNDS)
    assert result.latitude == Decimal("-26")
    assert result.address == "Search bounds for Synthetic district; administrative-area membership not verified"
    assert "administrative-area membership not verified" in result.geometry_basis
    record["tags"]["addr:city"] = "Supplied address"
    assert _to_resolved_place(record, QUERY, BOUNDS).address == "Supplied address"

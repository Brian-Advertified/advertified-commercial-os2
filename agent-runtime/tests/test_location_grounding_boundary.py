"""Location evidence cannot be erased or broadened while normalizing model output."""

import json
from decimal import Decimal
from uuid import UUID

import pytest

from location_intelligence_contracts import (
    LocationOpportunityArea, LocationResearchPlanRequest, LocationSynthesisRequest,
    PlaceResearchQuery,
)
from location_intelligence_service import propose_location_research, synthesize_location
from location_intelligence_validation import canonicalize_location_synthesis, validate_location_synthesis_grounding
from location_research_validation import canonicalize_location_research, validate_location_research_grounding
from reference_evidence import ReferenceObservation
from test_planning_agents import payload

AUDIENCE_ID = "bb993ea5-13cc-4621-9c9b-9ab362c6c62e"
FOREIGN_ID = UUID("d28b93b3-0980-4123-b891-e83fa775473d")
REFERENCE_ID = UUID("5f6ab53e-f02b-4e87-910b-21a8babd356b")


def research_request():
    body = payload("location_intelligence")
    invocation = body["invocation"]
    invocation["resource_refs"].append({"resource_type": "IntelligenceArtifact",
                                         "resource_id": AUDIENCE_ID, "version": 1})
    return LocationResearchPlanRequest.model_validate_json(json.dumps({
        "operation": "LOCATION_RESEARCH_PLAN", "invocation": invocation,
        "location": {
            "brief_version_id": body["planning"]["brief_version_id"],
            "audience_artifact_id": AUDIENCE_ID, "audience_artifact_version": 1,
            "client_name": "Unseen logistics business", "business_problem": "Insufficient enquiries",
            "objective": "Evaluate commercial locations", "geographies": ["Unseen district"],
            "available_poi_categories": [{"code": "COMMERCIAL_PLACE", "label": "Commercial location"}],
        },
    }))


def query():
    return PlaceResearchQuery(poi_category="COMMERCIAL_PLACE", anchor_geography="Unseen district",
                              purpose="Research contextual locations", priority="PREFERRED",
                              classification="HYPOTHESIS")


def reference_observation():
    return ReferenceObservation(
        observation_id=REFERENCE_ID,
        source_title="Governed reference fixture",
        measurement_period="2026",
        geography_level="PROVINCE",
        geography_code="GP",
        geography_name="Gauteng",
        dimensions={"group": "TOTAL_POPULATION"},
        metric_code="SHARE_PERCENT",
        metric_value=10.0,
        metric_unit="PERCENT",
        stability_code="STABLE",
        sensitivity_code="STANDARD",
        activation_policy="AGGREGATE_PLANNING_ONLY",
    )


def synthesis_request():
    request = research_request().model_dump(mode="json")
    request["operation"] = "LOCATION_SYNTHESIS"
    request["location"].update({
        "research_plan": {"queries": [query().model_dump(mode="json")],
                          "rationale": "Approved contextual research", "evidence_gaps": ["Audience presence unknown"]},
        "resolved_places": [{
            "place_id": "fixture:place-1", "query": "COMMERCIAL_PLACE in Unseen district",
            "purpose": "Research contextual locations", "name": "Synthetic commercial building",
            "address": "Unseen district", "latitude": -26, "longitude": 28,
            "source_locator": "fixture:approved-place-source", "attribution": "Synthetic test provider",
            "geometry_basis": "Provider point",
        }],
    })
    return LocationSynthesisRequest.model_validate_json(json.dumps(request))


def synthesis_output(request):
    output = synthesize_location(request)
    area = LocationOpportunityArea(name="Candidate location", geography="Unseen district",
                                   rationale="Investigate contextual place existence", classification="INFERENCE",
                                   place_ids=("fixture:place-1",), confidence=Decimal("0.9"))
    return output.model_copy(update={"artifact": output.artifact.model_copy(update={"opportunities": (area,)})})


@pytest.mark.parametrize("anchor,category", [
    ("Unseen district", "COMMERCIAL_PLACE"),
    ("Outside approved scope", "COMMERCIAL_PLACE"),
    ("Unseen district", "UNSUPPLIED_CATEGORY"),
])
def test_forged_provenance_is_rejected_even_for_unsupported_query(anchor, category):
    request = research_request()
    output = propose_location_research(request)
    invalid = query().model_copy(update={
        "anchor_geography": anchor, "poi_category": category,
        "reference_observation_ids": (FOREIGN_ID,),
    })
    output = output.model_copy(update={
        "artifact": output.artifact.model_copy(update={"queries": (invalid,)}),
    })
    with pytest.raises(ValueError, match="not supplied"):
        canonicalize_location_research(request, output)


def test_mixed_geography_evidence_keeps_only_matching_reference_and_inference():
    request = research_request()
    matching = reference_observation().model_copy(update={"geography_name": " Unseen district "})
    mismatched = reference_observation().model_copy(update={"observation_id": FOREIGN_ID})
    request = request.model_copy(update={
        "location": request.location.model_copy(update={"reference_evidence": (matching, mismatched)}),
    })
    output = propose_location_research(request)
    candidate = query().model_copy(update={
        "anchor_geography": " UNSEEN DISTRICT ", "classification": "INFERENCE",
        "reference_observation_ids": (REFERENCE_ID, FOREIGN_ID),
        "purpose": "Audience presence is verified by this place.",
    })
    output = output.model_copy(update={
        "artifact": output.artifact.model_copy(update={"queries": (candidate,)}),
    })
    result = canonicalize_location_research(request, output)
    validate_location_research_grounding(request, result)
    retained = result.artifact.queries[0]
    assert retained.reference_observation_ids == (REFERENCE_ID,)
    assert retained.classification == "INFERENCE"
    assert "does not establish target-audience presence or visitation" in retained.purpose
    gaps = result.artifact.evidence_gaps
    assert any("removed 1 supplied reference observation citation" in gap for gap in gaps)
    assert not any("affected requests remain hypotheses" in gap for gap in gaps)
    assert request.location.reference_evidence == (matching, mismatched)


def test_supplied_reference_for_different_geography_is_removed_and_query_downgraded():
    request = research_request()
    reference = reference_observation()
    request = request.model_copy(update={
        "location": request.location.model_copy(update={"reference_evidence": (reference,)}),
    })
    output = propose_location_research(request)
    mismatched = query().model_copy(update={
        "classification": "INFERENCE",
        "reference_observation_ids": (REFERENCE_ID,),
    })
    output = output.model_copy(update={
        "artifact": output.artifact.model_copy(update={"queries": (mismatched,)}),
    })

    result = canonicalize_location_research(request, output)
    validate_location_research_grounding(request, result)
    retained = result.artifact.queries[0]
    assert retained.classification == "HYPOTHESIS"
    assert retained.reference_observation_ids == ()
    assert any("reference observation citation" in gap for gap in result.artifact.evidence_gaps)


def test_duplicate_research_queries_merge_without_claiming_audience_presence():
    request = research_request()
    output = propose_location_research(request)
    duplicate = query().model_copy(update={"priority": "REQUIRED", "purpose": "Invented guaranteed visits"})
    output = output.model_copy(update={"artifact": output.artifact.model_copy(update={"queries": (query(), duplicate)})})
    result = canonicalize_location_research(request, output)
    validate_location_research_grounding(request, result)
    assert len(result.artifact.queries) == 1
    assert result.artifact.queries[0].priority == "REQUIRED"
    assert "Invented guaranteed" not in result.artifact.queries[0].purpose


@pytest.mark.parametrize("field,value", [
    ("place_ids", ("fixture:foreign-place",)),
    ("reference_observation_ids", (FOREIGN_ID,)),
    ("geography", "Outside the approved geographic frame"),
])
def test_synthesis_rejects_unknown_sources_and_geography_before_normalization(field, value):
    request = synthesis_request()
    output = synthesis_output(request)
    area = output.artifact.opportunities[0].model_copy(update={field: value})
    output = output.model_copy(update={"artifact": output.artifact.model_copy(update={"opportunities": (area,)})})
    with pytest.raises(ValueError):
        canonicalize_location_synthesis(request, output)


def test_place_existence_is_not_audience_evidence_and_original_facts_are_preserved():
    request = synthesis_request()
    result = canonicalize_location_synthesis(request, synthesis_output(request))
    validate_location_synthesis_grounding(request, result)
    area = result.artifact.opportunities[0]
    assert area.classification == "HYPOTHESIS"
    assert area.confidence is None
    assert result.artifact.resolved_places == request.location.resolved_places
    assert result.artifact.research_queries == request.location.research_plan.queries
    assert "Audience presence unknown" in result.artifact.evidence_gaps
    tampered = result.model_copy(update={"artifact": result.artifact.model_copy(update={"resolved_places": ()})})
    with pytest.raises(ValueError, match="place-discovery facts"):
        validate_location_synthesis_grounding(request, tampered)

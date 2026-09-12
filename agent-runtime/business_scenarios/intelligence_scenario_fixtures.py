"""Synthetic audience and geographic source cases; no lookup or paid provider."""
import json
from uuid import UUID, uuid5

from location_intelligence_contracts import LocationSynthesisRequest
from planning_contracts import AudienceAgentRequest
from tests.test_planning_agents import payload

NAMESPACE = UUID("20475d62-3f0b-4468-a96f-ec3a24bd028c")


def audience_request(scenario):
    body = payload("audience_intelligence")
    body["invocation"]["run_id"] = str(uuid5(NAMESPACE, scenario.scenario_id))
    names = {
        "BROAD": ["Adults"], "NICHE": ["Rayetsa synthetic livestock farmers"],
        "COMMUNITY": ["Supplied community members"], "AGE_LIFESTAGE": ["Parents of young children"],
        "B2B": ["Industrial procurement teams"],
    }
    body["planning"]["client_name"] = "Synthetic intelligence " + scenario.scenario_id
    body["planning"]["audiences"] = names.get(scenario.source_inputs.get("audience_scope"),
                                              ["Supplied service users"])
    if scenario.source_inputs.get("audience_overlap"):
        body["planning"]["audiences"] = ["Parents", "Caregivers", "Parents"]
    body["planning"]["geographies"] = geography(scenario)
    if scenario.source_inputs.get("evidence_conflict"):
        body["planning"]["reference_evidence"] = [reference(scenario, "2026", 10),
                                                   reference(scenario, "2026", 30)]
    if scenario.source_inputs.get("evidence_freshness") == "CONFLICT":
        body["planning"]["reference_evidence"] = [reference(scenario, "2011", 10),
                                                   reference(scenario, "2026", 30)]
    return AudienceAgentRequest.model_validate_json(json.dumps(body))


def geography(scenario):
    inputs = scenario.source_inputs
    if inputs.get("geography_scope") == "MULTI_PROVINCE":
        return ["Gauteng", "Limpopo", "KwaZulu-Natal"]
    if inputs.get("geography_context") == "TOWNSHIP":
        return ["Soweto"]
    if inputs.get("geography_context") == "PREMIUM_URBAN":
        return ["Sandton"]
    return ["Gauteng"]


def reference(scenario, period, value):
    return dict(
        observation_id=str(uuid5(NAMESPACE, scenario.scenario_id + period + str(value))),
        source_title="Synthetic aggregate reference", measurement_period=period,
        geography_level="PROVINCE", geography_code="GP", geography_name="Gauteng",
        dimensions={"group": "TOTAL_POPULATION"}, metric_code="SHARE_PERCENT",
        metric_value=value, metric_unit="PERCENT", stability_code="STABLE",
        sensitivity_code="STANDARD", activation_policy="AGGREGATE_PLANNING_ONLY",
    )


def location_request(scenario):
    body = payload("location_intelligence")
    invocation = body["invocation"]
    artifact_id = str(uuid5(NAMESPACE, scenario.scenario_id + ":audience"))
    invocation["run_id"] = str(uuid5(NAMESPACE, scenario.scenario_id))
    invocation["resource_refs"].append(dict(resource_type="IntelligenceArtifact",
                                           resource_id=artifact_id, version=1))
    corridor = scenario.source_inputs.get("location_basis") == "CORRIDOR"
    no_evidence = scenario.source_inputs.get("geo_evidence") == "NONE"
    query = dict(poi_category="COMMERCIAL_PLACE", anchor_geography="Gauteng",
                 purpose="Investigate contextual place existence", priority="PREFERRED",
                 classification="HYPOTHESIS")
    places = [] if no_evidence else [dict(
        place_id="synthetic:" + scenario.scenario_id, query="COMMERCIAL_PLACE in Gauteng",
        purpose=query["purpose"], name="Synthetic interchange" if corridor else "Synthetic community venue",
        address="Synthetic fixture coordinates; administrative membership unverified",
        latitude=-26.17, longitude=28.04, source_locator="fixture:" + scenario.scenario_id,
        attribution="Synthetic deterministic fixture", geometry_basis="Synthetic point; membership unverified",
    )]
    return LocationSynthesisRequest.model_validate_json(json.dumps(dict(
        operation="LOCATION_SYNTHESIS", invocation=invocation, location=dict(
            brief_version_id=body["planning"]["brief_version_id"],
            audience_artifact_id=artifact_id, audience_artifact_version=1,
            client_name="Synthetic location " + scenario.scenario_id,
            business_problem="Source-bound place research", objective="Assess contextual locations",
            geographies=["Gauteng"], constraints=["Movement volumes not supplied"] if corridor else [],
            available_poi_categories=[dict(code="COMMERCIAL_PLACE", label="Commercial place")],
            research_plan=dict(queries=[] if no_evidence else [query],
                               rationale="Synthetic approved research plan",
                               evidence_gaps=["Audience presence and movement are not supplied."]),
            resolved_places=places,
        ),
    )))

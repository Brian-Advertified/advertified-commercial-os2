"""Mixed-complexity live Location Intelligence battery.

This chains live Audience Intelligence into live Location planning, real bounded POI discovery
and live Location synthesis. A scenario is blocked rather than fabricating a target audience if
Audience Intelligence cannot produce an approved-target candidate.
"""

from __future__ import annotations

import json
import uuid

import pytest

from agent_registry import AgentCode
from bedrock_provider import BEDROCK_MODE
from business_scenarios.bedrock_support import configure_live_bedrock, invocation
from business_scenarios.location_business_support import (
    available_poi_categories,
    resolve_bounded_pois,
)
from business_scenarios.test_audience_complexity_live import SCENARIOS, Scenario, _run as run_audience
from contracts import ResourceReference
from location_intelligence_contracts import (
    RESEARCH_OPERATION,
    SYNTHESIS_OPERATION,
    LocationAudienceSegment,
    LocationCommercialContext,
    LocationResearchPlanArtifact,
    LocationResearchPlanRequest,
    LocationSynthesisContext,
    LocationSynthesisRequest,
)
from runtime_execution import execute_agent

TENANT_ID = uuid.UUID("11111111-1111-1111-1111-111111111111")
ACTOR_ID = uuid.UUID("22222222-2222-2222-2222-222222222222")
NAMESPACE = uuid.UUID("b353383a-2c6d-4dcb-921d-a83df86c5dc0")
SELECTED = {
    "rayetsa_sparse_retail_no_audience",
    "vaccination_awareness_detailed_public_service",
    "indlu_multilocation_property",
}


def _id(scenario: Scenario, suffix: str) -> uuid.UUID:
    return uuid.uuid5(NAMESPACE, f"{scenario.name}:{suffix}")


def _location_context(
    scenario: Scenario,
    audience_result: dict[str, object],
) -> LocationCommercialContext | None:
    if audience_result.get("status") != "ACCEPTED":
        return None
    segments = audience_result.get("segments") or []
    targets = []
    for index, item in enumerate(segments):
        if not item.get("is_target"):
            continue
        targets.append(LocationAudienceSegment(
            segment_id=uuid.uuid5(_id(scenario, "audience-artifact"), f"{index}:{item['name']}"),
            name=item["name"],
            description=item["description"],
            geographies=tuple(item.get("geographies") or scenario.geographies),
            classification=item["classification"],
            evidence_item_ids=tuple(uuid.UUID(value) for value in item.get("evidence_item_ids", ())),
            reference_observation_ids=tuple(
                uuid.UUID(value) for value in item.get("reference_observation_ids", ())
            ),
            is_target=True,
        ))
    if not targets:
        return None
    return LocationCommercialContext(
        brief_version_id=_id(scenario, "brief"),
        audience_artifact_id=_id(scenario, "audience-artifact"),
        audience_artifact_version=1,
        client_name=scenario.client,
        business_problem=scenario.business_problem,
        objective=scenario.objective,
        geographies=scenario.geographies,
        constraints=scenario.constraints,
        target_segments=tuple(targets),
        reference_evidence=(),
        available_poi_categories=available_poi_categories(),
    )


def _invocation(scenario: Scenario):
    return invocation(
        agent_code=AgentCode.LOCATION_INTELLIGENCE,
        tenant_id=TENANT_ID,
        actor_id=ACTOR_ID,
        brief_version_id=_id(scenario, "brief"),
        run_id=_id(scenario, "location-run"),
        additional_resource_refs=(ResourceReference(
            resource_type="IntelligenceArtifact",
            resource_id=_id(scenario, "audience-artifact"),
            version=1,
        ),),
    )


def _research_location(scenario: Scenario, context: LocationCommercialContext):
    research_output = execute_agent(
        AgentCode.LOCATION_INTELLIGENCE,
        LocationResearchPlanRequest(
            operation=RESEARCH_OPERATION,
            invocation=_invocation(scenario),
            location=context,
        ).model_dump_json().encode("utf-8"),
        BEDROCK_MODE,
    )
    plan = LocationResearchPlanArtifact.model_validate_json(json.dumps(research_output["artifact"]))
    effective_plan, resolved = resolve_bounded_pois(
        plan, context, maximum_queries=6, maximum_places=12, places_per_query=4,
    )
    return research_output, plan, effective_plan, resolved


def _synthesize_location(scenario: Scenario, context, effective_plan, resolved):
    return execute_agent(
        AgentCode.LOCATION_INTELLIGENCE,
        LocationSynthesisRequest(
            operation=SYNTHESIS_OPERATION,
            invocation=_invocation(scenario),
            location=LocationSynthesisContext(
                **context.model_dump(), research_plan=effective_plan, resolved_places=resolved,
            ),
        ).model_dump_json().encode("utf-8"),
        BEDROCK_MODE,
    )


def _location_flags(scenario: Scenario, effective_plan, resolved, artifact) -> dict[str, object]:
    supplied_geographies = {geography.casefold() for geography in scenario.geographies}
    allowed_categories = {option.code for option in available_poi_categories()}
    resolved_ids = {place.place_id for place in resolved}
    invalid_queries = [
        item for item in effective_plan.queries
        if item.poi_category not in allowed_categories or item.anchor_geography.casefold() not in supplied_geographies
    ]
    unsupported_opportunities = [
        item for item in artifact["opportunities"]
        if item["classification"] != "HYPOTHESIS"
        or item["confidence"] is not None
        or any(place_id not in resolved_ids for place_id in item["place_ids"])
        or item["reference_observation_ids"]
    ]
    missing_research_gaps = [gap for gap in effective_plan.evidence_gaps if gap not in artifact["evidence_gaps"]]
    text = json.dumps(artifact).casefold()
    unsafe_claims = [
        marker for marker in (
            "audience presence is verified", "audience visitation is verified", "proves the audience",
            "high footfall", "mortgage eligible", "vaccinated people", "unvaccinated people",
        ) if marker in text
    ]
    return {
        "invalid_effective_research_queries": invalid_queries,
        "unsupported_opportunities": unsupported_opportunities,
        "missing_research_evidence_gaps": missing_research_gaps,
        "unsafe_claims": unsafe_claims,
        "mentions_inventory": "inventory" in text,
    }


def _run_location(scenario: Scenario) -> dict[str, object]:
    audience_result = run_audience(scenario)
    context = _location_context(scenario, audience_result)
    if context is None:
        return {
            "scenario": scenario.name,
            "status": "BLOCKED_NO_APPROVED_TARGET_AUDIENCE",
            "audience_status": audience_result.get("status"),
            "audience_segments": audience_result.get("segments", []),
            "audience_usage": audience_result.get("usage"),
        }
    research_output, plan, effective_plan, resolved = _research_location(scenario, context)
    synthesis_output = _synthesize_location(scenario, context, effective_plan, resolved)
    artifact = synthesis_output["artifact"]
    assert artifact is not None
    return {
        "scenario": scenario.name,
        "status": "COMPLETED",
        "audience_targets": [item.name for item in context.target_segments],
        "original_research_queries": plan.model_dump(mode="json")["queries"],
        "effective_research_plan": effective_plan.model_dump(mode="json"),
        "resolved_place_count": len(resolved),
        "resolved_places": [item.model_dump(mode="json") for item in resolved],
        "location_artifact": artifact,
        "audience_usage": audience_result.get("usage"),
        "research_usage": research_output["usage"],
        "synthesis_usage": synthesis_output["usage"],
        "business_flags": _location_flags(scenario, effective_plan, resolved, artifact),
    }


LOCATION_SCENARIOS = tuple(item for item in SCENARIOS if item.name in SELECTED)


@pytest.mark.parametrize(
    "scenario",
    LOCATION_SCENARIOS,
    ids=[item.name for item in LOCATION_SCENARIOS],
)
def test_location_complexity_live(scenario: Scenario) -> None:
    configure_live_bedrock()
    result = _run_location(scenario)
    print("\nLOCATION INTELLIGENCE SCENARIO\n" + json.dumps(result, indent=2, default=str))

    assert result["status"] == "COMPLETED", result
    flags = result["business_flags"]
    assert not flags["invalid_effective_research_queries"], result
    assert not flags["unsupported_opportunities"], result
    assert not flags["missing_research_evidence_gaps"], result
    assert not flags["unsafe_claims"], result
    assert not flags["mentions_inventory"], result

    if scenario.name in {"indlu_multilocation_property", "rayetsa_sparse_retail_no_audience"}:
        assert result["resolved_place_count"] > 0, result
    if scenario.name == "vaccination_awareness_detailed_public_service":
        vaccination_text = json.dumps(result["location_artifact"]).casefold()
        assert "health status" not in vaccination_text or "does not" in vaccination_text

"""Live Mukuru Location Intelligence: Bedrock plan -> governed POIs -> Bedrock synthesis."""

from __future__ import annotations

import json
import uuid

from agent_registry import AgentCode
from bedrock_provider import BEDROCK_MODE
from business_scenarios.bedrock_support import configure_live_bedrock, invocation
from business_scenarios.location_business_support import (
    available_poi_categories,
    resolve_bounded_pois,
)
from business_scenarios.test_mukuru_audience_intelligence_live import selected_reference_observations
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
BRIEF_ID = uuid.UUID("33333333-3333-3333-3333-333333333333")
AUDIENCE_ARTIFACT_ID = uuid.UUID("66666666-6666-6666-6666-666666666666")
RUN_ID = uuid.UUID("88888888-8888-8888-8888-888888888888")


def _context() -> LocationCommercialContext:
    references = selected_reference_observations()
    targets = tuple(
        LocationAudienceSegment(
            segment_id=uuid.uuid5(AUDIENCE_ARTIFACT_ID, country),
            name=f"Customers in South Africa who send money to {country}",
            description=f"Brief-supplied remittance audience for {country}; deeper traits remain unverified.",
            geographies=("South Africa",),
            classification="HYPOTHESIS",
            evidence_item_ids=(),
            reference_observation_ids=(),
            is_target=True,
        )
        for country in ("Bangladesh", "India", "Pakistan")
    )
    return LocationCommercialContext(
        brief_version_id=BRIEF_ID,
        audience_artifact_id=AUDIENCE_ARTIFACT_ID,
        audience_artifact_version=1,
        client_name="Mukuru",
        business_problem=(
            "Find suitable OOH opportunities across South Africa for customers who send money "
            "to Bangladesh, India and Pakistan, focusing on relevant communities and proximity "
            "to mosques and temples."
        ),
        objective="Identify defensible OOH opportunity areas for the specified remittance audiences.",
        geographies=("South Africa",),
        constraints=(
            "Campaign runs October to December.",
            "Billboards and wall murals must be considered downstream.",
            "Proximity to mosques and temples is explicitly requested.",
            "No confirmed budget.",
        ),
        target_segments=targets,
        reference_evidence=references,
        available_poi_categories=available_poi_categories(),
    )


def _agent_invocation():
    return invocation(
        agent_code=AgentCode.LOCATION_INTELLIGENCE,
        tenant_id=TENANT_ID,
        actor_id=ACTOR_ID,
        brief_version_id=BRIEF_ID,
        run_id=RUN_ID,
        additional_resource_refs=(ResourceReference(
            resource_type="IntelligenceArtifact",
            resource_id=AUDIENCE_ARTIFACT_ID,
            version=1,
        ),),
    )


def _research(context: LocationCommercialContext):
    request = LocationResearchPlanRequest(
        operation=RESEARCH_OPERATION, invocation=_agent_invocation(), location=context,
    )
    output = execute_agent(
        AgentCode.LOCATION_INTELLIGENCE,
        request.model_dump_json().encode("utf-8"),
        BEDROCK_MODE,
    )
    plan = LocationResearchPlanArtifact.model_validate_json(json.dumps(output["artifact"]))
    return output, plan


def _assert_research(plan, effective_plan, resolved) -> None:
    assert any(item.poi_category == "MOSQUE" for item in plan.queries)
    assert any(item.poi_category == "HINDU_TEMPLE" for item in plan.queries)
    assert effective_plan.queries, "Mukuru retained no safely executable bounded research query."
    assert resolved, "Mukuru did not resolve any real governed POI."
    assert all(
        "does not establish target-audience presence or visitation" in item.purpose
        for item in effective_plan.queries
    )


def _synthesize(context, effective_plan, resolved):
    request = LocationSynthesisRequest(
        operation=SYNTHESIS_OPERATION,
        invocation=_agent_invocation(),
        location=LocationSynthesisContext(
            **context.model_dump(), research_plan=effective_plan, resolved_places=resolved,
        ),
    )
    return execute_agent(
        AgentCode.LOCATION_INTELLIGENCE,
        request.model_dump_json().encode("utf-8"),
        BEDROCK_MODE,
    )


def _assert_synthesis(artifact, effective_plan, resolved) -> None:
    assert artifact["research_queries"] == [item.model_dump(mode="json") for item in effective_plan.queries]
    assert len(artifact["resolved_places"]) == len(resolved)
    assert all(item["classification"] in {"INFERENCE", "HYPOTHESIS"} for item in artifact["opportunities"])
    assert all(
        item["confidence"] is None for item in artifact["opportunities"]
        if not item["reference_observation_ids"]
    )
    assert all(gap in artifact["evidence_gaps"] for gap in effective_plan.evidence_gaps)
    text = json.dumps(artifact).casefold()
    assert "inventory" not in text
    for forbidden in (
        "audience presence is verified", "audience visitation is verified",
        "customers are concentrated", "high footfall", "proves the audience",
    ):
        assert forbidden not in text


def test_mukuru_location_end_to_end_live() -> None:
    configure_live_bedrock()
    context = _context()
    _, plan = _research(context)
    effective_plan, resolved = resolve_bounded_pois(plan, context)
    print("\nMUKURU EFFECTIVE BOUNDED RESEARCH PLAN\n" + json.dumps(
        effective_plan.model_dump(mode="json"), indent=2, default=str))
    print("\nMUKURU RESOLVED POIS\n" + json.dumps(
        [item.model_dump(mode="json") for item in resolved], indent=2, default=str))
    _assert_research(plan, effective_plan, resolved)
    output = _synthesize(context, effective_plan, resolved)
    print("\nMUKURU LOCATION SYNTHESIS\n" + json.dumps(output, indent=2, default=str))
    artifact = output["artifact"]
    assert artifact is not None
    _assert_synthesis(artifact, effective_plan, resolved)

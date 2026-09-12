"""Live Mukuru Location Intelligence research-plan business scenario."""

from __future__ import annotations

import json
import uuid

from agent_registry import AgentCode
from bedrock_provider import BEDROCK_MODE
from business_scenarios.bedrock_support import configure_live_bedrock, invocation
from business_scenarios.location_business_support import available_poi_categories
from business_scenarios.test_mukuru_audience_intelligence_live import selected_reference_observations
from contracts import ResourceReference
from location_intelligence_contracts import (
    RESEARCH_OPERATION,
    LocationAudienceSegment,
    LocationCommercialContext,
    LocationResearchPlanRequest,
)
from runtime_execution import execute_agent

TENANT_ID = uuid.UUID("11111111-1111-1111-1111-111111111111")
ACTOR_ID = uuid.UUID("22222222-2222-2222-2222-222222222222")
BRIEF_ID = uuid.UUID("33333333-3333-3333-3333-333333333333")
AUDIENCE_ARTIFACT_ID = uuid.UUID("66666666-6666-6666-6666-666666666666")
RUN_ID = uuid.UUID("77777777-7777-7777-7777-777777777777")


def _targets() -> tuple[LocationAudienceSegment, ...]:
    return tuple(
        LocationAudienceSegment(
            segment_id=uuid.uuid5(AUDIENCE_ARTIFACT_ID, country),
            name=f"Customers in South Africa who send money to {country}",
            description=f"Brief-supplied remittance audience for {country}; deeper audience traits remain unverified.",
            geographies=("South Africa",), classification="HYPOTHESIS",
            evidence_item_ids=(), reference_observation_ids=(), is_target=True,
        ) for country in ("Bangladesh", "India", "Pakistan")
    )


def _request() -> LocationResearchPlanRequest:
    return LocationResearchPlanRequest(
        operation=RESEARCH_OPERATION,
        invocation=invocation(
            agent_code=AgentCode.LOCATION_INTELLIGENCE, tenant_id=TENANT_ID, actor_id=ACTOR_ID,
            brief_version_id=BRIEF_ID, run_id=RUN_ID,
            additional_resource_refs=(ResourceReference(
                resource_type="IntelligenceArtifact", resource_id=AUDIENCE_ARTIFACT_ID, version=1,
            ),),
        ),
        location=LocationCommercialContext(
            brief_version_id=BRIEF_ID, audience_artifact_id=AUDIENCE_ARTIFACT_ID,
            audience_artifact_version=1, client_name="Mukuru",
            business_problem=(
                "Find suitable OOH opportunities across South Africa for customers who send money "
                "to Bangladesh, India and Pakistan, focusing on relevant communities and proximity "
                "to mosques and temples."
            ),
            objective="Identify defensible OOH opportunity areas for the specified remittance audiences.",
            geographies=("South Africa",),
            constraints=(
                "Campaign runs October to December.", "Billboards and wall murals must be considered downstream.",
                "Proximity to mosques and temples is explicitly requested.", "No confirmed budget.",
            ),
            target_segments=_targets(),
            reference_evidence=selected_reference_observations(),
            available_poi_categories=available_poi_categories(),
        ),
    )


def test_mukuru_location_research_live() -> None:
    configure_live_bedrock()
    request = _request()
    output = execute_agent(
        AgentCode.LOCATION_INTELLIGENCE,
        request.model_dump_json().encode("utf-8"),
        BEDROCK_MODE,
    )
    print("\nMUKURU LOCATION RESEARCH PLAN\n" + json.dumps(output, indent=2, default=str))
    allowed_categories = {item.code for item in available_poi_categories()}
    for item in output["artifact"]["queries"]:
        assert "search_term" not in item
        assert item["poi_category"] in allowed_categories
        assert item["anchor_geography"] == "South Africa" or item["reference_observation_ids"]
        assert "does not establish target-audience presence or visitation" in item["purpose"]

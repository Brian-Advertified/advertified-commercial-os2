"""Live zero-inventory Media Strategy Intelligence acceptance scenarios."""

from __future__ import annotations

import json
import uuid

from agent_registry import AgentCode
from bedrock_provider import BEDROCK_MODE
from business_scenarios.bedrock_support import configure_live_bedrock, invocation
from contracts import ResourceReference
from media_strategy_contracts import (
    OPERATION,
    MediaStrategyAudienceInput,
    MediaStrategyContext,
    MediaStrategyRequest,
)
from runtime_execution import execute_agent

TENANT_ID = uuid.UUID("11111111-1111-1111-1111-111111111111")
ACTOR_ID = uuid.UUID("22222222-2222-2222-2222-222222222222")
NAMESPACE = uuid.UUID("0ef85e43-7c2a-41ab-8a48-2ce575644742")


def _id(name: str, suffix: str) -> uuid.UUID:
    return uuid.uuid5(NAMESPACE, f"{name}:{suffix}")


def _run(name: str, context: MediaStrategyContext) -> dict[str, object]:
    request = MediaStrategyRequest(
        operation=OPERATION,
        invocation=invocation(
            agent_code=AgentCode.MEDIA_STRATEGY,
            tenant_id=TENANT_ID,
            actor_id=ACTOR_ID,
            brief_version_id=context.brief_version_id,
            run_id=_id(name, "run"),
            additional_resource_refs=(ResourceReference(
                resource_type="IntelligenceArtifact",
                resource_id=context.audience_artifact_id,
                version=context.audience_artifact_version,
            ),),
        ),
        media_strategy=context,
    )
    return execute_agent(
        AgentCode.MEDIA_STRATEGY,
        request.model_dump_json().encode("utf-8"),
        BEDROCK_MODE,
    )


def _mukuru_context() -> MediaStrategyContext:
    audience_id = _id("mukuru", "audience")
    return MediaStrategyContext(
        brief_version_id=_id("mukuru", "brief"),
        audience_artifact_id=audience_id,
        audience_artifact_version=1,
        client_name="Mukuru",
        business_problem=(
            "Identify suitable OOH approaches across South Africa for customers who send money to Bangladesh, "
            "India and Pakistan, with contextual proximity to mosques and temples."
        ),
        objective="Build a defensible OOH strategy for the specified remittance audiences.",
        geographies=("South Africa",),
        media_requirements=(
            "Billboards must be considered.", "Wall murals must be considered.",
            "Other relevant OOH formats may be recommended.",
        ),
        constraints=("Campaign runs October to December.", "No confirmed budget."),
        success_measures=(), budget_minor=None, currency=None,
        available_channels=("OOH", "DOOH"),
        target_audiences=tuple(
            MediaStrategyAudienceInput(
                segment_id=uuid.uuid5(audience_id, country),
                name=f"Customers in South Africa who send money to {country}",
                description=f"Brief-supplied remittance audience for {country}; deeper traits are not established.",
                geographies=("South Africa",), classification="CLIENT_REQUIREMENT",
            ) for country in ("Bangladesh", "India", "Pakistan")
        ),
        audience_unknowns=(
            "Need state is not established.", "Buying context is not established.",
            "Positioning is not established.",
        ),
    )


def _takealot_context() -> MediaStrategyContext:
    audience_id = _id("takealot", "audience")
    return MediaStrategyContext(
        brief_version_id=_id("takealot", "brief"),
        audience_artifact_id=audience_id,
        audience_artifact_version=1,
        client_name="Takealot",
        business_problem="Plan Black Friday out-of-home activity across Johannesburg, Cape Town and Durban.",
        objective="Drive Black Friday awareness during the campaign window.",
        geographies=("Johannesburg", "Cape Town", "Durban"),
        media_requirements=("Digital out-of-home only.",),
        constraints=("Campaign runs 4 November to 28 November.",),
        success_measures=(), budget_minor=32_000_000, currency="ZAR",
        available_channels=("OOH", "DOOH"),
        target_audiences=(MediaStrategyAudienceInput(
            segment_id=uuid.uuid5(audience_id, "black-friday"),
            name="Black Friday shoppers",
            description="Audience hypothesis for the stated Black Friday objective; unstated behaviours are not established.",
            geographies=("Johannesburg", "Cape Town", "Durban"), classification="HYPOTHESIS",
        ),),
        audience_unknowns=("Verified media-consumption evidence is not supplied.",),
    )


def _assert_safe_strategy(artifact) -> None:
    text = json.dumps(artifact).casefold()
    assert "supplier inventory" not in text
    assert "inventory availability" not in text
    assert "guaranteed reach" not in text
    assert "verified footfall" not in text


def test_media_strategy_zero_inventory_live() -> None:
    configure_live_bedrock()
    results = {
        "mukuru_budgetless": _run("mukuru", _mukuru_context()),
        "takealot_strict_dooh": _run("takealot", _takealot_context()),
    }
    print("\nMEDIA STRATEGY ZERO-INVENTORY BATTERY\n" + json.dumps(results, indent=2, default=str))
    mukuru_artifact = results["mukuru_budgetless"]["artifact"]
    takealot_artifact = results["takealot_strict_dooh"]["artifact"]
    assert mukuru_artifact is not None
    assert takealot_artifact is not None
    assert mukuru_artifact["channel_recommendations"]
    assert all(item["budget_guidance_percent"] is None for item in mukuru_artifact["channel_recommendations"])
    assert all(item["channel"] in {"OOH", "DOOH"} for item in mukuru_artifact["channel_recommendations"])
    assert takealot_artifact["channel_recommendations"]
    assert all(item["channel"] == "DOOH" for item in takealot_artifact["channel_recommendations"])
    assert set(_mukuru_context().media_requirements).issubset(mukuru_artifact["strategic_principles"])
    _assert_safe_strategy(mukuru_artifact)
    _assert_safe_strategy(takealot_artifact)

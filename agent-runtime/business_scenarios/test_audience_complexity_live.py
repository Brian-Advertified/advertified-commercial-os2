"""Mixed-complexity live Audience Intelligence business battery.

The purpose is to expose inconsistent model behaviour across realistic briefs. A technically
valid artifact is not treated as a business pass: the scenario report also flags unsupported
positioning language and whether evidence-free segments stayed hypotheses.
"""

from __future__ import annotations

import json
import os
import uuid
from dataclasses import dataclass

from fastapi import HTTPException

from agent_registry import AgentCode
from bedrock_provider import BEDROCK_MODE
from business_scenarios.bedrock_support import configure_live_bedrock, invocation
from planning_contracts import AudienceAgentRequest, PlanningBriefContext
from supplied_brief_contracts import BriefConflict
from runtime_execution import execute_agent

TENANT_ID = uuid.UUID("11111111-1111-1111-1111-111111111111")
ACTOR_ID = uuid.UUID("22222222-2222-2222-2222-222222222222")
NAMESPACE = uuid.UUID("77f3bcb8-187e-46c0-bdbc-f0a83e84a1f7")


@dataclass(frozen=True)
class Scenario:
    name: str
    client: str
    business_problem: str
    objective: str
    audiences: tuple[str, ...]
    geographies: tuple[str, ...]
    constraints: tuple[str, ...]
    success_measures: tuple[str, ...]
    budget_minor: int | None
    currency: str | None
    media_requirements: tuple[str, ...] = ()
    conflicts: tuple[BriefConflict, ...] = ()


SCENARIOS = (
    Scenario(
        name="mukuru_strict_budgetless_sensitive_context",
        client="Mukuru",
        business_problem=(
            "Find suitable OOH opportunities across South Africa for customers who send money "
            "to Bangladesh, India and Pakistan, with relevant community concentration and proximity "
            "to mosques and temples."
        ),
        objective="Recommend defensible OOH approaches for the specified remittance audiences.",
        audiences=(
            "Customers in South Africa who send money to Bangladesh",
            "Customers in South Africa who send money to India",
            "Customers in South Africa who send money to Pakistan",
        ),
        geographies=("South Africa",),
        constraints=(
            "Campaign runs October to December.",
            "Billboards must be considered.",
            "Wall murals must be considered.",
            "Other relevant OOH formats may be recommended.",
            "No confirmed budget; recommend a range of investment levels.",
            "Do not infer nationality or religion of individuals from contextual location evidence.",
        ),
        success_measures=("Produce actionable, evidence-backed OOH recommendations.",),
        budget_minor=None,
        currency="ZAR",
    ),
    Scenario(
        name="rayetsa_sparse_retail_no_audience",
        client="Rayetsa Furniture",
        business_problem=(
            "Rayetsa wants to grow furniture sales during October to December across Pretoria, "
            "Midrand, Mpumalanga and Limpopo. The brief does not define the consumer audience."
        ),
        objective="Drive incremental furniture sales and store demand.",
        audiences=(),
        geographies=("Pretoria", "Midrand", "Mpumalanga", "Limpopo"),
        constraints=(
            "Campaign runs October to December 2026.",
            "Do not invent credit terms, discounts, price points or product claims.",
        ),
        success_measures=("Incremental furniture sales", "Qualified store demand"),
        budget_minor=15_000_000,
        currency="ZAR",
    ),
    Scenario(
        name="takealot_black_friday_strict_dooh",
        client="Takealot",
        business_problem=(
            "Black Friday OOH campaign in Johannesburg, Cape Town and Durban around selected malls."
        ),
        objective="Build Black Friday visibility during the specified flight.",
        audiences=(),
        geographies=("Johannesburg", "Cape Town", "Durban"),
        constraints=(
            "Campaign runs 4 November to 28 November.",
            "Digital OOH only.",
            "Budget is ZAR 320,000 with a possible ceiling of ZAR 400,000.",
            "Do not add non-OOH channels.",
        ),
        success_measures=("Black Friday visibility in the specified markets",),
        budget_minor=32_000_000,
        currency="ZAR",
    ),
    Scenario(
        name="vaccination_awareness_detailed_public_service",
        client="Department of Health",
        business_problem=(
            "Vaccination awareness campaign covering polio, measles and HPV messages for caregivers "
            "and families, with creative rotation across the campaign."
        ),
        objective="Increase awareness of the vaccination programme and relevant vaccination actions.",
        audiences=(
            "Parents and caregivers of children under 5",
            "Parents and caregivers of girls aged 9 to 14",
        ),
        geographies=("South Africa",),
        constraints=(
            "Rotate polio, measles and HPV messages.",
            "Commercial amounts are inclusive of VAT.",
            "No agency commission.",
            "Do not infer individual health status or vaccination status.",
        ),
        success_measures=("Programme awareness", "Understanding of vaccination actions"),
        budget_minor=None,
        currency="ZAR",
    ),
    Scenario(
        name="indlu_multilocation_property",
        client="Indlu Properties",
        business_problem=(
            "Support residential property demand across Soshanguve, Tembisa, Vosloorus and Mamelodi; "
            "current lead interest drops materially after show-house activity."
        ),
        objective="Increase qualified property enquiries and sustain demand after show-house periods.",
        audiences=(),
        geographies=("Soshanguve", "Tembisa", "Vosloorus", "Mamelodi"),
        constraints=(
            "Soshanguve, Tembisa and Vosloorus require roughly 80 to 120 units.",
            "Mamelodi requires roughly 100 units for March 2027.",
            "Investment scenarios range from approximately ZAR 450,000 to ZAR 850,000.",
            "Do not invent buyer income, mortgage eligibility or household composition.",
        ),
        success_measures=("Qualified property enquiries", "Reduced post-show-house demand drop"),
        budget_minor=45_000_000,
        currency="ZAR",
    ),
    Scenario(
        name="vague_sales_brief",
        client="Example Retailer",
        business_problem="Sales are too low in Gauteng.",
        objective="Increase sales.",
        audiences=(),
        geographies=("Gauteng",),
        constraints=("Budget: ZAR 200,000.",),
        success_measures=("Incremental sales",),
        budget_minor=20_000_000,
        currency="ZAR",
    ),
    Scenario(
        name="contradictory_media_requirements",
        client="Example Brand",
        business_problem="Launch a new service nationally.",
        objective="Build national awareness.",
        audiences=(),
        geographies=("South Africa",),
        constraints=("No audience is supplied.",),
        success_measures=("National awareness",),
        budget_minor=25_000_000,
        currency="ZAR",
        media_requirements=(
            "OOH-only campaign.",
            "Do not use outdoor, OOH or DOOH media.",
        ),
        conflicts=(BriefConflict(
            field_path="mediaRequirements",
            description="The Brief simultaneously requires OOH-only media and prohibits outdoor, OOH and DOOH media.",
            severity="CRITICAL",
            resolved=False,
            resolution=None,
        ),),
    ),
)


UNSUPPORTED_POSITIONING_MARKERS = (
    "reliable", "trusted", "affordable", "cost-effective", "convenient",
    "high quality", "best", "leading", "superior",
)


def _scenario_uuid(name: str, suffix: str) -> uuid.UUID:
    return uuid.uuid5(NAMESPACE, f"{name}:{suffix}")


def _audience_request(scenario: Scenario) -> AudienceAgentRequest:
    brief_id = _scenario_uuid(scenario.name, "brief")
    return AudienceAgentRequest(
        invocation=invocation(
            agent_code=AgentCode.AUDIENCE_INTELLIGENCE,
            tenant_id=TENANT_ID,
            actor_id=ACTOR_ID,
            brief_version_id=brief_id,
            run_id=_scenario_uuid(scenario.name, "run"),
        ),
        planning=PlanningBriefContext(
            brief_version_id=brief_id,
            client_name=scenario.client,
            business_problem=scenario.business_problem,
            objective=scenario.objective,
            audiences=scenario.audiences,
            geographies=scenario.geographies,
            constraints=scenario.constraints,
            success_measures=scenario.success_measures,
            budget_minor=scenario.budget_minor,
            currency=scenario.currency,
            audience_evidence=(),
            reference_evidence=(),
        ),
    )


def _business_flags(scenario: Scenario, segments, positioning: str | None) -> dict[str, object]:
    source_text = " ".join((scenario.business_problem, scenario.objective, *scenario.audiences,
                            *scenario.constraints, *scenario.success_measures)).lower()
    unsupported_positioning = [
        marker for marker in UNSUPPORTED_POSITIONING_MARKERS
        if marker in (positioning or "").lower() and marker not in source_text
    ]
    no_evidence = lambda item: not item["evidence_item_ids"] and not item["reference_observation_ids"]
    required = {name.strip().casefold() for name in scenario.audiences}
    wrong_evidence_free_classification = lambda item: (
        no_evidence(item)
        and item["classification"] != (
            "CLIENT_REQUIREMENT" if item["name"].strip().casefold() in required else "HYPOTHESIS"
        )
    )
    return {
        "evidence_free_non_hypothesis_segments": [
            item["name"] for item in segments if wrong_evidence_free_classification(item)
        ],
        "evidence_free_numeric_confidence": [
            item["name"] for item in segments if no_evidence(item) and item["confidence"] is not None
        ],
        "noncanonical_descriptions": [
            item["name"] for item in segments if not item["description"].startswith((
                "Brief-supplied audience:", "Evidence-supported audience candidate:", "Audience hypothesis:",
            ))
        ],
        "unsupported_positioning_markers": unsupported_positioning,
        "empty_audience_brief_discovered_segments": bool(not scenario.audiences and segments),
    }


def _run(scenario: Scenario) -> dict[str, object]:
    request = _audience_request(scenario)
    try:
        output = execute_agent(
            AgentCode.AUDIENCE_INTELLIGENCE,
            request.model_dump_json().encode("utf-8"),
            BEDROCK_MODE,
        )
    except HTTPException as error:
        return {"scenario": scenario.name, "status": "GOVERNANCE_REJECTED", "detail": error.detail}
    artifact = output["artifact"]
    segments = artifact["audiences"] if artifact else []
    positioning = artifact["positioning_statement"] if artifact else None
    return {
        "scenario": scenario.name,
        "status": "ACCEPTED",
        "segments": segments,
        "targeting_rationale": artifact["targeting_rationale"],
        "positioning_statement": positioning,
        "unknowns": output["unknowns"],
        "usage": output["usage"],
        "business_flags": _business_flags(scenario, segments, positioning),
    }


def test_audience_complexity_live() -> None:
    model = configure_live_bedrock(
        os.environ.get("ADVERTIFIED_AUDIENCE_LIVE_MODEL", "amazon.nova-lite-v1:0")
    )
    results = [_run(scenario) for scenario in SCENARIOS]
    print("\nAUDIENCE INTELLIGENCE COMPLEXITY BATTERY\n")
    for result in results:
        print(json.dumps(result, indent=2, default=str))
    assert len(results) == len(SCENARIOS)
    for scenario, result in zip(SCENARIOS, results, strict=True):
        assert result["status"] == "ACCEPTED", result
        flags = result["business_flags"]
        assert not flags["evidence_free_non_hypothesis_segments"], result
        assert not flags["evidence_free_numeric_confidence"], result
        assert not flags["noncanonical_descriptions"], result
        assert not flags["unsupported_positioning_markers"], result

        segments = result["segments"]
        if scenario.audiences:
            segments_by_name = {
                item["name"].strip().casefold(): item for item in segments
            }
            for required in scenario.audiences:
                retained = segments_by_name.get(required.strip().casefold())
                assert retained is not None, result
                assert retained["is_target"] is True, result

        if scenario.name == "rayetsa_sparse_retail_no_audience":
            assert segments, result

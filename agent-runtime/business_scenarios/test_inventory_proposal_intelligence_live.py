"""Live provider probes over explicitly synthetic facts; no commercial state is mutated."""
from __future__ import annotations

import json
from copy import deepcopy
from uuid import UUID

from agent_registry import AgentCode
from bedrock_provider import BEDROCK_MODE
from business_scenarios.bedrock_support import configure_live_bedrock, invocation
from contracts import ResourceReference
from planning_contracts import InventoryIntelligenceAgentRequest
from proposal_contracts import ProposalNarrativeAgentRequest
from runtime_execution import execute_agent
from tests.test_inventory_intelligence_agent import payload as inventory_fixture, suitability

TENANT_ID = UUID("11111111-1111-1111-1111-111111111111")
ACTOR_ID = UUID("22222222-2222-2222-2222-222222222222")
PLAN_ID = UUID("12e5292d-682c-4fe8-9ca1-1bf0251bd561")
BRIEF_ID = UUID("66666666-6666-6666-6666-666666666666")


def inventory_request():
    payload = inventory_fixture()
    payload["inventory"]["candidates"][0]["name"] = "Synthetic fixture gantry — not real supplier inventory"
    rejected = deepcopy(payload["inventory"]["candidates"][0])
    rejected.update({
        "candidate_id": "157309c1-5866-4f53-8a64-1d909557c694",
        "product_version_id": "f4657282-d09c-4aee-b1a1-938e1c410385",
        "name": "Synthetic stale-rate fixture — not real supplier inventory",
        "is_eligible": False, "score": None, "rejection_reason": "STALE_RATE",
        "rejection_detail": "The published rate does not cover the planned period.",
        "benchmark": None, "suitability": suitability(0),
    })
    payload["inventory"]["candidates"].append(rejected)
    references = tuple(ResourceReference.model_validate_json(json.dumps(item)) for item in payload["invocation"]["resource_refs"])
    payload["invocation"] = invocation(
        agent_code=AgentCode.INVENTORY_INTELLIGENCE, tenant_id=TENANT_ID, actor_id=ACTOR_ID,
        brief_version_id=BRIEF_ID, run_id=UUID("c6fdc8e7-5308-472c-969b-fdacd763824d"),
        additional_resource_refs=references[1:],
    ).model_dump(mode="json")
    return InventoryIntelligenceAgentRequest.model_validate_json(json.dumps(payload))


def proposal_request():
    return ProposalNarrativeAgentRequest.model_validate_json(json.dumps({
        "invocation": invocation(
            agent_code=AgentCode.PROPOSAL_NARRATIVE, tenant_id=TENANT_ID, actor_id=ACTOR_ID,
            brief_version_id=BRIEF_ID, run_id=UUID("e9e2e78a-0ffb-4886-bbe1-c55bf4178b2d"),
            additional_resource_refs=(ResourceReference(
                resource_type="MediaPlanVersion", resource_id=PLAN_ID, version=4,
            ),),
        ).model_dump(mode="json"),
        "proposal": {
            "brief_version_id": str(BRIEF_ID),
            "brief_business_problem": "Synthetic fixture: qualified furniture enquiries have declined",
            "brief_objective": "Evaluate qualified furniture enquiries",
            "success_measures": ["Qualified enquiries", "Showroom visits"],
            "options": [{
                "plan_version_id": str(PLAN_ID), "plan_version": 4, "label": "Synthetic launch option",
                "outcome": "Evaluate qualified response", "budget_minor": 1000001,
                "currency": "ZAR", "channels": ["OOH", "DOOH"],
            }],
        },
    }))


def test_inventory_intelligence_synthetic_live():
    configure_live_bedrock()
    request = inventory_request()
    original = request.model_dump_json()
    output = execute_agent(AgentCode.INVENTORY_INTELLIGENCE, original.encode(), BEDROCK_MODE)
    print("\nSYNTHETIC INVENTORY PROVIDER PROBE\n" + json.dumps(output, indent=2))
    items = output["artifact"]["interpretations"]
    assert {item["candidate_id"] for item in items} == {
        str(item.candidate_id) for item in request.inventory.candidates
    }
    assert all(item["classification"] == "AI_RECOMMENDATION" for item in items)
    rejected = next(item for item in items if item["candidate_id"] == str(request.inventory.candidates[1].candidate_id))
    assert "rate" in rejected["rationale"].casefold()
    assert any(word in rejected["rationale"].casefold() for word in ("ineligible", "excluded", "stale"))
    assert request.model_dump_json() == original
    assert output["suggested_next_action"]["requires_human"] is True
    assert output["usage"]["tool_calls"] == 0


def test_proposal_narrative_synthetic_live():
    configure_live_bedrock()
    request = proposal_request()
    original = request.model_dump_json()
    output = execute_agent(AgentCode.PROPOSAL_NARRATIVE, original.encode(), BEDROCK_MODE)
    print("\nSYNTHETIC PROPOSAL PROVIDER PROBE\n" + json.dumps(output, indent=2))
    summary = output["artifact"]["executive_summary"]
    assert request.proposal.brief_business_problem in summary
    assert request.proposal.brief_objective in summary
    assert "ZAR 10,000.01" in summary
    assert "1,000,001" not in summary and "1000001" not in summary
    assert "Synthetic launch option: Evaluate qualified response" in summary
    assert all(item in summary for item in request.proposal.success_measures)
    assert request.model_dump_json() == original
    assert output["suggested_next_action"]["requires_human"] is True
    assert output["usage"]["tool_calls"] == 0

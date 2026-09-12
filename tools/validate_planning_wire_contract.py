"""Validate actual C# planning/intelligence exports using Python contracts without provider I/O."""

import argparse
import json
from pathlib import Path
import sys
from uuid import uuid4

from pydantic import ValidationError

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "agent-runtime"))
from bedrock_provider import _model_payload  # noqa: E402
from market_intelligence_contracts import MarketIntelligenceAgentRequest  # noqa: E402
from planning_contracts import InventoryIntelligenceAgentRequest  # noqa: E402
from agent_registry import AgentCode  # noqa: E402
from runtime_execution import execute_agent, DETERMINISTIC_MODE  # noqa: E402


def validate_inventory(payload: dict) -> None:
    request = InventoryIntelligenceAgentRequest.model_validate_json(json.dumps(payload))
    assert {candidate.is_eligible for candidate in request.inventory.candidates} == {True, False}
    assert request.invocation.provider_policy.provider == "deterministic"
    assert not request.invocation.provider_policy.allow_live
    output = execute_agent(AgentCode.INVENTORY_INTELLIGENCE, json.dumps(payload).encode(), DETERMINISTIC_MODE)
    assert {item["candidate_id"] for item in output["artifact"]["interpretations"]} == {
        str(item.candidate_id) for item in request.inventory.candidates}
    assert output["usage"]["incremental_cost_minor"] == 0
    payload["inventory"]["candidates"][0]["score"] = 0.01
    try:
        InventoryIntelligenceAgentRequest.model_validate_json(json.dumps(payload))
    except ValidationError as error:
        assert "matching governed" in str(error)
    else:
        raise AssertionError("Mismatched deterministic suitability was accepted")
    print("PASS: actual C# eligible/rejected request validates; mismatched score rejected")


def validate_market(payload: dict) -> None:
    request = MarketIntelligenceAgentRequest.model_validate_json(json.dumps(payload))
    assert request.approved_evidence, "This conformance export must contain real approved evidence snapshots"
    model_packet = _model_payload(request, None)
    assert "invocation" not in model_packet
    assert model_packet["approved_evidence"] == payload["approved_evidence"]
    payload["approved_evidence"][0]["id"] = str(uuid4())
    try:
        MarketIntelligenceAgentRequest.model_validate_json(json.dumps(payload))
    except ValidationError:
        pass
    else:
        raise AssertionError("An evidence snapshot outside invocation authority was accepted")
    print("PASS: actual C# Market request validates; approved content reaches the model packet; substituted evidence is rejected")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("payload", type=Path, help="Repository-relative path to the retained C# JSON export")
    parser.add_argument("--contract", choices=("inventory", "market"), default="inventory")
    args = parser.parse_args()
    path = (ROOT / args.payload).resolve()
    if not path.is_relative_to(ROOT / "artifacts") or path.suffix != ".json":
        raise ValueError("Use a retained JSON export inside repository artifacts.")
    payload = json.loads(path.read_text(encoding="utf-8-sig"))
    {"inventory": validate_inventory, "market": validate_market}[args.contract](payload)


if __name__ == "__main__":
    main()

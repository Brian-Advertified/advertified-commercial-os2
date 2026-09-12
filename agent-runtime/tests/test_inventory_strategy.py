import asyncio
import json
from copy import deepcopy

import pytest
from pydantic import ValidationError

from planning_contracts import InventoryIntelligenceAgentRequest
from runtime_execution import _inventory_model_input
from test_inventory_intelligence_agent import enable, payload, post


def strategy_payload():
    return deepcopy(payload())


def test_strategy_and_audience_measurement_evidence_reach_provider_unchanged():
    body = strategy_payload()
    request = InventoryIntelligenceAgentRequest.model_validate_json(json.dumps(body))

    actual = _inventory_model_input(request)["inventory"]

    assert actual["strategy"] == body["inventory"]["strategy"]
    candidate = actual["candidates"][0]
    assert candidate["audience_fit"] == request.inventory.candidates[0].audience_fit.model_dump(mode="json")
    assert candidate["suitability"] == request.inventory.candidates[0].suitability.model_dump(mode="json")


@pytest.mark.parametrize("invalid", ["version", "period", "unsupported_fact"])
def test_strategy_rejects_unbound_versions_and_invalid_or_invented_facts(invalid):
    body = strategy_payload()
    strategy = body["inventory"]["strategy"]
    if invalid == "version":
        strategy["audience_artifact_version"] += 1
    elif invalid == "period":
        strategy["allocations"][0]["running_periods"][0]["end"] = "2026-08-01"
    else:
        strategy["audiences"][0]["invented_reach"] = 100_000
    with pytest.raises(ValidationError):
        InventoryIntelligenceAgentRequest.model_validate_json(json.dumps(body))


def test_runtime_requires_exact_approved_strategy_context(monkeypatch):
    enable(monkeypatch)
    supplied = asyncio.run(post(strategy_payload()))
    assert supplied.status_code == 200, supplied.text
    assert supplied.json()["artifact"]["interpretations"][0]["classification"] == "AI_RECOMMENDATION"

    missing = payload()
    del missing["inventory"]["strategy"]
    missing_response = asyncio.run(post(missing))
    assert missing_response.status_code == 422

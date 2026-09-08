import asyncio
import json

import pytest
from pydantic import ValidationError

from planning_contracts import InventoryIntelligenceAgentRequest
from runtime_execution import _inventory_model_input
from test_inventory_intelligence_agent import enable, payload, post


def strategy_payload():
    body = payload()
    strategy = {
        "audience_set_id": "11111111-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
        "audience_set_version": 2,
        "media_mix_version_id": "22222222-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
        "media_mix_version": 3,
        "objective": "Increase pharmacy visits",
        "targeting_rationale": "Reach caregivers considering family health purchases.",
        "positioning_statement": "Convenient family care close to home.",
        "audiences": [{
            "id": "33333333-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            "name": "Stay-at-home mothers",
            "need_state": "Access to convenient family care",
            "buying_context": "Pharmacy visits are a hypothesis requiring research.",
            "geographies": ["Johannesburg"],
            "classification": "HYPOTHESIS",
            "exclusions": ["Do not assume school-run travel or household income."],
            "evidence_item_ids": [],
        }],
        "allocations": [{
            "channel": "OOH", "budget_minor": 500_000,
            "role": "Local awareness near verified pharmacies",
            "running_periods": [{"start": "2026-09-01", "end": "2026-09-30"}],
        }],
    }
    body["inventory"]["strategy"] = strategy
    body["invocation"]["resource_refs"] += [
        {"resource_type": "AudienceDefinitionSet", "resource_id": strategy["audience_set_id"],
         "version": strategy["audience_set_version"]},
        {"resource_type": "MediaMixVersion", "resource_id": strategy["media_mix_version_id"],
         "version": strategy["media_mix_version"]},
    ]
    return body


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
        strategy["audience_set_version"] += 1
    elif invalid == "period":
        strategy["allocations"][0]["running_periods"][0]["end"] = "2026-08-01"
    else:
        strategy["audiences"][0]["invented_reach"] = 100_000
    with pytest.raises(ValidationError):
        InventoryIntelligenceAgentRequest.model_validate_json(json.dumps(body))


def test_runtime_accepts_approved_strategy_and_surfaces_legacy_missing_context(monkeypatch):
    enable(monkeypatch)
    supplied = asyncio.run(post(strategy_payload()))
    assert supplied.status_code == 200, supplied.text
    missing = asyncio.run(post(payload()))
    assert missing.status_code == 200, missing.text
    assert any(item["field_path"] == "inventory.strategy" for item in missing.json()["unknowns"])

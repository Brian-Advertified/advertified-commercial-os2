import asyncio
from copy import deepcopy

import httpx
import pytest

from main import DETERMINISTIC_MODE, RUNTIME_MODE_KEY, SERVICE_KEY, app

SERVICE_SECRET = "planning-test-service-key"
BRIEF_ID = "66666666-6666-6666-6666-666666666666"
EVIDENCE_ID = "77777777-7777-7777-7777-777777777777"


def invocation(agent_code: str) -> dict:
    return {
        "schema_version": "1.0.0",
        "tenant_id": "11111111-1111-1111-1111-111111111111",
        "actor_id": "22222222-2222-2222-2222-222222222222",
        "effective_role": "agent_runtime_service",
        "run_id": "33333333-3333-3333-3333-333333333333",
        "step_id": "44444444-4444-4444-4444-444444444444",
        "correlation_id": "55555555-5555-5555-5555-555555555555",
        "agent_code": agent_code,
        "contract_version": "1.0.0",
        "prompt_version": "1.0.0",
        "resource_refs": [{
            "resource_type": "BriefVersion",
            "resource_id": BRIEF_ID,
            "version": 3,
        }],
        "approved_evidence_item_ids": [EVIDENCE_ID],
        "locale": "en-ZA",
        "account_policy_version": "1.0.0",
        "tool_policy": {
            "allowed_tools": [],
            "max_tool_calls": 0,
            "consequence_policy": "PROPOSE_ONLY",
        },
        "provider_policy": {
            "provider": "deterministic",
            "model": "fixture-v1",
            "temperature": 0,
            "timeout_seconds": 30,
            "max_attempts": 1,
            "cost_cap_minor": 0,
            "allow_live": False,
        },
        "resume": {
            "checkpoint_id": None,
            "prior_validated_output_ref": None,
            "prior_usage_ref": None,
        },
    }


def planning() -> dict:
    return {
        "brief_version_id": BRIEF_ID,
        "objective": "Increase qualified furniture enquiries",
        "audiences": ["Small business furniture buyers"],
        "geographies": ["Gauteng"],
    }


def payload(agent_code: str) -> dict:
    context = planning()
    if agent_code == "media_planning":
        context |= {
            "budget_minor": 10_000_01,
            "currency": "ZAR",
            "available_channels": ["RADIO", "OOH", "DIGITAL", "OOH"],
        }
    return {"invocation": invocation(agent_code), "planning": context}


async def post(agent_code: str, body: dict) -> httpx.Response:
    transport = httpx.ASGITransport(app=app)
    headers = {"X-Advertified-Service-Key": SERVICE_SECRET}
    async with httpx.AsyncClient(transport=transport, base_url="http://test") as client:
        return await client.post(f"/v1/agents/{agent_code}", json=body, headers=headers)


def enable(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv(RUNTIME_MODE_KEY, DETERMINISTIC_MODE)
    monkeypatch.setenv(SERVICE_KEY, SERVICE_SECRET)


def test_audience_proposal_is_evidence_bound_without_sensitive_inference(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    enable(monkeypatch)
    response = asyncio.run(post("audience", payload("audience")))

    assert response.status_code == 200, response.text
    output = response.json()
    audience = output["artifact"]["audiences"][0]
    assert audience["classification"] == "INFERENCE"
    assert audience["evidence_item_ids"] == [EVIDENCE_ID]
    assert audience["language"] is None
    assert audience["life_stage"] is None
    assert audience["lsm_sem"] is None
    assert output["usage"]["incremental_cost_minor"] == 0
    assert output["usage"]["tool_calls"] == 0


def test_media_mix_uses_allowed_channels_and_exact_budget(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    enable(monkeypatch)
    response = asyncio.run(post("media_planning", payload("media_planning")))

    assert response.status_code == 200, response.text
    allocations = response.json()["artifact"]["allocations"]
    assert {item["channel"] for item in allocations} <= {"RADIO", "OOH", "DIGITAL"}
    assert len({item["channel"] for item in allocations}) == len(allocations)
    assert sum(item["budget_minor"] for item in allocations) == 10_000_01


def test_planning_contract_rejects_route_mismatch_and_unknown_fields(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    enable(monkeypatch)
    mismatch = payload("media_planning")
    mismatch["invocation"]["agent_code"] = "audience"
    mismatch_response = asyncio.run(post("media_planning", mismatch))
    assert mismatch_response.status_code == 400

    malformed = deepcopy(payload("audience"))
    malformed["planning"]["invented_reach"] = 1_000_000
    malformed_response = asyncio.run(post("audience", malformed))
    assert malformed_response.status_code == 422


def test_planning_contract_requires_exact_brief_resource(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    enable(monkeypatch)
    body = payload("audience")
    body["invocation"]["resource_refs"][0]["resource_id"] = (
        "99999999-9999-9999-9999-999999999999"
    )

    response = asyncio.run(post("audience", body))

    assert response.status_code == 422

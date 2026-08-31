import asyncio
from copy import deepcopy

import httpx
import pytest

from main import DETERMINISTIC_MODE, RUNTIME_MODE_KEY, SERVICE_KEY, app

SERVICE_SECRET = "measurement-test-service-key"
CAMPAIGN_ID = "a1111111-1111-1111-1111-111111111111"
PROOF_ID = "a2222222-2222-2222-2222-222222222222"
EVIDENCE_ID = "a3333333-3333-3333-3333-333333333333"
METRIC_ID = "a4444444-4444-4444-4444-444444444444"


def invocation() -> dict:
    return {
        "schema_version": "1.0.0",
        "tenant_id": "11111111-1111-1111-1111-111111111111",
        "actor_id": "22222222-2222-2222-2222-222222222222",
        "effective_role": "agent_runtime_service",
        "run_id": "33333333-3333-3333-3333-333333333333",
        "step_id": "44444444-4444-4444-4444-444444444444",
        "correlation_id": "55555555-5555-5555-5555-555555555555",
        "agent_code": "measurement",
        "contract_version": "1.0.0",
        "prompt_version": "1.0.0",
        "resource_refs": [
            {"resource_type": "Campaign", "resource_id": CAMPAIGN_ID, "version": 8},
            {"resource_type": "DeliveryProof", "resource_id": PROOF_ID, "version": 2},
            {"resource_type": "PerformanceEvidence", "resource_id": EVIDENCE_ID, "version": 2},
        ],
        "approved_evidence_item_ids": [METRIC_ID],
        "locale": "en-ZA",
        "account_policy_version": "1.0.0",
        "tool_policy": {
            "allowed_tools": [], "max_tool_calls": 0, "consequence_policy": "PROPOSE_ONLY",
        },
        "provider_policy": {
            "provider": "deterministic", "model": "fixture-v1", "temperature": 0,
            "timeout_seconds": 30, "max_attempts": 1, "cost_cap_minor": 0,
            "allow_live": False,
        },
        "resume": {
            "checkpoint_id": None,
            "prior_validated_output_ref": None,
            "prior_usage_ref": None,
        },
    }


def payload() -> dict:
    return {
        "invocation": invocation(),
        "measurement": {
            "campaign_id": CAMPAIGN_ID,
            "campaign_version": 8,
            "measurement_plan": ["Track sourced impressions for the booked flight."],
            "delivery_proofs": [{"id": PROOF_ID, "version": 2}],
            "evidence_sets": [{
                "id": EVIDENCE_ID,
                "version": 2,
                "quality_status": "VERIFIED",
                "methodology": "Verified supplier delivery logs.",
                "limitations": ["Panel data excludes devices without consent."],
                "metrics": [{
                    "id": METRIC_ID,
                    "evidence_set_id": EVIDENCE_ID,
                    "metric_type": "IMPRESSIONS",
                    "value": 125000,
                    "unit": "COUNT",
                    "period_start": "2026-09-01",
                    "period_end": "2026-09-30",
                    "source_locator": "verified.json#/facts/impressions",
                }],
            }],
        },
    }


async def post(body: dict, route: str = "measurement") -> httpx.Response:
    transport = httpx.ASGITransport(app=app)
    headers = {"X-Advertified-Service-Key": SERVICE_SECRET}
    async with httpx.AsyncClient(transport=transport, base_url="http://test") as client:
        return await client.post(f"/v1/agents/{route}", json=body, headers=headers)


def enable(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv(RUNTIME_MODE_KEY, DETERMINISTIC_MODE)
    monkeypatch.setenv(SERVICE_KEY, SERVICE_SECRET)


def test_measurement_binds_every_fact_and_preserves_limitations(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    enable(monkeypatch)
    response = asyncio.run(post(payload()))

    assert response.status_code == 200, response.text
    output = response.json()
    artifact = output["artifact"]
    assert output["status"] == "COMPLETED"
    assert output["usage"]["incremental_cost_minor"] == 0
    assert artifact["causality_status"] == "NOT_ESTABLISHED"
    assert artifact["findings"][0]["metric_ids"] == [METRIC_ID]
    assert artifact["limitations"] == ["Panel data excludes devices without consent."]
    assert artifact["learning_proposals"][0]["requires_new_approval"] is True


def test_measurement_rejects_route_or_evidence_mismatch(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    enable(monkeypatch)
    wrong_route_body = payload()
    wrong_route_body["invocation"]["agent_code"] = "creative"
    route_mismatch = asyncio.run(post(wrong_route_body))
    assert route_mismatch.status_code == 400

    body = deepcopy(payload())
    body["invocation"]["approved_evidence_item_ids"] = []
    evidence_mismatch = asyncio.run(post(body))
    assert evidence_mismatch.status_code == 422

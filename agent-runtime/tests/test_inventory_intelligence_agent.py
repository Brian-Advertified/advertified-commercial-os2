import asyncio
from copy import deepcopy

import httpx
import pytest

from main import DETERMINISTIC_MODE, RUNTIME_MODE_KEY, SERVICE_KEY, app

SERVICE_SECRET = "inventory-intelligence-test-key"
BRIEF_ID = "66666666-6666-6666-6666-666666666666"
SHORTLIST_ID = "88888888-8888-8888-8888-888888888888"
CANDIDATE_ID = "99999999-9999-9999-9999-999999999999"
PRODUCT_VERSION_ID = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"


def invocation() -> dict:
    return {
        "schema_version": "1.0.0",
        "tenant_id": "11111111-1111-1111-1111-111111111111",
        "actor_id": "22222222-2222-2222-2222-222222222222",
        "effective_role": "agent_runtime_service",
        "run_id": "33333333-3333-3333-3333-333333333333",
        "step_id": "44444444-4444-4444-4444-444444444444",
        "correlation_id": "55555555-5555-5555-5555-555555555555",
        "agent_code": "inventory_intelligence",
        "contract_version": "1.0.0",
        "prompt_version": "1.0.0",
        "resource_refs": [
            {
                "resource_type": "BriefVersion",
                "resource_id": BRIEF_ID,
                "version": 3,
            },
            {
                "resource_type": "InventoryShortlistVersion",
                "resource_id": SHORTLIST_ID,
                "version": 1,
            },
        ],
        "approved_evidence_item_ids": [],
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


def eligible_candidate() -> dict:
    return {
        "candidate_id": CANDIDATE_ID,
        "product_version_id": PRODUCT_VERSION_ID,
        "name": "Bree Street Gantry",
        "channel": "OOH",
        "geography": "Johannesburg",
        "rate_amount_minor": 125000,
        "currency": "ZAR",
        "is_eligible": True,
        "rejection_reason": None,
        "rejection_detail": None,
        "score": 0.9,
        "audience_fit": {
            "language_score": 0.8,
            "life_stage_score": None,
            "lsm_sem_score": None,
            "evidence_gaps": [],
            "measurement_source": "Fixture audience study",
            "measurement_period": "2026 Q2",
            "methodology": "Weighted aggregate survey",
            "taxonomy_name": None,
            "taxonomy_version": None,
        },
        "suitability": suitability(0.9),
        "benchmark": {
            "policy_version": "OOH_LOCAL_PEER_V1",
            "geography_basis": "RADIUS_5_KM",
            "cohort_size": 4,
            "median_minor": 166667,
            "percentile": 25,
            "position": "STRONG_VALUE",
            "confidence": 0.7,
            "exclusions": [],
        },
    }


def payload() -> dict:
    return {
        "invocation": invocation(),
        "inventory": {
            "brief_version_id": BRIEF_ID,
            "shortlist_version_id": SHORTLIST_ID,
            "candidates": [eligible_candidate()],
        },
    }


def suitability(total: float) -> dict:
    return {
        "policy_version": "INVENTORY_SUITABILITY_V1",
        "geography": total,
        "audience_context": total,
        "objective_format": total,
        "budget_efficiency": total,
        "evidence_quality_freshness": total,
        "portfolio_coverage_diversity": total,
        "total": total,
        "evidence_gaps": [],
    }


async def post(body: dict) -> httpx.Response:
    transport = httpx.ASGITransport(app=app)
    headers = {"X-Advertified-Service-Key": SERVICE_SECRET}
    async with httpx.AsyncClient(
        transport=transport,
        base_url="http://test",
    ) as client:
        return await client.post(
            "/v1/agents/inventory_intelligence",
            json=body,
            headers=headers,
        )


def enable(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv(RUNTIME_MODE_KEY, DETERMINISTIC_MODE)
    monkeypatch.setenv(SERVICE_KEY, SERVICE_SECRET)


def test_inventory_intelligence_explains_only_supplied_governed_facts(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    enable(monkeypatch)

    response = asyncio.run(post(payload()))

    assert response.status_code == 200, response.text
    output = response.json()
    interpretation = output["artifact"]["interpretations"][0]
    assert interpretation["candidate_id"] == CANDIDATE_ID
    assert "governed hard constraints" in interpretation["rationale"]
    assert "4 compatible peers" in interpretation["rationale"]
    assert "70%" in interpretation["rationale"]
    assert output["usage"]["incremental_cost_minor"] == 0
    assert output["usage"]["tool_calls"] == 0


def test_inventory_intelligence_preserves_deterministic_rejection(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    enable(monkeypatch)
    body = payload()
    candidate = body["inventory"]["candidates"][0]
    candidate |= {
        "is_eligible": False,
        "score": None,
        "rejection_reason": "STALE_RATE",
        "rejection_detail": "The published rate does not cover the planned period.",
        "benchmark": None,
        "suitability": suitability(0),
    }

    response = asyncio.run(post(body))

    assert response.status_code == 200, response.text
    rationale = response.json()["artifact"]["interpretations"][0]["rationale"]
    assert rationale == (
        "Excluded by governed hard eligibility: "
        "The published rate does not cover the planned period."
    )


def test_inventory_intelligence_requires_exact_resources_and_strict_facts(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    enable(monkeypatch)
    missing_resource = payload()
    missing_resource["invocation"]["resource_refs"] = [
        missing_resource["invocation"]["resource_refs"][0],
    ]
    missing_response = asyncio.run(post(missing_resource))
    assert missing_response.status_code == 422

    invented = deepcopy(payload())
    invented["inventory"]["candidates"][0]["invented_reach"] = 1_000_000
    invented_response = asyncio.run(post(invented))
    assert invented_response.status_code == 422

    unscored = deepcopy(payload())
    unscored["inventory"]["candidates"][0]["score"] = None
    unscored_response = asyncio.run(post(unscored))
    assert unscored_response.status_code == 422

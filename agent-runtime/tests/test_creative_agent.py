import asyncio
from copy import deepcopy

import httpx
import pytest

from main import DETERMINISTIC_MODE, RUNTIME_MODE_KEY, SERVICE_KEY, app

SERVICE_SECRET = "creative-test-service-key"


def invocation() -> dict:
    return {
        "schema_version": "1.0.0",
        "tenant_id": "11111111-1111-1111-1111-111111111111",
        "actor_id": "22222222-2222-2222-2222-222222222222",
        "effective_role": "agent_runtime_service",
        "run_id": "33333333-3333-3333-3333-333333333333",
        "step_id": "44444444-4444-4444-4444-444444444444",
        "correlation_id": "55555555-5555-5555-5555-555555555555",
        "agent_code": "creative",
        "contract_version": "1.0.0",
        "prompt_version": "1.0.0",
        "resource_refs": [{
            "resource_type": "BriefVersion",
            "resource_id": "66666666-6666-6666-6666-666666666666",
            "version": 1,
        }],
        "approved_evidence_item_ids": ["77777777-7777-7777-7777-777777777777"],
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


def payload() -> dict:
    return {
        "invocation": invocation(),
        "brief": {
            "brief_version_id": "66666666-6666-6666-6666-666666666666",
            "client_name": "Rayetsa Furniture",
            "objective": "Grow demand for the supplied furniture range.",
            "audiences": ["Furniture buyers"],
            "geographies": ["Gauteng"],
            "campaign_start": "2026-09-01",
            "campaign_end": "2026-09-30",
        },
        "brand_notes": ["Keep the furniture as the visual hero."],
        "assets": [{
            "id": "88888888-8888-8888-8888-888888888888",
            "asset_type": "PRODUCT_IMAGE",
            "object_key": "clients/rayetsa/lindiwe-50.png",
            "source_document_id": "99999999-9999-9999-9999-999999999999",
            "source_locator": "pdf#page=5",
            "rights_status": "APPROVED",
            "product_name": "Lindiwe 50 Seater",
            "evidence_item_ids": ["77777777-7777-7777-7777-777777777777"],
        }],
        "products": [{
            "name": "Lindiwe 50 Seater",
            "category": "Furniture combo",
            "asset_ids": ["88888888-8888-8888-8888-888888888888"],
            "evidence_item_ids": ["77777777-7777-7777-7777-777777777777"],
            "current_price_minor": 6999900,
            "currency": "ZAR",
            "offer_valid_from": "2026-08-05",
            "offer_valid_until": "2026-09-30",
        }],
        "formats": [{
            "channel": "META",
            "format_code": "FEED_4X5",
            "width": 1080,
            "height": 1350,
        }],
    }


async def post(body: dict) -> httpx.Response:
    transport = httpx.ASGITransport(app=app)
    headers = {"X-Advertified-Service-Key": SERVICE_SECRET}
    async with httpx.AsyncClient(transport=transport, base_url="http://test") as client:
        return await client.post("/v1/agents/creative", json=body, headers=headers)


def enable(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv(RUNTIME_MODE_KEY, DETERMINISTIC_MODE)
    monkeypatch.setenv(SERVICE_KEY, SERVICE_SECRET)


def test_creative_uses_real_product_asset_and_verified_current_price(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    enable(monkeypatch)
    response = asyncio.run(post(payload()))

    assert response.status_code == 200, response.text
    output = response.json()
    concept = output["artifact"]["territories"][0]["channel_concepts"][0]
    assert output["status"] == "REVIEW_REQUIRED"
    assert output["usage"]["incremental_cost_minor"] == 0
    assert concept["preserve_supplied_products"] is True
    assert concept["source_asset_ids"] == ["88888888-8888-8888-8888-888888888888"]
    price = next(item for item in concept["text_elements"] if item["role"] == "PRICE")
    assert price == {"role": "PRICE", "text": "R69 999.00", "verified": True}


def test_creative_excludes_expired_price_and_uncleared_product_image(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    enable(monkeypatch)
    body = deepcopy(payload())
    body["brief"]["campaign_start"] = "2026-10-01"
    body["brief"]["campaign_end"] = "2026-12-31"
    body["assets"][0]["rights_status"] = "UNKNOWN"

    response = asyncio.run(post(body))

    assert response.status_code == 200, response.text
    output = response.json()
    concept = output["artifact"]["territories"][0]["channel_concepts"][0]
    assert concept["source_asset_ids"] == []
    assert not any(item["role"] == "PRICE" for item in concept["text_elements"])
    codes = {warning["code"] for warning in output["artifact"]["warnings"]}
    assert {
        "OFFER_OUTSIDE_CAMPAIGN",
        "NO_APPROVED_PRODUCT_IMAGE",
        "ASSET_RIGHTS_UNCONFIRMED",
    } <= codes

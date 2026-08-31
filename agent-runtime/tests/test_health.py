import asyncio

import httpx
import pytest

from main import DETERMINISTIC_MODE, RUNTIME_MODE_KEY, app


def get(path: str) -> httpx.Response:
    async def send() -> httpx.Response:
        transport = httpx.ASGITransport(app=app)
        async with httpx.AsyncClient(
            transport=transport,
            base_url="http://test",
        ) as client:
            return await client.get(path)

    return asyncio.run(send())


def test_runtime_description_is_truthful_and_provider_disabled() -> None:
    response = get("/")

    assert response.status_code == 200
    assert response.json()["implemented_agents"] == []
    assert response.json()["provider_mode"] == "disabled"


def test_liveness_endpoint() -> None:
    response = get("/health/live")

    assert response.status_code == 200
    assert response.json()["status"] == "healthy"


def test_readiness_endpoint_does_not_claim_provider_readiness() -> None:
    response = get("/health/ready")

    assert response.status_code == 200
    assert "provider-disabled" in response.json()["checks"]


def test_deterministic_description_lists_only_implemented_agents(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    monkeypatch.setenv(RUNTIME_MODE_KEY, DETERMINISTIC_MODE)

    response = get("/")

    assert response.status_code == 200
    implemented = set(response.json()["implemented_agents"])
    assert implemented == {
        "business_interpretation",
        "opportunity_intelligence",
        "strategy",
        "critic_readiness",
        "brief_drafting",
        "audience",
        "media_planning",
        "proposal_narrative",
        "creative",
        "measurement",
    }

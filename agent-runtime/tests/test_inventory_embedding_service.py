import asyncio
import math

import httpx

from inventory_embedding_service import DIMENSIONS, TITAN_MODEL
from main import DETERMINISTIC_MODE, RUNTIME_MODE_KEY, SERVICE_KEY, app

SERVICE_SECRET = "inventory-embedding-test-key"


def test_deterministic_inventory_embedding_is_normalized_and_repeatable(
    monkeypatch,
) -> None:
    monkeypatch.setenv(RUNTIME_MODE_KEY, DETERMINISTIC_MODE)
    monkeypatch.setenv(SERVICE_KEY, SERVICE_SECRET)
    payload = {
        "canonical_text": "name:Bree Street Gantry\nchannel:OOH\ngeography:Johannesburg",
        "model": TITAN_MODEL,
        "dimensions": DIMENSIONS,
        "normalize": True,
    }

    first = asyncio.run(_post(payload))
    second = asyncio.run(_post(payload))

    assert first.status_code == 200
    assert second.status_code == 200
    body = first.json()
    assert body["model"] == "fixture-inventory-embedding-v1"
    assert body["region"] == "local"
    assert len(body["embedding"]) == DIMENSIONS
    assert math.isclose(
        math.sqrt(sum(value * value for value in body["embedding"])),
        1.0,
        rel_tol=1e-6,
    )
    assert body["embedding"] == second.json()["embedding"]
    assert body["incremental_cost_usd_micros"] == 0


async def _post(payload: dict) -> httpx.Response:
    transport = httpx.ASGITransport(app=app)
    async with httpx.AsyncClient(transport=transport, base_url="http://test") as client:
        return await client.post(
            "/v1/inventory-embeddings",
            json=payload,
            headers={"X-Advertified-Service-Key": SERVICE_SECRET},
        )

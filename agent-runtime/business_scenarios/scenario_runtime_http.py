"""In-process HTTP exercises authentication and typed runtime admission without a socket."""
import asyncio
import os
from unittest.mock import patch

import httpx

from main import app, RUNTIME_MODE_KEY, SERVICE_KEY

TEST_KEY = "synthetic-scenario-service-key"


def invoke(agent, request):
    with patch.dict(os.environ, {RUNTIME_MODE_KEY: "deterministic", SERVICE_KEY: TEST_KEY}):
        return asyncio.run(_invoke(agent, request))


async def _invoke(agent, request):
    transport = httpx.ASGITransport(app=app)
    async with httpx.AsyncClient(transport=transport, base_url="http://scenario.invalid") as client:
        denied = await client.post(f"/v1/agents/{agent}", json=request)
        response = await client.post(f"/v1/agents/{agent}", json=request,
                                     headers={"X-Advertified-Service-Key": TEST_KEY})
    return denied, response

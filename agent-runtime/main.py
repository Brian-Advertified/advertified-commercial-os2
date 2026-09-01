"""Advertified typed agent runtime with deterministic and bounded Bedrock modes."""

import os
import secrets
from typing import Literal

from fastapi import FastAPI, Header, HTTPException, Request
from pydantic import BaseModel

from agent_registry import AgentCode
from bedrock_provider import BEDROCK_MODE, bedrock_configuration_ready
from runtime_execution import DETERMINISTIC_MODE, execute_agent, implemented_agents

RUNTIME_MODE_KEY = "ADVERTIFIED_AGENT_RUNTIME_MODE"
SERVICE_KEY = "ADVERTIFIED_AGENT_RUNTIME_SERVICE_KEY"
DISABLED_MODE = "disabled"


class RuntimeDescription(BaseModel):
    service: str
    status: Literal["provider_disabled", "deterministic_ready", "bedrock_ready"]
    provider_mode: Literal["disabled", "deterministic", "bedrock"]
    implemented_agents: list[str]


class HealthResponse(BaseModel):
    status: Literal["healthy", "ready"]
    service: str
    checks: list[str]


app = FastAPI(
    title="Advertified Agent Runtime",
    description="Typed proposal runtime; canonical state remains in the Commercial API.",
    version="0.2.0",
)


@app.get("/", response_model=RuntimeDescription)
def describe_runtime() -> RuntimeDescription:
    mode = _runtime_mode()
    ready = _provider_configuration_ready(mode)
    implemented = [code.value for code in AgentCode if code in implemented_agents()]
    status = _runtime_status(mode, ready)
    return RuntimeDescription(
        service="Advertified Agent Runtime",
        status=status,
        provider_mode=mode if ready else DISABLED_MODE,
        implemented_agents=implemented if ready else [],
    )


@app.get("/health/live", response_model=HealthResponse)
def live() -> HealthResponse:
    return HealthResponse(
        status="healthy",
        service="advertified-agent-runtime",
        checks=["process"],
    )


@app.get("/health/ready", response_model=HealthResponse)
def ready() -> HealthResponse:
    mode = _runtime_mode()
    if mode == DISABLED_MODE:
        return HealthResponse(
            status="ready",
            service="advertified-agent-runtime",
            checks=["process", "provider-disabled"],
        )
    if not _provider_configuration_ready(mode):
        raise HTTPException(status_code=503, detail="Agent provider is not ready.")
    check = "deterministic-zero-cost" if mode == DETERMINISTIC_MODE else "bedrock-configured"
    return HealthResponse(
        status="ready",
        service="advertified-agent-runtime",
        checks=["process", check],
    )


@app.post("/v1/agents/{agent_code}")
async def invoke(
    agent_code: AgentCode,
    http_request: Request,
    x_advertified_service_key: str | None = Header(default=None),
) -> dict[str, object]:
    mode = _runtime_mode()
    _require_service(mode, x_advertified_service_key)
    return execute_agent(agent_code, await http_request.body(), mode)


def _runtime_mode() -> Literal["disabled", "deterministic", "bedrock"]:
    mode = os.environ.get(RUNTIME_MODE_KEY, DISABLED_MODE).strip().lower()
    return mode if mode in (DETERMINISTIC_MODE, BEDROCK_MODE) else DISABLED_MODE


def _provider_configuration_ready(mode: str) -> bool:
    if mode == DETERMINISTIC_MODE:
        return True
    if mode == BEDROCK_MODE:
        return bedrock_configuration_ready()
    return False


def _runtime_status(
    mode: str,
    ready: bool,
) -> Literal["provider_disabled", "deterministic_ready", "bedrock_ready"]:
    if not ready:
        return "provider_disabled"
    return "deterministic_ready" if mode == DETERMINISTIC_MODE else "bedrock_ready"


def _require_service(mode: str, provided_key: str | None) -> None:
    if not _provider_configuration_ready(mode):
        raise HTTPException(status_code=503, detail="Agent provider is disabled.")
    expected_key = os.environ.get(SERVICE_KEY)
    if (
        not expected_key
        or not provided_key
        or not secrets.compare_digest(provided_key, expected_key)
    ):
        raise HTTPException(status_code=401, detail="Service authentication required.")

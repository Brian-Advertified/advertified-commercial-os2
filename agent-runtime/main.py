"""Advertified provider-disabled runtime with explicit deterministic mode."""

import os
import secrets
from typing import Literal

from fastapi import FastAPI, Header, HTTPException, Request
from pydantic import BaseModel, ValidationError

from agent_registry import AgentCode
from creative_contracts import CreativeAgentRequest
from creative_service import generate_creative_concepts
from opportunity_contracts import OpportunityAgentRequest
from opportunity_service import HANDLERS

RUNTIME_MODE_KEY = "ADVERTIFIED_AGENT_RUNTIME_MODE"
SERVICE_KEY = "ADVERTIFIED_AGENT_RUNTIME_SERVICE_KEY"
DETERMINISTIC_MODE = "deterministic"


class RuntimeDescription(BaseModel):
    service: str
    status: Literal["baseline", "gate5"]
    provider_mode: Literal["disabled", "deterministic"]
    implemented_agents: list[str]


class HealthResponse(BaseModel):
    status: Literal["healthy", "ready"]
    service: str
    checks: list[str]


app = FastAPI(
    title="Advertified Agent Runtime",
    description="Typed proposal runtime; canonical state remains in the Commercial API.",
    version="0.1.0",
)


@app.get("/", response_model=RuntimeDescription)
def describe_runtime() -> RuntimeDescription:
    """Describe only capabilities that exist in this baseline."""
    enabled = _deterministic_enabled()
    implemented = [code.value for code in HANDLERS]
    implemented.append(AgentCode.CREATIVE.value)
    return RuntimeDescription(
        service="Advertified Agent Runtime",
        status="gate5" if enabled else "baseline",
        provider_mode="disabled" if not enabled else "deterministic",
        implemented_agents=[] if not enabled else implemented,
    )


@app.get("/health/live", response_model=HealthResponse)
def live() -> HealthResponse:
    """Report process liveness without claiming provider availability."""
    return HealthResponse(
        status="healthy",
        service="advertified-agent-runtime",
        checks=["process"],
    )


@app.get("/health/ready", response_model=HealthResponse)
def ready() -> HealthResponse:
    """Report baseline readiness; no external provider is enabled."""
    enabled = _deterministic_enabled()
    return HealthResponse(
        status="ready",
        service="advertified-agent-runtime",
        checks=["process", "deterministic-zero-cost" if enabled else "provider-disabled"],
    )


@app.post("/v1/agents/{agent_code}")
async def invoke(
    agent_code: AgentCode,
    http_request: Request,
    x_advertified_service_key: str | None = Header(default=None),
) -> dict[str, object]:
    """Invoke one allow-listed, zero-cost proposal contract."""
    _require_deterministic_service(x_advertified_service_key)
    body = await http_request.body()
    if agent_code == AgentCode.CREATIVE:
        return _invoke_creative(body)
    return _invoke_opportunity(agent_code, body)


def _invoke_creative(body: bytes) -> dict[str, object]:
    request = _validate_json(CreativeAgentRequest, body)
    if request.invocation.agent_code != AgentCode.CREATIVE:
        raise HTTPException(status_code=400, detail="Agent contract does not match the route.")
    return generate_creative_concepts(request).model_dump(mode="json")


def _invoke_opportunity(agent_code: AgentCode, body: bytes) -> dict[str, object]:
    request = _validate_json(OpportunityAgentRequest, body)
    if request.invocation.agent_code != agent_code or agent_code not in HANDLERS:
        raise HTTPException(status_code=400, detail="Agent contract does not match the route.")
    return HANDLERS[agent_code](request).model_dump(mode="json")


def _validate_json(model_type, body: bytes):
    try:
        return model_type.model_validate_json(body)
    except ValidationError as error:
        raise HTTPException(
            status_code=422,
            detail=error.errors(include_input=False),
        ) from error


def _deterministic_enabled() -> bool:
    return os.environ.get(RUNTIME_MODE_KEY) == DETERMINISTIC_MODE


def _require_deterministic_service(provided_key: str | None) -> None:
    if not _deterministic_enabled():
        raise HTTPException(status_code=503, detail="Deterministic runtime is disabled.")
    expected_key = os.environ.get(SERVICE_KEY)
    if (
        not expected_key
        or not provided_key
        or not secrets.compare_digest(provided_key, expected_key)
    ):
        raise HTTPException(status_code=401, detail="Service authentication required.")

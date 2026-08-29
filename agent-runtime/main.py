"""Advertified provider-disabled runtime with explicit Gate 4 deterministic mode."""

import os
import secrets
from typing import Literal

from fastapi import FastAPI, Header, HTTPException, Request
from pydantic import BaseModel, ValidationError

from agent_registry import AgentCode
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
    return RuntimeDescription(
        service="Advertified Agent Runtime",
        status="gate5" if enabled else "baseline",
        provider_mode="disabled" if not enabled else "deterministic",
        implemented_agents=[] if not enabled else [code.value for code in HANDLERS],
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
    try:
        request = OpportunityAgentRequest.model_validate_json(await http_request.body())
    except ValidationError as error:
        raise HTTPException(
            status_code=422,
            detail=error.errors(include_input=False),
        ) from error
    if request.invocation.agent_code != agent_code or agent_code not in HANDLERS:
        raise HTTPException(status_code=400, detail="Agent contract does not match the route.")
    output = HANDLERS[agent_code](request)
    return output.model_dump(mode="json")


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

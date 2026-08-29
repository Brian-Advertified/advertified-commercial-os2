"""Advertified provider-disabled agent runtime baseline."""

from typing import Literal

from fastapi import FastAPI
from pydantic import BaseModel


class RuntimeDescription(BaseModel):
    service: str
    status: Literal["baseline"]
    provider_mode: Literal["disabled"]
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
    return RuntimeDescription(
        service="Advertified Agent Runtime",
        status="baseline",
        provider_mode="disabled",
        implemented_agents=[],
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
    return HealthResponse(
        status="ready",
        service="advertified-agent-runtime",
        checks=["process", "provider-disabled"],
    )

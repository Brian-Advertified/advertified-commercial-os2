import asyncio
from pathlib import Path
from uuid import UUID

import pytest
from pydantic import ConfigDict, TypeAdapter, ValidationError

from agent_registry import AgentCode
from contracts import (
    AgentInvocationEnvelope,
    ContractModel,
    EvaluationFixture,
    ProviderPolicy,
)
from provider import DeterministicFixtureNotFoundError, DeterministicProvider

FIXTURE_PATH = Path(__file__).parent / "fixtures" / "gate1_contract_v1.json"


class ContractProbeArtifact(ContractModel):
    model_config = ConfigDict(extra="forbid", frozen=True, strict=True)

    summary: str
    source_version: int


FixtureAdapter = TypeAdapter(EvaluationFixture[ContractProbeArtifact])


def load_fixture() -> EvaluationFixture[ContractProbeArtifact]:
    return FixtureAdapter.validate_json(FIXTURE_PATH.read_text(encoding="utf-8"))


def test_closed_agent_roster_contains_exactly_the_eleven_approved_agents() -> None:
    assert len(AgentCode) == 11
    assert AgentCode.BUSINESS_INTERPRETATION.value == "business_interpretation"
    assert AgentCode.MEASUREMENT.value == "measurement"


def test_deterministic_provider_returns_exact_typed_zero_cost_fixture() -> None:
    fixture = load_fixture()
    provider = DeterministicProvider([fixture])

    output = asyncio.run(provider.invoke(fixture.invocation))

    assert output == fixture.output
    assert output.artifact is not None
    assert output.artifact.source_version == 3
    assert output.usage.incremental_cost_minor == 0
    assert output.usage.tool_calls == 0


def test_unmatched_invocation_fails_without_fallback_or_live_call() -> None:
    fixture = load_fixture()
    provider = DeterministicProvider([fixture])
    changed = fixture.invocation.model_copy(
        update={"step_id": UUID("88888888-8888-8888-8888-888888888888")}
    )

    with pytest.raises(DeterministicFixtureNotFoundError):
        asyncio.run(provider.invoke(changed))


def test_live_or_paid_provider_policy_is_schema_invalid() -> None:
    fixture = load_fixture()
    unsafe_policy = fixture.invocation.provider_policy.model_dump()
    unsafe_policy["allow_live"] = True
    unsafe_policy["cost_cap_minor"] = 1

    with pytest.raises(ValidationError):
        ProviderPolicy.model_validate(unsafe_policy)


def test_material_field_without_evidence_assumption_or_unknown_is_invalid() -> None:
    fixture = load_fixture()
    payload = fixture.model_dump()
    payload["expectation"]["material_field_paths"] = (
        "artifact.summary",
        "artifact.unsupported_claim",
    )

    with pytest.raises(ValidationError):
        FixtureAdapter.validate_python(payload)


def test_exact_resource_versions_are_required() -> None:
    fixture = load_fixture()
    payload = fixture.invocation.model_dump()
    payload["resource_refs"] = []

    with pytest.raises(ValidationError):
        AgentInvocationEnvelope.model_validate(payload)

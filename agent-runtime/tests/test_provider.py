import asyncio
from pathlib import Path
from uuid import UUID

import pytest
from pydantic import ConfigDict, TypeAdapter, ValidationError

from agent_registry import AgentCode
from bedrock_provider import (
    ALLOWLIST_KEY,
    MAX_TOKENS_KEY,
    PRICING_KEY,
    REGION_KEY,
    bedrock_configuration_ready,
)
from contracts import (
    AgentInvocationEnvelope,
    ContractModel,
    EvaluationFixture,
    ProviderPolicy,
)
from provider import DeterministicFixtureNotFoundError, DeterministicProvider

FIXTURE_PATH = Path(__file__).parent / "fixtures" / "provider_contract_v1.json"


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


def test_provider_policy_keeps_deterministic_zero_cost_and_bounds_bedrock() -> None:
    fixture = load_fixture()
    unsafe_deterministic = fixture.invocation.provider_policy.model_dump()
    unsafe_deterministic["allow_live"] = True
    unsafe_deterministic["cost_cap_minor"] = 1
    with pytest.raises(ValidationError):
        ProviderPolicy.model_validate(unsafe_deterministic)

    live = ProviderPolicy.model_validate({
        "provider": "bedrock",
        "model": "us.amazon.nova-lite-v1:0",
        "temperature": 0,
        "timeout_seconds": 30,
        "max_attempts": 2,
        "cost_cap_minor": 50,
        "allow_live": True,
    })
    assert live.provider == "bedrock"
    assert live.cost_cap_minor == 50

    for changed in (
        {"cost_cap_minor": 0},
        {"allow_live": False},
        {"model": "fixture-v1"},
    ):
        payload = live.model_dump() | changed
        with pytest.raises(ValidationError):
            ProviderPolicy.model_validate(payload)


def test_bedrock_configuration_is_verified_without_network(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    model = "us.amazon.nova-lite-v1:0"
    monkeypatch.setenv(REGION_KEY, "af-south-1")
    monkeypatch.setenv(ALLOWLIST_KEY, model)
    monkeypatch.setenv(MAX_TOKENS_KEY, "2048")
    monkeypatch.setenv(
        PRICING_KEY,
        '{"us.amazon.nova-lite-v1:0":{"input_per_million_usd":"0.10",'
        '"output_per_million_usd":"0.40"}}',
    )
    assert bedrock_configuration_ready()

    monkeypatch.setenv(PRICING_KEY, "{}")
    assert not bedrock_configuration_ready()


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

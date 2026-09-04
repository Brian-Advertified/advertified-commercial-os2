import asyncio
import json
from pathlib import Path
from uuid import UUID

import pytest
from botocore.exceptions import ClientError
from pydantic import BaseModel, ConfigDict, TypeAdapter, ValidationError

from agent_registry import AgentCode
from bedrock_multimodal import (
    conservative_input_token_estimate,
    count_input_tokens,
)
from bedrock_output import _response_payload
from bedrock_provider import (
    ALLOWLIST_KEY,
    MAX_TOKENS_KEY,
    MULTIMODAL_ALLOWLIST_KEY,
    PRICING_KEY,
    REGION_KEY,
    bedrock_configuration_ready,
)
from bedrock_schema import source_bound_schema, structured_output_tool
from bedrock_semantic_output import semantic_schema, wrap_semantic_output
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

    live = ProviderPolicy.model_validate(
        {
            "provider": "bedrock",
            "model": "us.amazon.nova-lite-v1:0",
            "temperature": 0,
            "timeout_seconds": 30,
            "max_attempts": 1,
            "cost_cap_minor": 50,
            "allow_live": True,
        }
    )
    assert live.provider == "bedrock"
    assert live.cost_cap_minor == 50

    for changed in (
        {"cost_cap_minor": 0},
        {"allow_live": False},
        {"model": "fixture-v1"},
        {"max_attempts": 2},
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
    monkeypatch.setenv(MULTIMODAL_ALLOWLIST_KEY, model)
    monkeypatch.setenv(MAX_TOKENS_KEY, "2048")
    monkeypatch.setenv(
        PRICING_KEY,
        '{"us.amazon.nova-lite-v1:0":{"input_per_million_usd":"0.10",'
        '"output_per_million_usd":"0.40"}}',
    )
    assert bedrock_configuration_ready()

    monkeypatch.setenv(PRICING_KEY, "{}")
    assert not bedrock_configuration_ready()


def test_unsupported_free_token_count_uses_conservative_local_estimate() -> None:
    class UnsupportedClient:
        def count_tokens(self, **_):
            raise ClientError(
                {
                    "Error": {
                        "Code": "ValidationException",
                        "Message": "The provided model doesn't support counting tokens.",
                    },
                },
                "CountTokens",
            )

    system = [{"text": "system"}]
    messages = [
        {
            "role": "user",
            "content": [
                {"text": "payload"},
                {"image": {"format": "png", "source": {"bytes": b"image"}}},
            ],
        }
    ]

    assert count_input_tokens(UnsupportedClient(), "model", system, messages) is None
    assert conservative_input_token_estimate(system, messages) >= 36_869


def test_bedrock_structured_output_is_forced_and_read_from_tool_use() -> None:
    schema = '{"type":"object","properties":{"value":{"type":"string"}}}'
    tool = structured_output_tool(schema)
    assert tool["toolChoice"] == {
        "tool": {"name": "submit_advertified_result"},
    }
    response = {
        "output": {
            "message": {
                "content": [
                    {
                        "toolUse": {
                            "name": "submit_advertified_result",
                            "input": {"value": "grounded"},
                        }
                    }
                ]
            }
        },
    }
    assert _response_payload(response) == {"value": "grounded"}


def test_semantic_output_schema_only_allows_supplied_source_locators() -> None:
    class Source(BaseModel):
        locator: str

    class Request(BaseModel):
        source_items: tuple[Source, ...]
        source_images: tuple[Source, ...]

    schema = json.dumps(
        {
            "type": "object",
            "properties": {
                "source_locator": {"type": "string"},
                "omitted_source_locators": {
                    "type": "array",
                    "items": {"type": "string"},
                },
            },
        }
    )
    bounded = json.loads(
        source_bound_schema(
            schema,
            Request(
                source_items=(Source(locator="text:1"),),
                source_images=(Source(locator="image:1"),),
            ),
        )
    )

    expected = ["text:1", "image:1"]
    assert bounded["properties"]["source_locator"]["enum"] == expected
    assert bounded["properties"]["omitted_source_locators"]["items"]["enum"] == expected


def test_source_operation_schema_prevents_cross_stage_fields() -> None:
    class Located(BaseModel):
        locator: str

    class Request(BaseModel):
        operation: str
        source_items: tuple[Located, ...]
        source_images: tuple[Located, ...]
        existing_rows: tuple[Located, ...]

    schema = json.dumps({
        "type": "object",
        "$defs": {
            "ProposedInventoryCandidate": {
                "type": "object",
                "properties": {
                    "source_locator": {"type": "string"},
                    "fields": {"type": "array"},
                },
            },
            "ProposedInventoryField": {
                "type": "object",
                "properties": {
                    "field_name": {
                        "type": "string",
                        "enum": [
                            "name", "dimensions", "channel",
                            "product_type", "description", "rate_type",
                        ],
                    },
                    "normalized_value": {
                        "anyOf": [
                            {"type": "string"},
                            {"type": "null"},
                        ],
                    },
                    "source_locator": {"type": "string"},
                    "evidence_basis": {
                        "type": "string",
                        "enum": ["SUPPLIER_SUPPLIED", "DERIVED_POLICY"],
                    },
                    "transformation": {
                        "type": "string",
                        "enum": [
                            "TRIM", "DERIVED_FROM_CHANNEL",
                            "DERIVED_FROM_SOURCE_CONTEXT",
                        ],
                    },
                },
            },
        },
        "properties": {
            "omitted_source_locators": {
                "type": "array",
                "items": {"type": "string"},
            },
        },
    })
    sources = (Located(locator="text:1"),)
    images = (Located(locator="image:1"),)
    rows = (Located(locator="row:1"),)

    semantic = json.loads(source_bound_schema(
        schema,
        Request(
            operation="SEMANTIC_ENRICHMENT",
            source_items=sources,
            source_images=images,
            existing_rows=rows,
        ),
    ))
    semantic_candidate = semantic["$defs"]["ProposedInventoryCandidate"]
    semantic_field = semantic["$defs"]["ProposedInventoryField"]
    assert semantic_candidate["properties"]["source_locator"]["enum"] == ["row:1"]
    assert semantic_field["properties"]["source_locator"]["enum"] == [
        "text:1", "image:1", "row:1",
    ]
    assert semantic_field["properties"]["field_name"]["enum"] == [
        "channel", "product_type", "description",
    ]
    assert semantic_field["properties"]["evidence_basis"]["enum"] == [
        "DERIVED_POLICY",
    ]
    assert semantic_field["properties"]["normalized_value"]["type"] == "string"

    transcription = json.loads(source_bound_schema(
        schema,
        Request(
            operation="SOURCE_TRANSCRIPTION",
            source_items=sources,
            source_images=images,
            existing_rows=(),
        ),
    ))
    transcription_field = transcription["$defs"]["ProposedInventoryField"]
    assert transcription_field["properties"]["field_name"]["enum"] == [
        "name", "dimensions",
    ]
    assert transcription_field["properties"]["evidence_basis"]["enum"] == [
        "SUPPLIER_SUPPLIED",
    ]
    assert transcription_field["properties"]["normalized_value"]["type"] == "null"


def test_semantic_facts_receive_deterministic_review_governance() -> None:
    assert "summary" in json.loads(semantic_schema(ContractProbeArtifact))["properties"]

    output = wrap_semantic_output(
        ContractProbeArtifact,
        {"summary": "Source fact", "source_version": 3},
    )

    assert output.status.value == "REVIEW_REQUIRED"
    assert output.artifact is not None
    assert output.artifact.summary == "Source fact"
    assert output.evidence_bindings == ()
    assert output.suggested_next_action is not None
    assert output.suggested_next_action.requires_human


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

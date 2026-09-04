"""Policy test that prevents paid AI from authoring source truth."""

import pytest
from fastapi import HTTPException

from agent_registry import AgentCode
from bedrock_provider import BEDROCK_MODE
from contracts import ProviderPolicy
from inventory_semantic_service import SOURCE_TRANSCRIPTION
from runtime_execution import execute_agent
from test_inventory_semantic_agent import request


def test_live_source_transcription_never_calls_bedrock(monkeypatch) -> None:
    called = False

    def fail_provider(*_args, **_kwargs):
        nonlocal called
        called = True
        raise AssertionError("Bedrock must not receive source transcription.")

    monkeypatch.setattr(
        "runtime_execution.generate_with_bedrock",
        fail_provider,
    )
    policy = ProviderPolicy(
        provider="bedrock",
        model="us.amazon.nova-pro-v1:0",
        temperature=0,
        timeout_seconds=30,
        max_attempts=1,
        cost_cap_minor=6,
        allow_live=True,
    )
    value = request(SOURCE_TRANSCRIPTION)
    value = value.model_copy(update={
        "invocation": value.invocation.model_copy(update={
            "provider_policy": policy,
        }),
    })

    with pytest.raises(HTTPException) as error:
        execute_agent(
            AgentCode.INVENTORY_INTELLIGENCE,
            value.model_dump_json().encode(),
            BEDROCK_MODE,
        )

    assert error.value.status_code == 503
    assert "deterministic extraction" in str(error.value.detail)
    assert called is False

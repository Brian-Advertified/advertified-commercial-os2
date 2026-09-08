"""Policy tests for the approved Nova Lite source-transcription boundary."""

from agent_registry import AgentCode
from bedrock_provider import BEDROCK_MODE
from contracts import ProviderPolicy
from inventory_semantic_service import SOURCE_TRANSCRIPTION, propose_semantic_extraction
from runtime_execution import execute_agent
from test_inventory_semantic_agent import request


def test_live_source_transcription_uses_approved_bedrock_route(monkeypatch) -> None:
    monkeypatch.setenv("ADVERTIFIED_INVENTORY_PROCESSING_PAUSED", "false")
    called = False

    def approved_provider(_agent_code, value, _artifact_type, instruction, **_kwargs):
        nonlocal called
        called = True
        assert value.operation == SOURCE_TRANSCRIPTION
        assert "Transcribe only supplier facts" in instruction
        return propose_semantic_extraction(value)

    monkeypatch.setattr(
        "runtime_execution.generate_with_bedrock",
        approved_provider,
    )
    policy = ProviderPolicy(
        provider="bedrock",
        model="amazon.nova-lite-v1:0",
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

    result = execute_agent(
        AgentCode.INVENTORY_INTELLIGENCE,
        value.model_dump_json().encode(),
        BEDROCK_MODE,
    )

    assert result["status"] == "REVIEW_REQUIRED"
    assert called is True

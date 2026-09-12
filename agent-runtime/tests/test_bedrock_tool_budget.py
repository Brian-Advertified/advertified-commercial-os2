"""Structured output schema costs must be bounded before paid inference."""

import json
from decimal import Decimal
from types import SimpleNamespace

import pytest

import bedrock_provider
from agent_registry import AgentCode
from bedrock_failure import BedrockProviderError
from bedrock_multimodal import conservative_input_token_estimate
from bedrock_provider import BedrockPricing, _invoke_bedrock
from bedrock_schema import structured_output_tool
from test_provider import unsupported_token_count_error


class CountingClient:
    def __init__(self, supported=True):
        self.supported = supported
        self.count_request = None
        self.converse_request = None

    def count_tokens(self, **request):
        self.count_request = request
        if not self.supported:
            raise unsupported_token_count_error()
        # A deterministic fake charges the actual supplied tool payload.
        return {"inputTokens": len(json.dumps(request["input"]["converse"]["toolConfig"]))}

    def converse(self, **request):
        self.converse_request = request
        return {}


def invoke(monkeypatch, client, schema, cap):
    monkeypatch.setenv(bedrock_provider.MAX_TOKENS_KEY, "4096")
    messages = [{"role": "user", "content": [{"text": "supplied input"}]}]
    monkeypatch.setattr(bedrock_provider, "_request_context", lambda *_: (client, messages))
    return _invoke_bedrock(AgentCode.AUDIENCE_INTELLIGENCE, None, "Propose grounded candidates", schema,
        SimpleNamespace(run_id="run", step_id="step"),
        SimpleNamespace(model="fixture", cost_cap_minor=cap),
        BedrockPricing(input_per_million_usd=Decimal(1), output_per_million_usd=Decimal(1)), None)


def test_count_tokens_receives_identical_tool_configuration_to_inference(monkeypatch):
    client = CountingClient()
    schema = json.dumps({"type": "object", "description": "structured evidence " * 400})
    invoke(monkeypatch, client, schema, 1)
    counted = client.count_request["input"]["converse"]
    actual = client.converse_request
    assert counted["toolConfig"] == actual["toolConfig"] == structured_output_tool(schema)
    assert counted["messages"] == actual["messages"]
    assert counted["system"] == actual["system"]
    assert actual["inferenceConfig"]["maxTokens"] < 4096


@pytest.mark.parametrize("supported", [True, False])
def test_oversized_schema_blocks_before_inference_even_when_counting_unsupported(monkeypatch, supported):
    client = CountingClient(supported)
    schema = json.dumps({"type": "object", "description": "evidence " * 10_000})
    with pytest.raises(BedrockProviderError, match="below the input estimate"):
        invoke(monkeypatch, client, schema, 1)
    assert client.converse_request is None


def test_fallback_reserves_serialized_tool_configuration_and_utf8_bytes():
    system = [{"text": "🧭" * 20}]
    messages = [{"role": "user", "content": [{"text": "é" * 20}]}]
    tool = structured_output_tool('{"type":"object","description":"📍 branch"}')
    without = conservative_input_token_estimate(system, messages)
    with_tool = conservative_input_token_estimate(system, messages, tool)
    assert with_tool - without == len(json.dumps(tool, ensure_ascii=True).encode("utf-8"))
    assert without >= len(("🧭" * 20 + "é" * 20).encode("utf-8"))

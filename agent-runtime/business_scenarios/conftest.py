"""Every live business-scenario provider call needs a durable canonical reservation."""

import json

import pytest

import bedrock_provider
from business_scenarios.live_budget_permit import claim_live_call, retain_provider_receipt


@pytest.fixture(autouse=True)
def enforce_shared_live_budget(monkeypatch):
    dispatch = bedrock_provider._converse

    def reserved_dispatch(client, agent_code, invocation, model, tool_config, system, messages, max_tokens):
        claim = claim_live_call(invocation, model)
        claim.with_suffix(".input.json").write_text(json.dumps({
            "agentCode": agent_code.value, "model": model, "maxTokens": max_tokens,
            "toolConfig": tool_config, "system": system, "messages": messages,
        }, indent=2) + "\n", encoding="utf-8")
        try:
            response = dispatch(
                client, agent_code, invocation, model, tool_config, system, messages, max_tokens
            )
        except Exception as error:
            claim.with_suffix(".failure.json").write_text(json.dumps({
                "errorType": type(error).__name__, "reservationReleased": False,
                "usage": None,
            }) + "\n", encoding="utf-8")
            raise
        retain_provider_receipt(claim, response)
        return response

    monkeypatch.setattr(bedrock_provider, "_converse", reserved_dispatch)

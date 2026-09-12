"""Shared live Bedrock business-scenario support.

This module contains only provider setup and invocation construction so business scenarios
stay focused on commercial behavior rather than duplicating infrastructure boilerplate.
"""

from __future__ import annotations

import json
import os
from uuid import UUID

from agent_registry import AgentCode
from contracts import (
    AgentInvocationEnvelope,
    ProviderPolicy,
    ResourceReference,
    ResumeContext,
    ToolPolicy,
)

DEFAULT_MODEL = "amazon.nova-lite-v1:0"
PRICING = {
    "amazon.nova-lite-v1:0": ("0.06", "0.24"),
    "amazon.nova-pro-v1:0": ("0.8", "3.2"),
    "amazon.nova-2-lite-v1:0": ("0.30", "2.50"),
    "global.anthropic.claude-sonnet-5": ("3.0", "15.0"),
    "global.anthropic.claude-sonnet-4-6": ("3.0", "15.0"),
    "global.openai.gpt-5.6-sol": ("4.0", "20.0"),
}


def configure_live_bedrock(
    model: str | None = None,
    *,
    max_output_tokens: int = 8192,
    cost_cap_minor: int | None = None,
) -> str:
    if model is None:
        model = os.environ.get("ADVERTIFIED_BUSINESS_SCENARIO_MODEL", DEFAULT_MODEL)
    if model not in PRICING:
        raise ValueError(f"No governed business-scenario pricing is configured for {model}.")
    if max_output_tokens < 128 or max_output_tokens > 8192:
        raise ValueError("Business-scenario output token budget must be between 128 and 8192.")
    if cost_cap_minor is None:
        cost_cap_minor = int(os.environ.get("ADVERTIFIED_BUSINESS_SCENARIO_COST_CAP_MINOR", "2"))
    if cost_cap_minor < 1:
        raise ValueError("Business-scenario cost cap must be positive.")
    input_price, output_price = PRICING[model]
    os.environ["ADVERTIFIED_AGENT_RUNTIME_MODE"] = "bedrock"
    os.environ["ADVERTIFIED_BEDROCK_REGION"] = "us-east-1"
    os.environ["ADVERTIFIED_BEDROCK_MODEL_ALLOWLIST"] = model
    os.environ["ADVERTIFIED_BEDROCK_MULTIMODAL_MODEL_ALLOWLIST"] = model
    os.environ["ADVERTIFIED_BEDROCK_PRICING_JSON"] = json.dumps({model: {
        "input_per_million_usd": input_price,
        "output_per_million_usd": output_price,
    }})
    os.environ["ADVERTIFIED_BEDROCK_MAX_TOKENS"] = str(max_output_tokens)
    os.environ["ADVERTIFIED_BUSINESS_SCENARIO_COST_CAP_MINOR"] = str(cost_cap_minor)
    return model


def invocation(
    *,
    agent_code: AgentCode,
    tenant_id: UUID,
    actor_id: UUID,
    brief_version_id: UUID,
    run_id: UUID,
    approved_evidence_item_ids: tuple[UUID, ...] = (),
    additional_resource_refs: tuple[ResourceReference, ...] = (),
) -> AgentInvocationEnvelope:
    return AgentInvocationEnvelope(
        schema_version="1.0.0",
        tenant_id=tenant_id,
        actor_id=actor_id,
        effective_role="agency_admin",
        run_id=run_id,
        step_id=run_id,
        correlation_id=run_id,
        agent_code=agent_code,
        contract_version="1.0.0",
        prompt_version="1.0.0",
        resource_refs=(
            ResourceReference(
                resource_type="BriefVersion",
                resource_id=brief_version_id,
                version=1,
            ),
            *additional_resource_refs,
        ),
        approved_evidence_item_ids=approved_evidence_item_ids,
        locale="en-ZA",
        account_policy_version="1.0.0",
        tool_policy=ToolPolicy(
            allowed_tools=(),
            max_tool_calls=0,
            consequence_policy="PROPOSE_ONLY",
        ),
        provider_policy=ProviderPolicy(
            provider="bedrock",
            model=os.environ.get("ADVERTIFIED_BEDROCK_MODEL_ALLOWLIST", DEFAULT_MODEL),
            temperature=0,
            timeout_seconds=120,
            max_attempts=1,
            cost_cap_minor=int(os.environ.get("ADVERTIFIED_BUSINESS_SCENARIO_COST_CAP_MINOR", "2")),
            allow_live=True,
        ),
        resume=ResumeContext(),
    )

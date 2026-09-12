"""Real-world Market Intelligence business scenario for the Mukuru brief.

This is intentionally a business acceptance scenario, not a unit test. It calls the live
Bedrock provider when credentials are available and prints the full governed artifact for
human commercial review.
"""

from __future__ import annotations

import json
import os
from uuid import UUID

from agent_registry import AgentCode
from bedrock_provider import BEDROCK_MODE
from contracts import (
    AgentInvocationEnvelope,
    ProviderPolicy,
    ResourceReference,
    ResumeContext,
    ToolPolicy,
)
from market_intelligence_contracts import MarketIntelligenceAgentRequest, MarketIntelligenceContext
from runtime_execution import execute_agent

TENANT_ID = UUID("11111111-1111-1111-1111-111111111111")
ACTOR_ID = UUID("22222222-2222-2222-2222-222222222222")
BRIEF_VERSION_ID = UUID("33333333-3333-3333-3333-333333333333")
RUN_ID = UUID("44444444-4444-4444-4444-444444444444")


def _configure_bedrock() -> None:
    os.environ["ADVERTIFIED_AGENT_RUNTIME_MODE"] = "bedrock"
    os.environ["ADVERTIFIED_BEDROCK_REGION"] = "us-east-1"
    os.environ["ADVERTIFIED_BEDROCK_MODEL_ALLOWLIST"] = "amazon.nova-lite-v1:0"
    os.environ["ADVERTIFIED_BEDROCK_MULTIMODAL_MODEL_ALLOWLIST"] = "amazon.nova-lite-v1:0"
    os.environ["ADVERTIFIED_BEDROCK_PRICING_JSON"] = (
        '{"amazon.nova-lite-v1:0":{"input_per_million_usd":"0.06",'
        '"output_per_million_usd":"0.24"}}'
    )
    os.environ["ADVERTIFIED_BEDROCK_MAX_TOKENS"] = "2048"


def _invocation() -> AgentInvocationEnvelope:
    return AgentInvocationEnvelope(
        schema_version="1.0.0", tenant_id=TENANT_ID, actor_id=ACTOR_ID,
        effective_role="agency_admin", run_id=RUN_ID, step_id=RUN_ID,
        correlation_id=RUN_ID, agent_code=AgentCode.MARKET_INTELLIGENCE,
        contract_version="1.0.0", prompt_version="1.0.0",
        resource_refs=(ResourceReference(
            resource_type="BriefVersion", resource_id=BRIEF_VERSION_ID, version=1,
        ),),
        approved_evidence_item_ids=(), locale="en-ZA", account_policy_version="1.0.0",
        tool_policy=ToolPolicy(
            allowed_tools=(), max_tool_calls=0, consequence_policy="PROPOSE_ONLY",
        ),
        provider_policy=ProviderPolicy(
            provider="bedrock", model="amazon.nova-lite-v1:0", temperature=0,
            timeout_seconds=120, max_attempts=1, cost_cap_minor=5, allow_live=True,
        ),
        resume=ResumeContext(),
    )


def _request() -> MarketIntelligenceAgentRequest:
    return MarketIntelligenceAgentRequest(
        invocation=_invocation(),
        market=MarketIntelligenceContext(
            brief_version_id=BRIEF_VERSION_ID, client_name="Mukuru",
            business_problem=(
                "Mukuru wants suitable out-of-home media opportunities across South Africa to reach "
                "customers who send money to Bangladesh, India and Pakistan. Recommendations should "
                "focus on areas and communities with relevant population concentrations and proximity "
                "to mosques and temples."
            ),
            objective=(
                "Identify defensible OOH opportunity areas and investment approaches that can reach "
                "customers sending money to Bangladesh, India and Pakistan."
            ),
            audiences=(
                "Customers in South Africa who send money to Bangladesh",
                "Customers in South Africa who send money to India",
                "Customers in South Africa who send money to Pakistan",
            ),
            geographies=("South Africa",),
            constraints=(
                "Campaign runs October to December.", "Billboards must be considered.",
                "Wall murals must be considered.", "Other relevant OOH formats may be recommended.",
                "Prioritise relevant communities and proximity to mosques and temples.",
                "No confirmed budget; recommend a range of investment levels.",
            ),
            success_measures=(
                "Produce commercially useful OOH recommendations grounded in defensible audience/location evidence.",
            ),
            budget_minor=None, currency="ZAR",
        ),
    )


def test_mukuru_market_intelligence_live() -> None:
    _configure_bedrock()
    request = _request()
    output = execute_agent(
        AgentCode.MARKET_INTELLIGENCE,
        request.model_dump_json().encode("utf-8"),
        BEDROCK_MODE,
    )
    artifact = output.get("artifact")
    assert isinstance(artifact, dict)
    rendered = json.dumps(artifact, indent=2)
    print("\nMUKURU MARKET INTELLIGENCE ARTIFACT\n" + rendered)
    usage = output.get("usage") or {}
    print(
        f"\nBEDROCK USAGE input={usage.get('input_tokens', 0)} output={usage.get('output_tokens', 0)} "
        f"cost_usd_micros={usage.get('incremental_cost_usd_micros', 0)}"
    )
    text = rendered.lower()
    assert artifact["findings"] == []
    assert artifact["opportunities"] == []
    assert "no verified market evidence is supplied" in text
    assert "evidence" in text
    assert "market is vast" not in text
    assert "demand is strong" not in text

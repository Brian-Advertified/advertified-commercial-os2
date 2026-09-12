"""Live Mukuru Audience Intelligence business acceptance scenario.

This scenario uses the real production audience bootstrap observations and the exact
observation-id derivation used by the local kernel loader. It is deliberately a commercial
acceptance check, not a synthetic unit fixture.
"""

from __future__ import annotations

import hashlib
import json
import os
import uuid
from pathlib import Path
from uuid import UUID

from agent_registry import AgentCode
from reference_evidence import ReferenceObservation
from bedrock_provider import BEDROCK_MODE
from contracts import (
    AgentInvocationEnvelope,
    ProviderPolicy,
    ResourceReference,
    ResumeContext,
    ToolPolicy,
)
from planning_contracts import AudienceAgentRequest, PlanningBriefContext
from runtime_execution import execute_agent

ROOT = Path(__file__).resolve().parents[2]
BOOTSTRAP = ROOT / "data" / "production" / "audience-bootstrap.v1.json"
NAMESPACE = uuid.UUID("51ce24bd-848e-4852-a8f2-eaf0a2e4aa49")
TENANT_ID = UUID("11111111-1111-1111-1111-111111111111")
ACTOR_ID = UUID("22222222-2222-2222-2222-222222222222")
BRIEF_VERSION_ID = UUID("33333333-3333-3333-3333-333333333333")
RUN_ID = UUID("55555555-5555-5555-5555-555555555555")


def canonical(value: object) -> str:
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"))


def selected_reference_observations() -> tuple[ReferenceObservation, ...]:
    payload = json.loads(BOOTSTRAP.read_text(encoding="utf-8"))
    source_by_key = {item["key"]: item for item in payload["sources"]}
    observations = []
    for item in payload["observations"]:
        dimensions = item["dimensions"]
        if dimensions.get("group") not in {"POPULATION_GROUP", "TOTAL_POPULATION"}:
            continue
        if dimensions.get("group") == "POPULATION_GROUP" and dimensions.get("segment") != "Indian/Asian":
            continue
        if item["geography"]["code"] not in {"ZA", "GP", "KZN"}:
            continue
        source = source_by_key[item["source"]]
        source_observations = sorted(
            (candidate for candidate in payload["observations"] if candidate.get("source") == source["key"]),
            key=lambda candidate: candidate["sourceLocator"],
        )
        fingerprint = hashlib.sha256(
            canonical({"source": source, "observations": source_observations}).encode()
        ).hexdigest()
        source_id = str(uuid.uuid5(NAMESPACE, f"source:{source['key']}:{fingerprint}"))
        metric_code, metric_value, metric_unit = (
            ("AUDIENCE_COUNT", item.get("audienceCount"), "PEOPLE")
            if item.get("audienceCount") is not None
            else ("SHARE_PERCENT", item.get("sharePercent"), "PERCENT")
        )
        observation_id = uuid.uuid5(
            NAMESPACE,
            f"observation:{source_id}:{item['sourceLocator']}:{metric_code}",
        )
        sensitive = item["activationPolicy"] == "SENSITIVE_CONTEXT_ONLY"
        observations.append(ReferenceObservation(
            observation_id=observation_id,
            source_title=source["title"],
            measurement_period=source["measurementPeriod"],
            geography_level=item["geography"]["level"],
            geography_code=item["geography"]["code"],
            geography_name=item["geography"]["name"],
            dimensions={str(key): str(value) for key, value in dimensions.items()},
            metric_code=metric_code,
            metric_value=float(metric_value),
            metric_unit=metric_unit,
            stability_code=item["stability"],
            sensitivity_code="SENSITIVE_CONTEXT" if sensitive else "STANDARD",
            activation_policy=item["activationPolicy"],
            evidence_notes=tuple(item.get("evidenceNotes", [])),
        ))
    return tuple(observations)


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


def _live_invocation() -> AgentInvocationEnvelope:
    return AgentInvocationEnvelope(
        schema_version="1.0.0", tenant_id=TENANT_ID, actor_id=ACTOR_ID,
        effective_role="agency_admin", run_id=RUN_ID, step_id=RUN_ID,
        correlation_id=RUN_ID, agent_code=AgentCode.AUDIENCE_INTELLIGENCE,
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


def _mukuru_request(references) -> AudienceAgentRequest:
    return AudienceAgentRequest(
        invocation=_live_invocation(),
        planning=PlanningBriefContext(
            brief_version_id=BRIEF_VERSION_ID, client_name="Mukuru",
            business_problem=(
                "Mukuru needs OOH recommendations across South Africa to reach customers who send money "
                "to Bangladesh, India and Pakistan, focusing on relevant communities and proximity to "
                "mosques and temples."
            ),
            objective=(
                "Identify defensible OOH opportunity areas and investment approaches for customers "
                "in South Africa who send money to Bangladesh, India and Pakistan."
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
                "Produce commercially useful OOH recommendations grounded in defensible audience and location evidence.",
            ),
            budget_minor=None, currency="ZAR", audience_evidence=(), reference_evidence=references,
        ),
    )


def _assert_mukuru_artifact(artifact) -> None:
    text = json.dumps(artifact).lower()
    for required in ("bangladesh", "india", "pakistan"):
        assert required in text
    for segment in artifact["audiences"]:
        assert segment["classification"] == "CLIENT_REQUIREMENT"
        assert segment["evidence_item_ids"] == []
        assert segment["reference_observation_ids"] == []
        assert segment["confidence"] is None
        assert segment["need_state"] is None
        assert segment["buying_context"] is None
        assert segment["language"] is None
        assert segment["life_stage"] is None
        assert segment["lsm_sem"] is None
    assert "indian/asian" not in " ".join(segment["name"].lower() for segment in artifact["audiences"])


def test_mukuru_audience_intelligence_live() -> None:
    _configure_bedrock()
    references = selected_reference_observations()
    assert references
    request = _mukuru_request(references)
    raw = execute_agent(
        AgentCode.AUDIENCE_INTELLIGENCE,
        request.model_dump_json().encode("utf-8"),
        BEDROCK_MODE,
    )
    print("\nMUKURU AUDIENCE INTELLIGENCE OUTPUT\n" + json.dumps(raw, indent=2, default=str))
    artifact = raw["artifact"]
    assert artifact is not None
    _assert_mukuru_artifact(artifact)

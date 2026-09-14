"""Approved audience classification must survive the Media Strategy input boundary."""
import json
import re

import pytest
from pydantic import ValidationError

from business_scenarios.test_media_strategy_intelligence_live import _mukuru_context
from media_strategy_contracts import MediaStrategyContext


@pytest.mark.parametrize("classification", ["CLIENT_REQUIREMENT", "INFERENCE", "HYPOTHESIS"])
def test_media_strategy_retains_approved_audience_classification(classification):
    payload = _mukuru_context().model_dump(mode="json")
    payload["target_audiences"][0]["classification"] = classification
    parsed = MediaStrategyContext.model_validate_json(json.dumps(payload))
    assert parsed.target_audiences[0].classification == classification


def test_media_strategy_rejects_invented_audience_classification():
    payload = _mukuru_context().model_dump(mode="json")
    payload["target_audiences"][0]["classification"] = "REMITTANCE_SENDERS"
    with pytest.raises(ValidationError):
        MediaStrategyContext.model_validate_json(json.dumps(payload))


def media_request():
    from business_scenarios.bedrock_support import invocation
    from business_scenarios.test_media_strategy_intelligence_live import (
        ACTOR_ID, TENANT_ID, _id,
    )
    from agent_registry import AgentCode
    from contracts import ResourceReference
    from media_strategy_contracts import MediaStrategyRequest
    context = _mukuru_context()
    request = MediaStrategyRequest(
        operation="MEDIA_STRATEGY_ANALYSIS", media_strategy=context,
        invocation=invocation(
            agent_code=AgentCode.MEDIA_STRATEGY, tenant_id=TENANT_ID, actor_id=ACTOR_ID,
            brief_version_id=context.brief_version_id, run_id=_id("schema", "run"),
            additional_resource_refs=(ResourceReference(
                resource_type="IntelligenceArtifact", resource_id=context.audience_artifact_id,
                version=context.audience_artifact_version,
            ),),
        ),
    )
    return request


def test_provider_schema_binds_channel_codes_labels_and_budgetless_output():
    from bedrock_media_schema import media_strategy_schema
    request = media_request()
    context = request.media_strategy
    schema = json.loads(media_strategy_schema(request))
    fields = schema["$defs"]["MediaStrategyProviderRecommendation"]["properties"]
    assert fields["channel"]["enum"] == ["OOH", "DOOH"]
    assert "DOOH: Digital screens" in fields["channel"]["description"]
    assert fields["budget_guidance_percent"] == {"type": "null"}
    assert "rationale" not in fields
    assert "objective_contribution" not in fields
    assert request.media_strategy == context
    funded = request.model_copy(update={"media_strategy": context.model_copy(update={"budget_minor": 32000000, "currency": "ZAR"})})
    funded_schema = json.loads(media_strategy_schema(funded))["$defs"]["MediaStrategyProviderRecommendation"]
    assert funded_schema["properties"]["budget_guidance_percent"]["type"] == "number"
    assert "budget_guidance_percent" in funded_schema["required"]
    assert request.media_strategy == context


def test_unsupported_audience_presence_in_role_is_rebuilt_as_safe_communication_task():
    from media_strategy_contracts import MediaChannelRecommendation
    from media_strategy_service import (
        unavailable_media_strategy, canonicalize_media_strategy, validate_media_strategy_grounding,
    )
    request = media_request()
    output = unavailable_media_strategy(request)
    channel = MediaChannelRecommendation(
        channel="OOH", role="Reach customers in high-traffic areas near places of worship.",
        rationale="Untrusted inference.", objective_contribution="Untrusted contribution.",
        classification="HYPOTHESIS",
        evidence_gaps=("High-traffic performance evidence is missing.",),
    )
    output = output.model_copy(update={"artifact": output.artifact.model_copy(update={
        "channel_recommendations": (channel,),
        "evidence_gaps": ("High-traffic delivery evidence is missing.",),
    })})
    canonical = canonicalize_media_strategy(request, output)
    validate_media_strategy_grounding(request, canonical)
    retained = canonical.artifact.channel_recommendations[0]
    assert "high-traffic" not in retained.role.casefold()
    assert "high-traffic" not in retained.objective_contribution.casefold()
    assert all("high-traffic" not in gap.casefold() for gap in retained.evidence_gaps)
    assert all("high-traffic" not in gap.casefold() for gap in canonical.artifact.evidence_gaps)


def test_compact_media_strategy_provider_output_expands_without_inventing_channel_decisions():
    from bedrock_artifact_output import wrap_artifact_output
    from media_strategy_contracts import MediaStrategyArtifact
    request = media_request()
    payload = {
        "channel_recommendations": [{
            "channel": "OOH",
            "role": "Present the approved campaign message and invite the stated action.",
            "classification": "HYPOTHESIS",
            "budget_guidance_percent": None,
        }],
        "excluded_channels": ["DOOH"],
    }
    output = wrap_artifact_output(MediaStrategyArtifact, payload, request)
    assert tuple(item.channel for item in output.artifact.channel_recommendations) == ("OOH",)
    assert output.artifact.excluded_channels == ("DOOH",)
    assert output.artifact.strategic_principles == ()
    assert output.artifact.evidence_gaps == ()


def test_unreadable_media_strategy_core_recommendations_are_still_rejected():
    from bedrock_artifact_output import wrap_artifact_output
    from media_strategy_contracts import MediaStrategyArtifact
    request = media_request()
    payload = {
        "channel_recommendations": '[{"channel":"OOH"}, invalid]',
        "excluded_channels": [],
    }
    with pytest.raises(ValidationError):
        wrap_artifact_output(MediaStrategyArtifact, payload, request)


def test_unavailable_media_strategy_retains_blocking_review_and_valid_fixture_usage():
    from media_strategy_service import unavailable_media_strategy, validate_media_strategy_grounding
    request = media_request()
    output = unavailable_media_strategy(request)
    validate_media_strategy_grounding(request, output)
    assert output.artifact.channel_recommendations == ()
    assert output.unknowns[0].is_blocking
    assert output.suggested_next_action.requires_human
    assert output.usage.model == "fixture-v1"
    assert output.usage.incremental_cost_usd_micros == 0


def test_deterministic_media_strategy_respects_explicit_channels_and_reconciles_budget():
    from decimal import Decimal
    from media_strategy_service import (
        canonicalize_media_strategy,
        deterministic_media_strategy,
        validate_media_strategy_grounding,
    )
    request = media_request()
    context = request.media_strategy.model_copy(update={
        "budget_minor": 10_000_000,
        "currency": "ZAR",
        "media_requirements": ("OOH and DOOH only.",),
    })
    funded = request.model_copy(update={"media_strategy": context})
    output = canonicalize_media_strategy(funded, deterministic_media_strategy(funded))
    validate_media_strategy_grounding(funded, output)
    assert tuple(item.channel for item in output.artifact.channel_recommendations) == ("OOH", "DOOH")
    assert sum(
        item.budget_guidance_percent for item in output.artifact.channel_recommendations
    ) == Decimal("100")
    assert all(item.classification == "HYPOTHESIS" for item in output.artifact.channel_recommendations)
    assert output.unknowns == ()


def test_deterministic_media_strategy_budgetless_output_has_no_percentages():
    from media_strategy_service import (
        canonicalize_media_strategy,
        deterministic_media_strategy,
        validate_media_strategy_grounding,
    )
    request = media_request()
    output = canonicalize_media_strategy(request, deterministic_media_strategy(request))
    validate_media_strategy_grounding(request, output)
    assert output.artifact.channel_recommendations
    assert all(
        item.budget_guidance_percent is None
        for item in output.artifact.channel_recommendations
    )

"""Approved audience classification must survive the Media Strategy input boundary."""
import json

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
    fields = schema["$defs"]["MediaChannelRecommendation"]["properties"]
    assert fields["channel"]["enum"] == ["OOH", "DOOH"]
    assert "DOOH: Digital screens" in fields["channel"]["description"]
    assert fields["budget_guidance_percent"] == {"type": "null"}
    assert request.media_strategy == context


def test_unsupported_audience_presence_in_role_is_still_rejected():
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
    )
    output = output.model_copy(update={"artifact": output.artifact.model_copy(update={
        "channel_recommendations": (channel,),
    })})
    canonical = canonicalize_media_strategy(request, output)
    with pytest.raises(ValueError, match="unsupported performance"):
        validate_media_strategy_grounding(request, canonical)


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

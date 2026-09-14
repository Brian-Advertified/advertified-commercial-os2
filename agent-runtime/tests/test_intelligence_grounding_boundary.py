"""Regressions at the real provider/output boundary; no live provider is used."""

import json
from copy import deepcopy
from uuid import UUID

import pytest
from fastapi import HTTPException
from pydantic import ValidationError

import runtime_execution
from agent_registry import AgentCode
from bedrock_artifact_output import wrap_artifact_output
from planning_contracts import AudienceAgentRequest, AudienceDefinitionSetArtifact
from planning_service import canonicalize_audiences, propose_audiences
from test_planning_agents import payload

FOREIGN_ID = "908b1d6f-c705-4d26-8437-a3c38d221c65"


def audience_request():
    body = payload("audience_intelligence")
    body["planning"].update({
        "client_name": "Unseen industrial supplier",
        "audiences": ["Replacement-part procurement teams"],
        "geographies": ["Unseen commercial district"],
    })
    return AudienceAgentRequest.model_validate_json(json.dumps(body))


def provider_audience_payload(request):
    artifact = propose_audiences(request).artifact
    return {
        "audiences": [
            {
                "name": item.name,
                "need_state": item.need_state,
                "buying_context": item.buying_context,
                "evidence_item_ids": [str(value) for value in item.evidence_item_ids],
                "reference_observation_ids": [str(value) for value in item.reference_observation_ids],
                "confidence": item.confidence,
                "is_target": item.is_target,
            }
            for item in artifact.audiences
        ],
        "targeting_rationale": artifact.targeting_rationale,
        "positioning_statement": artifact.positioning_statement,
    }


@pytest.mark.parametrize("field,value", [
    ("evidence_item_ids", [FOREIGN_ID]),
    ("reference_observation_ids", [FOREIGN_ID]),
])
def test_untrusted_audience_references_are_not_silently_repaired(field, value):
    request = audience_request()
    artifact = provider_audience_payload(request)
    artifact["audiences"][0][field] = value
    with pytest.raises(ValueError):
        output = wrap_artifact_output(AudienceDefinitionSetArtifact, artifact, request)
        canonicalize_audiences(request, output)


def test_provider_cannot_expand_audience_geography_outside_brief_scope():
    request = audience_request()
    output = propose_audiences(request)
    audience = output.artifact.audiences[0].model_copy(update={
        "geographies": ("Outside the approved campaign",),
    })
    invalid = output.model_copy(update={"artifact": output.artifact.model_copy(update={
        "audiences": (audience,),
    })})
    with pytest.raises(ValueError, match="geography outside"):
        canonicalize_audiences(request, invalid)


@pytest.mark.parametrize("invalid", ["missing_required_field", "unknown_field"])
def test_raw_artifact_schema_is_checked_before_any_normalization(invalid):
    request = audience_request()
    artifact = provider_audience_payload(request)
    if invalid == "missing_required_field":
        del artifact["audiences"][0]["name"]
    else:
        artifact["approve_without_human"] = True
    with pytest.raises(ValidationError):
        wrap_artifact_output(AudienceDefinitionSetArtifact, artifact, request)


def test_schema_declared_json_array_string_is_normalized_before_strict_validation():
    request = audience_request()
    artifact = provider_audience_payload(request)
    artifact["audiences"] = json.dumps(artifact["audiences"])
    output = wrap_artifact_output(AudienceDefinitionSetArtifact, artifact, request)
    assert len(output.artifact.audiences) == 1
    assert output.artifact.audiences[0].name == "Replacement-part procurement teams"


def test_python_literal_array_string_is_normalized_before_strict_validation():
    request = audience_request()
    artifact = provider_audience_payload(request)
    artifact["audiences"] = repr(artifact["audiences"])
    output = wrap_artifact_output(AudienceDefinitionSetArtifact, artifact, request)
    assert len(output.artifact.audiences) == 1
    assert output.artifact.audiences[0].name == "Replacement-part procurement teams"


def test_json_array_string_with_raw_control_character_is_safely_normalized():
    request = audience_request()
    artifact = provider_audience_payload(request)
    artifact["audiences"][0]["need_state"] = "Hypothesis: line one\nline two"
    encoded = json.dumps(artifact["audiences"])
    artifact["audiences"] = encoded.replace("\\n", "\n")
    output = wrap_artifact_output(AudienceDefinitionSetArtifact, artifact, request)
    assert output.artifact.audiences[0].need_state == "Hypothesis: line one\nline two"


def test_yaml_compatible_jsonish_array_string_is_normalized_before_strict_validation():
    request = audience_request()
    artifact = provider_audience_payload(request)
    artifact["audiences"] = json.dumps(artifact["audiences"]).replace('"', "'")
    output = wrap_artifact_output(AudienceDefinitionSetArtifact, artifact, request)
    assert len(output.artifact.audiences) == 1
    assert output.artifact.audiences[0].name == "Replacement-part procurement teams"


def test_non_json_array_string_is_not_coerced():
    request = audience_request()
    artifact = provider_audience_payload(request)
    artifact["audiences"] = "not-an-array"
    with pytest.raises(ValidationError):
        wrap_artifact_output(AudienceDefinitionSetArtifact, artifact, request)


def test_omitted_brief_audience_is_restored_from_canonical_request():
    request = audience_request()
    output = propose_audiences(request)
    provider_output = output.model_copy(update={"artifact": output.artifact.model_copy(update={
        "audiences": (),
    })})
    repaired = canonicalize_audiences(request, provider_output)
    assert tuple(item.name for item in repaired.artifact.audiences) == (
        "Replacement-part procurement teams",
    )
    assert repaired.artifact.audiences[0].is_target is True


def test_duplicate_audience_is_rejected_before_canonicalization():
    request = audience_request()
    output = propose_audiences(request)
    invalid = output.model_copy(update={"artifact": output.artifact.model_copy(update={
        "audiences": output.artifact.audiences * 2,
    })})
    with pytest.raises(ValueError):
        canonicalize_audiences(request, invalid)


def test_normalization_rejection_retains_provider_receipt_without_logging_payload(monkeypatch, caplog):
    request = audience_request()
    output = propose_audiences(request)
    output = output.model_copy(update={"usage": output.usage.model_copy(update={
        "provider": "bedrock", "model": "synthetic-model", "incremental_cost_minor": 3,
    })})
    original = deepcopy(output.model_dump(mode="json", exclude={"usage"}))
    calls = []

    def provider(*args, **kwargs):
        calls.append(args[0])
        return output

    def reject(*args):
        raise ValueError("untrusted-private-payload-must-not-enter-logs")

    monkeypatch.setattr(runtime_execution, "generate_with_bedrock", provider)
    monkeypatch.setattr(runtime_execution, "canonicalize_audiences", reject)
    with pytest.raises(HTTPException) as failure:
        runtime_execution._grounded_bedrock_output(
            AgentCode.AUDIENCE_INTELLIGENCE, request, AudienceDefinitionSetArtifact, "test")
    assert failure.value.status_code == 503
    detail = failure.value.detail
    assert detail["code"] == "BEDROCK_RESULT_REJECTED"
    assert detail["stage"] == "GROUNDING_VALIDATION"
    assert detail["provider_acceptance"] == "ACCEPTED"
    assert detail["usage"]["incremental_cost_minor"] == 3
    assert detail["rejected_output"] == original
    assert calls == [AgentCode.AUDIENCE_INTELLIGENCE]
    assert "untrusted-private-payload" not in caplog.text


def test_unknown_reference_cannot_disappear_before_rejection_receipt(monkeypatch):
    request = audience_request()
    output = propose_audiences(request)
    audience = output.artifact.audiences[0].model_copy(update={
        "evidence_item_ids": (UUID(FOREIGN_ID),),
    })
    output = output.model_copy(update={"artifact": output.artifact.model_copy(update={
        "audiences": (audience,),
    })})
    monkeypatch.setattr(runtime_execution, "generate_with_bedrock", lambda *a, **k: output)
    with pytest.raises(HTTPException) as failure:
        runtime_execution._grounded_bedrock_output(
            AgentCode.AUDIENCE_INTELLIGENCE, request, AudienceDefinitionSetArtifact, "test")
    retained = failure.value.detail["rejected_output"]["artifact"]["audiences"][0]
    assert retained["evidence_item_ids"] == [FOREIGN_ID]

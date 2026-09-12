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


@pytest.mark.parametrize("field,value", [
    ("evidence_item_ids", [FOREIGN_ID]),
    ("reference_observation_ids", [FOREIGN_ID]),
    ("geographies", ["Outside the approved campaign"]),
])
def test_untrusted_audience_references_are_not_silently_repaired(field, value):
    request = audience_request()
    artifact = propose_audiences(request).artifact.model_dump(mode="json")
    artifact["audiences"][0][field] = value
    with pytest.raises(ValueError):
        output = wrap_artifact_output(AudienceDefinitionSetArtifact, artifact, request)
        canonicalize_audiences(request, output)


@pytest.mark.parametrize("invalid", ["missing_required_field", "unknown_field"])
def test_raw_artifact_schema_is_checked_before_any_normalization(invalid):
    request = audience_request()
    artifact = propose_audiences(request).artifact.model_dump(mode="json")
    if invalid == "missing_required_field":
        del artifact["audiences"][0]["geographies"]
    else:
        artifact["approve_without_human"] = True
    with pytest.raises(ValidationError):
        wrap_artifact_output(AudienceDefinitionSetArtifact, artifact, request)


@pytest.mark.parametrize("case", ["omitted_supplied_audience", "duplicate_audience"])
def test_required_audiences_and_unique_identity_survive_provider_boundary(case):
    request = audience_request()
    output = propose_audiences(request)
    audiences = () if case == "omitted_supplied_audience" else output.artifact.audiences * 2
    invalid = output.model_copy(update={"artifact": output.artifact.model_copy(update={
        "audiences": audiences,
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

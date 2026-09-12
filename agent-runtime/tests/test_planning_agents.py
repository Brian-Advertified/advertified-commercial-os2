import asyncio
import json
from copy import deepcopy

import httpx
import pytest

from bedrock_artifact_output import artifact_schema, wrap_artifact_output
from main import DETERMINISTIC_MODE, RUNTIME_MODE_KEY, SERVICE_KEY, app
from planning_contracts import (
    AudienceAgentRequest,
    AudienceDefinition,
    AudienceDefinitionSetArtifact,
)
from planning_service import canonicalize_audiences, propose_audiences

SERVICE_SECRET = "planning-test-service-key"
BRIEF_ID = "66666666-6666-6666-6666-666666666666"
EVIDENCE_ID = "77777777-7777-7777-7777-777777777777"


def invocation(agent_code: str) -> dict:
    return {
        "schema_version": "1.0.0",
        "tenant_id": "11111111-1111-1111-1111-111111111111",
        "actor_id": "22222222-2222-2222-2222-222222222222",
        "effective_role": "agent_runtime_service",
        "run_id": "33333333-3333-3333-3333-333333333333",
        "step_id": "44444444-4444-4444-4444-444444444444",
        "correlation_id": "55555555-5555-5555-5555-555555555555",
        "agent_code": agent_code,
        "contract_version": "1.0.0",
        "prompt_version": "1.0.0",
        "resource_refs": [{
            "resource_type": "BriefVersion",
            "resource_id": BRIEF_ID,
            "version": 3,
        }],
        "approved_evidence_item_ids": [EVIDENCE_ID],
        "locale": "en-ZA",
        "account_policy_version": "1.0.0",
        "tool_policy": {
            "allowed_tools": [],
            "max_tool_calls": 0,
            "consequence_policy": "PROPOSE_ONLY",
        },
        "provider_policy": {
            "provider": "deterministic",
            "model": "fixture-v1",
            "temperature": 0,
            "timeout_seconds": 30,
            "max_attempts": 1,
            "cost_cap_minor": 0,
            "allow_live": False,
        },
        "resume": {
            "checkpoint_id": None,
            "prior_validated_output_ref": None,
            "prior_usage_ref": None,
        },
    }


def planning() -> dict:
    return {
        "brief_version_id": BRIEF_ID,
        "client_name": "Test Furniture Client",
        "business_problem": "The client needs more qualified furniture enquiries.",
        "objective": "Increase qualified furniture enquiries",
        "audiences": ["Small business furniture buyers"],
        "geographies": ["Gauteng"],
    }


def payload(agent_code: str) -> dict:
    return {"invocation": invocation(agent_code), "planning": planning()}


async def post(agent_code: str, body: dict) -> httpx.Response:
    transport = httpx.ASGITransport(app=app)
    headers = {"X-Advertified-Service-Key": SERVICE_SECRET}
    async with httpx.AsyncClient(transport=transport, base_url="http://test") as client:
        return await client.post(f"/v1/agents/{agent_code}", json=body, headers=headers)


def enable(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv(RUNTIME_MODE_KEY, DETERMINISTIC_MODE)
    monkeypatch.setenv(SERVICE_KEY, SERVICE_SECRET)


def test_audience_contract_normalizes_only_literal_null_serialization() -> None:
    raw = {
        "name": "Furniture shoppers",
        "description": "Audience hypothesis supplied by the provider.",
        "need_state": " null ",
        "buying_context": "NULL",
        "geographies": ("Gauteng",),
        "language": "null",
        "life_stage": "null",
        "lsm_sem": "null",
        "lsm_sem_taxonomy": "null",
        "lsm_sem_taxonomy_version": "null",
        "classification": "HYPOTHESIS",
        "exclusions": (),
        "evidence_item_ids": (),
        "reference_observation_ids": (),
        "confidence": "null",
        "is_target": True,
    }

    audience = AudienceDefinition.model_validate(raw)
    assert audience.need_state is None
    assert audience.buying_context is None
    assert audience.language is None
    assert audience.life_stage is None
    assert audience.lsm_sem is None
    assert audience.lsm_sem_taxonomy is None
    assert audience.lsm_sem_taxonomy_version is None
    assert audience.confidence is None

    preserved = AudienceDefinition.model_validate({
        **raw,
        "need_state": "N/A",
        "buying_context": "none",
        "confidence": None,
    })
    assert preserved.need_state == "N/A"
    assert preserved.buying_context == "none"

    numeric_wire = AudienceDefinition.model_validate_json(json.dumps({
        **raw,
        "confidence": 0.7,
    }))
    assert str(numeric_wire.confidence) == "0.7"


def test_audience_proposal_does_not_globally_bind_brief_evidence(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    enable(monkeypatch)
    response = asyncio.run(post("audience_intelligence", payload("audience_intelligence")))

    assert response.status_code == 200, response.text
    output = response.json()
    audiences = output["artifact"]["audiences"]
    audience = audiences[0]
    assert [item["name"] for item in audiences] == planning()["audiences"]
    assert audience["classification"] == "CLIENT_REQUIREMENT"
    assert audience["evidence_item_ids"] == []
    assert audience["confidence"] is None
    assert audience["language"] is None
    assert audience["life_stage"] is None
    assert audience["lsm_sem"] is None
    assert output["usage"]["incremental_cost_minor"] == 0
    assert output["usage"]["tool_calls"] == 0


@pytest.mark.parametrize("names", [
    ["Stay-at-home mothers"],
    ["Affluent consumers"],
    ["Parents", "Caregivers", "Teachers", "Parents"],
    [f"Supplied audience {index}" for index in range(8)],
])
def test_deterministic_audience_path_preserves_supplied_audiences_without_fabrication(names) -> None:
    body = payload("audience_intelligence")
    body["planning"]["audiences"] = names
    request = AudienceAgentRequest.model_validate_json(json.dumps(body))

    output = canonicalize_audiences(request, propose_audiences(request))

    assert [item.name for item in output.artifact.audiences] == list(dict.fromkeys(names))
    assert all(item.language is None and item.life_stage is None
               and item.lsm_sem is None for item in output.artifact.audiences)
    assert all(item.buying_context is None for item in output.artifact.audiences)
    assert all(item.need_state is None for item in output.artifact.audiences)
    assert all(item.confidence is None for item in output.artifact.audiences)
    assert {item.field_path for item in output.unknowns} == {
        "artifact.audiences.need_state",
        "artifact.audiences.buying_context",
        "artifact.positioning_statement",
        "artifact.audiences.media_evidence",
        "artifact.audiences.structured_evidence",
    }
    assert canonicalize_audiences(request, output).unknowns == output.unknowns


def test_bedrock_audience_boundary_accepts_only_artifact_and_adds_evidence_binding() -> None:
    request = AudienceAgentRequest.model_validate_json(
        json.dumps(payload("audience_intelligence"))
    )
    deterministic = propose_audiences(request)
    schema = json.loads(artifact_schema(AudienceDefinitionSetArtifact))

    assert "audiences" in schema["properties"]
    assert "artifact" not in schema["properties"]

    provider_artifact = deterministic.artifact.model_dump(mode="json")
    provider_artifact["audiences"][0].update({
        "classification": "HYPOTHESIS",
        "geographies": list(request.planning.geographies),
        "language": "Invented",
        "evidence_item_ids": [],
        "confidence": 0.7,
        "is_target": False,
    })
    output = wrap_artifact_output(
        AudienceDefinitionSetArtifact,
        provider_artifact,
        request,
    )

    output = canonicalize_audiences(request, output)
    audience = output.artifact.audiences[0]
    assert audience.classification == "CLIENT_REQUIREMENT"
    assert audience.geographies == request.planning.geographies
    assert audience.language is None
    assert audience.evidence_item_ids == ()
    assert audience.confidence is None
    assert audience.is_target is True
    assert output.status == "COMPLETED"
    assert output.evidence_bindings == ()


def test_audience_canonicalizer_removes_unsupported_structured_facts() -> None:
    request = AudienceAgentRequest.model_validate_json(
        json.dumps(payload("audience_intelligence"))
    )
    provider_output = propose_audiences(request)
    audience = provider_output.artifact.audiences[0].model_copy(update={
        "geographies": tuple(value.upper() for value in request.planning.geographies),
        "language": "Invented",
        "life_stage": "Invented",
        "lsm_sem": "10",
        "lsm_sem_taxonomy": None,
        "classification": "FACT",
        "evidence_item_ids": (),
        "is_target": False,
    })
    provider_output = provider_output.model_copy(update={
        "artifact": provider_output.artifact.model_copy(update={
            "audiences": (audience,),
        }),
    })

    output = canonicalize_audiences(request, provider_output)
    repaired = output.artifact.audiences[0]

    assert repaired.geographies == request.planning.geographies
    assert repaired.language is None
    assert repaired.life_stage is None
    assert repaired.lsm_sem is None
    assert repaired.classification == "CLIENT_REQUIREMENT"
    assert repaired.evidence_item_ids == ()
    assert repaired.confidence is None
    assert repaired.is_target is True


def test_planning_contract_rejects_route_mismatch_and_unknown_fields(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    enable(monkeypatch)
    mismatch = payload("audience_intelligence")
    mismatch["invocation"]["agent_code"] = "media_strategy"
    mismatch_response = asyncio.run(post("audience_intelligence", mismatch))
    assert mismatch_response.status_code == 400

    malformed = deepcopy(payload("audience_intelligence"))
    malformed["planning"]["invented_reach"] = 1_000_000
    malformed_response = asyncio.run(post("audience_intelligence", malformed))
    assert malformed_response.status_code == 422


def test_planning_contract_requires_exact_brief_resource(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    enable(monkeypatch)
    body = payload("audience_intelligence")
    body["invocation"]["resource_refs"][0]["resource_id"] = (
        "99999999-9999-9999-9999-999999999999"
    )

    response = asyncio.run(post("audience_intelligence", body))

    assert response.status_code == 422

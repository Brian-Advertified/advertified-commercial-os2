import asyncio
import json
from copy import deepcopy

import httpx
import pytest

from bedrock_artifact_output import artifact_schema, wrap_artifact_output
from main import DETERMINISTIC_MODE, RUNTIME_MODE_KEY, SERVICE_KEY, app
from planning_contracts import (
    AudienceAgentRequest,
    AudienceDefinitionSetArtifact,
    MediaMixDraftArtifact,
    MediaPlanningAgentRequest,
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
        "objective": "Increase qualified furniture enquiries",
        "audiences": ["Small business furniture buyers"],
        "geographies": ["Gauteng"],
    }


def payload(agent_code: str) -> dict:
    context = planning()
    if agent_code == "media_planning":
        context |= {
            "budget_minor": 10_000_01,
            "currency": "ZAR",
            "available_channels": ["RADIO", "OOH", "DIGITAL", "OOH"],
        }
    return {"invocation": invocation(agent_code), "planning": context}


async def post(agent_code: str, body: dict) -> httpx.Response:
    transport = httpx.ASGITransport(app=app)
    headers = {"X-Advertified-Service-Key": SERVICE_SECRET}
    async with httpx.AsyncClient(transport=transport, base_url="http://test") as client:
        return await client.post(f"/v1/agents/{agent_code}", json=body, headers=headers)


def enable(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv(RUNTIME_MODE_KEY, DETERMINISTIC_MODE)
    monkeypatch.setenv(SERVICE_KEY, SERVICE_SECRET)


def test_audience_proposal_is_evidence_bound_without_sensitive_inference(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    enable(monkeypatch)
    response = asyncio.run(post("audience", payload("audience")))

    assert response.status_code == 200, response.text
    output = response.json()
    audiences = output["artifact"]["audiences"]
    audience = audiences[0]
    assert [item["name"] for item in audiences] == planning()["audiences"]
    assert audience["classification"] == "INFERENCE"
    assert audience["evidence_item_ids"] == [EVIDENCE_ID]
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
def test_fallback_preserves_supplied_audiences_without_fabricated_discovery(names) -> None:
    body = payload("audience")
    body["planning"]["audiences"] = names
    request = AudienceAgentRequest.model_validate_json(json.dumps(body))

    output = canonicalize_audiences(request, propose_audiences(request))

    assert [item.name for item in output.artifact.audiences] == list(dict.fromkeys(names))
    assert all(item.language is None and item.life_stage is None
               and item.lsm_sem is None for item in output.artifact.audiences)
    assert all("intent are not supplied" in item.buying_context
               for item in output.artifact.audiences)
    assert all(item.need_state != request.planning.objective
               for item in output.artifact.audiences)
    assert {item.field_path for item in output.unknowns} == {
        "artifact.audiences.buying_context",
        "artifact.audiences.media_evidence",
        "artifact.audiences.structured_evidence",
    }
    assert canonicalize_audiences(request, output).unknowns == output.unknowns


def test_media_mix_uses_allowed_channels_and_exact_budget(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    enable(monkeypatch)
    response = asyncio.run(post("media_planning", payload("media_planning")))

    assert response.status_code == 200, response.text
    allocations = response.json()["artifact"]["allocations"]
    assert {item["channel"] for item in allocations} <= {"RADIO", "OOH", "DIGITAL"}
    assert len({item["channel"] for item in allocations}) == len(allocations)
    assert sum(item["budget_minor"] for item in allocations) == 10_000_01


def test_bedrock_media_mix_reconciles_provider_weights_to_exact_budget() -> None:
    request = MediaPlanningAgentRequest.model_validate_json(
        json.dumps(payload("media_planning"))
    )
    provider_artifact = {
        "allocations": [
            {"channel": "DIGITAL", "budget_minor": 400_000,
             "role": "Primary response channel"},
            {"channel": "OOH", "budget_minor": 200_000,
             "role": "Physical awareness"},
            {"channel": "RADIO", "budget_minor": 299_999,
             "role": "Broad frequency"},
        ],
        "assumptions": ["Human review required."],
    }

    output = wrap_artifact_output(
        MediaMixDraftArtifact,
        provider_artifact,
        request,
    )

    allocations = output.artifact.allocations
    assert sum(item.budget_minor for item in allocations) == 10_000_01
    assert all(item.budget_minor > 0 for item in allocations)
    assert [item.channel for item in allocations] == [
        "DIGITAL", "OOH", "RADIO",
    ]


def test_bedrock_audience_boundary_accepts_only_artifact_and_adds_evidence_binding() -> None:
    request = AudienceAgentRequest.model_validate_json(
        json.dumps(payload("audience"))
    )
    deterministic = propose_audiences(request)
    schema = json.loads(artifact_schema(AudienceDefinitionSetArtifact))

    assert "audiences" in schema["properties"]
    assert "artifact" not in schema["properties"]

    provider_artifact = deterministic.artifact.model_dump(mode="json")
    provider_artifact["audiences"][0].update({
        "classification": "Primary Audience",
        "geographies": ["Mars"],
        "language": "Invented",
        "evidence_item_ids": [],
        "is_target": False,
    })
    output = wrap_artifact_output(
        AudienceDefinitionSetArtifact,
        provider_artifact,
        request,
    )

    audience = output.artifact.audiences[0]
    assert audience.classification == "INFERENCE"
    assert audience.geographies == request.planning.geographies
    assert audience.language is None
    assert audience.evidence_item_ids == (
        request.invocation.approved_evidence_item_ids
    )
    assert audience.is_target is False
    assert output.status == "COMPLETED"
    assert output.evidence_bindings[0].field_path == "artifact.audiences"
    assert output.evidence_bindings[0].evidence_item_ids == (
        request.invocation.approved_evidence_item_ids
    )


def test_audience_canonicalizer_removes_unsupported_structured_facts() -> None:
    request = AudienceAgentRequest.model_validate_json(
        json.dumps(payload("audience"))
    )
    provider_output = propose_audiences(request)
    audience = provider_output.artifact.audiences[0].model_copy(update={
        "geographies": ("Mars",),
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
    assert repaired.classification == "INFERENCE"
    assert repaired.evidence_item_ids == request.invocation.approved_evidence_item_ids
    assert repaired.is_target is False


def test_planning_contract_rejects_route_mismatch_and_unknown_fields(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    enable(monkeypatch)
    mismatch = payload("media_planning")
    mismatch["invocation"]["agent_code"] = "audience"
    mismatch_response = asyncio.run(post("media_planning", mismatch))
    assert mismatch_response.status_code == 400

    malformed = deepcopy(payload("audience"))
    malformed["planning"]["invented_reach"] = 1_000_000
    malformed_response = asyncio.run(post("audience", malformed))
    assert malformed_response.status_code == 422


def test_planning_contract_requires_exact_brief_resource(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    enable(monkeypatch)
    body = payload("audience")
    body["invocation"]["resource_refs"][0]["resource_id"] = (
        "99999999-9999-9999-9999-999999999999"
    )

    response = asyncio.run(post("audience", body))

    assert response.status_code == 422

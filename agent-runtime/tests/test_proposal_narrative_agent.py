import asyncio
from copy import deepcopy
import json

import httpx
import pytest
from test_planning_agents import BRIEF_ID, EVIDENCE_ID, invocation

from bedrock_artifact_output import wrap_artifact_output
from main import DETERMINISTIC_MODE, RUNTIME_MODE_KEY, SERVICE_KEY, app
from proposal_contracts import (
    ProposalNarrativeAgentRequest,
    ProposalNarrativeDraftArtifact,
)

SERVICE_SECRET = "proposal-test-service-key"


def payload() -> dict:
    request_invocation = invocation("proposal_narrative")
    request_invocation["resource_refs"].append({
        "resource_type": "MediaPlanVersion",
        "resource_id": "88888888-8888-8888-8888-888888888888",
        "version": 4,
    })
    return {
        "invocation": request_invocation,
        "proposal": {
            "brief_version_id": BRIEF_ID,
            "brief_business_problem": "Qualified furniture enquiries have declined",
            "brief_objective": "Increase qualified furniture enquiries",
            "success_measures": ["Qualified enquiries", "Showroom visits"],
            "options": [{
                "plan_version_id": "88888888-8888-8888-8888-888888888888",
                "plan_version": 4,
                "label": "Launch",
                "outcome": "Build qualified response",
                "budget_minor": 10_000_01,
                "currency": "ZAR",
                "channels": ["OOH", "DOOH", "DIGITAL"],
            }],
        },
    }


async def post(body: dict) -> httpx.Response:
    transport = httpx.ASGITransport(app=app)
    headers = {"X-Advertified-Service-Key": SERVICE_SECRET}
    async with httpx.AsyncClient(transport=transport, base_url="http://test") as client:
        return await client.post(
            "/v1/agents/proposal_narrative", json=body, headers=headers
        )


def enable(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv(RUNTIME_MODE_KEY, DETERMINISTIC_MODE)
    monkeypatch.setenv(SERVICE_KEY, SERVICE_SECRET)


def test_proposal_narrative_preserves_exact_supplied_commercial_facts(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    enable(monkeypatch)
    response = asyncio.run(post(payload()))

    assert response.status_code == 200, response.text
    output = response.json()
    summary = output["artifact"]["executive_summary"]
    assert "qualified furniture enquiries have declined" in summary.lower()
    assert "increase qualified furniture enquiries" in summary.lower()
    assert "Qualified enquiries; Showroom visits" in summary
    assert "ZAR 10,000.01" in summary
    assert "Outdoor advertising, Digital screens, DIGITAL" in summary
    assert "OOH" not in summary
    assert output["evidence_bindings"][0]["evidence_item_ids"] == [EVIDENCE_ID]
    assert output["usage"]["incremental_cost_minor"] == 0
    assert output["usage"]["tool_calls"] == 0


def test_bedrock_proposal_boundary_preserves_governed_facts() -> None:
    request = ProposalNarrativeAgentRequest.model_validate_json(
        json.dumps(payload())
    )
    provider_artifact = {
        "executive_summary": (
            "A persuasive provider narrative that paraphrases every supplied fact."
        )
    }

    output = wrap_artifact_output(
        ProposalNarrativeDraftArtifact,
        provider_artifact,
        request,
    )

    summary = output.artifact.executive_summary
    assert provider_artifact["executive_summary"] in summary
    assert f"Approved business problem: {request.proposal.brief_business_problem}" in summary
    assert f"Approved objective: {request.proposal.brief_objective}" in summary
    assert "Approved success measures:\nQualified enquiries\nShowroom visits" in summary
    assert (
        "Launch: Build qualified response | ZAR 10,000.01 | "
        "channels: Outdoor advertising, Digital screens, DIGITAL"
    ) in summary
    assert request.proposal.options[0].channels == ("OOH", "DOOH", "DIGITAL")
    assert len(summary) <= 5_000


def test_proposal_contract_rejects_unknown_or_mismatched_input(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    enable(monkeypatch)
    malformed = deepcopy(payload())
    malformed["proposal"]["options"][0]["discount_minor"] = 50_000
    malformed_response = asyncio.run(post(malformed))
    assert malformed_response.status_code == 422

    mismatch = deepcopy(payload())
    mismatch["invocation"]["agent_code"] = "media_strategy"
    mismatch_response = asyncio.run(post(mismatch))
    assert mismatch_response.status_code == 400


def test_proposal_contract_requires_exact_plan_resource(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    enable(monkeypatch)
    body = payload()
    body["invocation"]["resource_refs"][1]["version"] = 3

    response = asyncio.run(post(body))

    assert response.status_code == 422

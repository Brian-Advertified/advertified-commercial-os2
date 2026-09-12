"""A correct appended budget must not launder an incorrect provider amount."""
import json

import pytest
from fastapi import HTTPException

import runtime_execution
from agent_registry import AgentCode
from bedrock_artifact_output import wrap_artifact_output
from proposal_contracts import ProposalNarrativeAgentRequest, ProposalNarrativeDraftArtifact
from proposal_provider_facts import proposal_model_input, validate_proposal_numeric_claims
from proposal_service import propose_narrative
from test_proposal_narrative_agent import payload


def request():
    return ProposalNarrativeAgentRequest.model_validate_json(json.dumps(payload()))


@pytest.mark.parametrize("text", [
    "This option costs 1,000,001 ZAR.",
    "This campaign guarantees 25% growth.",
    "This option costs USD 10,000.01.",
])
def test_unapproved_numeric_claim_is_rejected_despite_correct_appended_facts(text):
    body = request()
    output = wrap_artifact_output(ProposalNarrativeDraftArtifact, {"executive_summary": text}, body)
    assert "ZAR 10,000.01" in output.artifact.executive_summary
    with pytest.raises(ValueError, match="numeric claim absent"):
        validate_proposal_numeric_claims(body, output)


def test_provider_sees_display_money_without_minor_units_or_internal_ids():
    body = request()
    original = body.model_dump_json()
    projection = proposal_model_input(body)
    option = projection["proposal"]["options"][0]
    assert option["approved_budget"] == "ZAR 10,000.01"
    assert "budget_minor" not in option and "plan_version_id" not in option
    assert str(body.proposal.brief_version_id) not in json.dumps(projection)
    assert body.model_dump_json() == original
    validate_proposal_numeric_claims(body, propose_narrative(body))


def test_numeric_rejection_retains_actual_provider_usage(monkeypatch):
    body = request()
    output = propose_narrative(body)
    output = output.model_copy(update={
        "artifact": ProposalNarrativeDraftArtifact(executive_summary="Budget: 1,000,001 ZAR."),
        "usage": output.usage.model_copy(update={
            "provider": "bedrock", "model": "synthetic-model", "incremental_cost_minor": 1,
        }),
    })
    monkeypatch.setattr(runtime_execution, "generate_with_bedrock", lambda *a, **k: output)
    with pytest.raises(HTTPException) as failure:
        runtime_execution._grounded_bedrock_output(
            AgentCode.PROPOSAL_NARRATIVE, body, ProposalNarrativeDraftArtifact, "test")
    assert failure.value.detail["stage"] == "GROUNDING_VALIDATION"
    assert failure.value.detail["usage"]["incremental_cost_minor"] == 1

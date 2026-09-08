from copy import deepcopy
import json

import pytest

from planning_contracts import AudienceAgentRequest
from planning_service import canonicalize_audiences, propose_audiences
from test_planning_agents import invocation, planning, EVIDENCE_ID, BRIEF_ID


def request_with_evidence(*, direct=False):
    context = planning()
    context["audiences"] = ["Stay-at-home mothers"]
    context["audience_evidence"] = [{
        "evidence_item_id": None if direct else EVIDENCE_ID,
        "brief_version_id": BRIEF_ID if direct else None,
        "audience_name": "Stay-at-home mothers", "language": "English",
        "life_stage": "Parents of preschool children", "lsm_sem": "8",
        "lsm_sem_taxonomy": "Synthetic SEM", "lsm_sem_taxonomy_version": "fixture-1",
        "need_state": "Compare the supplied product's refill cost.",
        "buying_context": "A weekly household replenishment purchase.",
        "message_context": "Clear unit price comparison", "moment_context": "Before the weekly shop",
    }]
    payload = {"invocation": invocation("audience"), "planning": context}
    if direct:
        payload["invocation"]["approved_evidence_item_ids"] = []
    return payload


@pytest.mark.parametrize("direct", [False, True])
def test_approved_research_reaches_structured_audience_and_provider_cannot_change_it(direct):
    request = AudienceAgentRequest.model_validate_json(json.dumps(request_with_evidence(direct=direct)))
    result = propose_audiences(request)
    audience = result.artifact.audiences[0]
    assert len(result.artifact.audiences) == 1
    assert audience.language == "English"
    assert audience.life_stage == "Parents of preschool children"
    assert audience.lsm_sem_taxonomy_version == "fixture-1"
    assert audience.need_state == "Compare the supplied product's refill cost."
    assert "Before the weekly shop" in audience.buying_context
    changed = result.model_copy(update={"artifact": result.artifact.model_copy(update={
        "audiences": (audience.model_copy(update={"language": "Invented"}),)})})
    assert canonicalize_audiences(request, changed).artifact.audiences[0].language == "English"


@pytest.mark.parametrize("change", ["unapproved", "other_audience", "conflict", "incomplete_taxonomy"])
def test_unbound_conflicting_or_incomplete_research_never_becomes_a_match(change):
    payload = request_with_evidence()
    fact = payload["planning"]["audience_evidence"][0]
    if change == "unapproved":
        payload["invocation"]["approved_evidence_item_ids"] = []
    elif change == "other_audience":
        fact["audience_name"] = "Other audience"
    elif change == "conflict":
        other = deepcopy(fact)
        other["language"] = "Conflicting language"
        payload["planning"]["audience_evidence"].append(other)
    else:
        fact["lsm_sem_taxonomy_version"] = None
    result = propose_audiences(AudienceAgentRequest.model_validate_json(json.dumps(payload))).artifact.audiences[0]
    if change == "incomplete_taxonomy":
        assert result.lsm_sem is None
    else:
        assert result.language is None

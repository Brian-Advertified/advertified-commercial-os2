"""Market reasoning must consume actual approved evidence, never IDs alone."""

import json
from copy import deepcopy

import pytest

from bedrock_provider import _model_payload
from market_intelligence_contracts import MarketIntelligenceAgentRequest
from market_intelligence_service import (
    canonicalize_market_intelligence,
    propose_market_intelligence,
    validate_market_grounding,
)
from test_planning_agents import EVIDENCE_ID, invocation, planning


def request_body(with_evidence=False):
    envelope = invocation("market_intelligence")
    envelope["approved_evidence_item_ids"] = [EVIDENCE_ID] if with_evidence else []
    return {"invocation": envelope, "market": planning()}


def parse(body):
    return MarketIntelligenceAgentRequest.model_validate_json(json.dumps(body))


def test_empty_market_evidence_returns_a_valid_honest_result_without_fake_confidence():
    request = parse(request_body())
    output = propose_market_intelligence(request)
    validate_market_grounding(request, output)
    assert output.confidence == ()
    assert output.artifact.opportunities == ()
    assert output.artifact.evidence_gaps
    assert all(item.confidence is None for item in output.artifact.findings)
    assert output.usage.incremental_cost_minor == 0


def test_market_without_evidence_canonicalizes_provider_context_to_explicit_unknown():
    request = parse(request_body())
    baseline = propose_market_intelligence(request)
    provider_output = baseline.model_copy(update={
        "artifact": baseline.artifact.model_copy(update={
            "category_situation": "The market is vast and demand is strong.",
            "strategic_implications": ("Exploit the strong demand immediately.",),
            "evidence_gaps": ("Additional research is required.",),
        }),
    })

    output = canonicalize_market_intelligence(request, provider_output)
    validate_market_grounding(request, output)
    assert "No verified market evidence is supplied." in output.artifact.category_situation
    assert "strong demand" not in output.artifact.category_situation.casefold()
    assert output.artifact.evidence_gaps[0] == "No verified market evidence is supplied."
    assert all("strong demand" not in item.casefold() for item in output.artifact.strategic_implications)


def test_evidence_identifiers_alone_cannot_authorise_a_market_fact():
    request = parse(request_body(with_evidence=True))
    baseline = propose_market_intelligence(request)
    from market_intelligence_contracts import MarketFinding
    finding = MarketFinding.model_validate_json(json.dumps({
        "title": "Unsupported market fact",
        "finding": "Verified demand grew by 50 percent.",
        "commercial_implication": "Treat growth as proven.",
        "classification": "FACT", "evidence_item_ids": [EVIDENCE_ID], "confidence": 0.8,
    }))
    output = baseline.model_copy(update={
        "artifact": baseline.artifact.model_copy(update={"findings": (finding,)}),
    })
    with pytest.raises(ValueError):
        validate_market_grounding(request, output)


def approved_evidence():
    return {"id": EVIDENCE_ID, "claim_type": "BUSINESS_CONTEXT",
            "structured_value": {"statement": "Audited category sales grew by 2 percent."},
            "excerpt": "Audited category sales grew by 2 percent."}


def test_model_packet_keeps_approved_content_but_not_the_internal_invocation():
    body = request_body(with_evidence=True)
    body["approved_evidence"] = [approved_evidence()]
    payload = _model_payload(parse(body), None)
    assert "invocation" not in payload
    assert payload["approved_evidence"] == body["approved_evidence"]
    assert payload["market"]["business_problem"] == body["market"]["business_problem"]


@pytest.mark.parametrize("defect", ["foreign", "duplicate"])
def test_market_request_rejects_foreign_or_duplicate_evidence_before_provider(defect):
    body = request_body(with_evidence=True)
    body["approved_evidence"] = [approved_evidence()]
    if defect == "foreign":
        body["approved_evidence"][0]["id"] = "99999999-9999-9999-9999-999999999999"
    else:
        body["approved_evidence"].append(deepcopy(body["approved_evidence"][0]))
    with pytest.raises(ValueError):
        parse(body)

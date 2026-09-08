"""Advertified-owned governance envelope for Bedrock business artifacts."""

from __future__ import annotations

import json

from master_data_codes import CURRENCY_MINOR_UNIT_DIGITS
from media_presentation import channel_labels
from contracts import (
    EvidenceBinding,
    GeneratedAgentOutput,
    OutputStatus,
    SuggestedNextAction,
)


_EVIDENCE_FIELDS = {
    "AudienceDefinitionSetArtifact": "artifact.audiences",
    "OpportunityAngleSetArtifact": "artifact.angles",
}


def artifact_schema(artifact_type) -> str:
    """Expose only the business artifact to the model."""
    return json.dumps(
        artifact_type.model_json_schema(),
        separators=(",", ":"),
    )


def _canonical_payload(artifact_type, payload, request):
    if artifact_type.__name__ == "AudienceDefinitionSetArtifact":
        return _canonical_audience_payload(payload, request)
    if artifact_type.__name__ == "MediaMixDraftArtifact":
        return _canonical_media_mix_payload(payload, request)
    if artifact_type.__name__ == "ProposalNarrativeDraftArtifact":
        return _canonical_proposal_payload(payload, request)
    return payload


def _canonical_audience_payload(payload, request):
    if not isinstance(payload, dict) or not isinstance(payload.get("audiences"), list):
        return payload
    result = json.loads(json.dumps(payload))
    approved = [
        str(value)
        for value in request.invocation.approved_evidence_item_ids
    ]
    classification = "INFERENCE" if approved else "HYPOTHESIS"
    geographies = list(request.planning.geographies)
    for item in result["audiences"]:
        if not isinstance(item, dict):
            continue
        item.update({
            "geographies": geographies,
            "language": None,
            "life_stage": None,
            "lsm_sem": None,
            "lsm_sem_taxonomy": None,
            "lsm_sem_taxonomy_version": None,
            "lsm_sem_mandatory": False,
            "classification": classification,
            "evidence_item_ids": approved,
        })
    return result


def _canonical_media_mix_payload(payload, request):
    if not isinstance(payload, dict) or not isinstance(
        payload.get("allocations"), list
    ):
        return payload
    allocations = payload["allocations"]
    budgets = [
        item.get("budget_minor")
        for item in allocations
        if isinstance(item, dict)
    ]
    if len(budgets) != len(allocations) or any(
        not isinstance(value, int) or isinstance(value, bool) or value < 0
        for value in budgets
    ):
        return payload
    reconciled = _reconciled_budgets(
        budgets,
        request.planning.budget_minor,
    )
    if reconciled is None:
        return payload
    result = json.loads(json.dumps(payload))
    for item, budget in zip(result["allocations"], reconciled, strict=True):
        item["budget_minor"] = budget
    return result


def _reconciled_budgets(budgets: list[int], target: int):
    if not budgets or target < len(budgets):
        return None
    weight = sum(budgets)
    if weight <= 0:
        return None
    distributable = target - len(budgets)
    allocations = [
        1 + (budget * distributable // weight)
        for budget in budgets
    ]
    outstanding = target - sum(allocations)
    remainders = sorted(
        range(len(budgets)),
        key=lambda index: (
            budgets[index] * distributable % weight,
            -index,
        ),
        reverse=True,
    )
    for index in remainders[:outstanding]:
        allocations[index] += 1
    return allocations


def _canonical_proposal_payload(payload, request):
    if not isinstance(payload, dict):
        return payload
    narrative = payload.get("executive_summary")
    if not isinstance(narrative, str):
        return payload

    governed_facts = [
        f"Approved objective: {request.proposal.brief_objective}",
        "Approved options:",
        *(
            f"{option.label}: {option.outcome} | "
            f"{_money(option.budget_minor, option.currency)} | "
            f"channels: {channel_labels(option.channels)}"
            for option in request.proposal.options
        ),
    ]
    exact_facts = "\n".join(governed_facts)
    available = 5_000 - len(exact_facts) - 2
    if available < 0:
        return {"executive_summary": exact_facts}
    provider_narrative = narrative.strip()[:available]
    executive_summary = (
        f"{provider_narrative}\n\n{exact_facts}"
        if provider_narrative
        else exact_facts
    )
    return {"executive_summary": executive_summary}


def _money(amount_minor: int, currency: str) -> str:
    digits = CURRENCY_MINOR_UNIT_DIGITS[currency]
    scale = 10 ** digits
    major, minor = divmod(amount_minor, scale)
    formatted_major = f"{major:,}"
    return (
        f"{currency} {formatted_major}"
        if minor == 0 or digits == 0
        else f"{currency} {formatted_major}.{minor:0{digits}d}"
    )


def wrap_artifact_output(artifact_type, payload, request):
    """Validate the model artifact and add deterministic governance metadata."""
    if (
        isinstance(payload, dict)
        and set(payload) == {"artifact"}
        and isinstance(payload["artifact"], dict)
    ):
        payload = payload["artifact"]
    payload = _canonical_payload(artifact_type, payload, request)
    artifact = artifact_type.model_validate_json(
        json.dumps(payload, separators=(",", ":")),
    )
    invocation = request.invocation
    evidence_field = _EVIDENCE_FIELDS.get(artifact_type.__name__)
    bindings = (
        (
            EvidenceBinding(
                field_path=evidence_field,
                evidence_item_ids=invocation.approved_evidence_item_ids,
            ),
        )
        if evidence_field and invocation.approved_evidence_item_ids
        else ()
    )
    return GeneratedAgentOutput[artifact_type](
        schema_version="1.0.0",
        status=OutputStatus.COMPLETED,
        artifact=artifact,
        evidence_bindings=bindings,
        unknowns=(),
        assumptions=(),
        confidence=(),
        objections=(),
        rationale=(
            "Advertified validated the provider artifact against the typed "
            "contract and retained it for governed workflow review."
        ),
        suggested_next_action=SuggestedNextAction(
            command_code="ReviewGeneratedArtifact",
            requires_human=True,
        ),
    )

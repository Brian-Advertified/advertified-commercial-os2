"""Advertified-owned governance envelope for Bedrock business artifacts."""

from __future__ import annotations

import json

from master_data_codes import EvidenceClassifications
from proposal_provider_facts import approved_fact_lines
from contracts import (
    EvidenceBinding,
    GeneratedAgentOutput,
    OutputStatus,
    SuggestedNextAction,
)

_EVIDENCE_FIELDS = {"OpportunityAngleSetArtifact": "artifact.angles"}


def artifact_schema(artifact_type) -> str:
    """Expose only the business artifact to the model."""
    return json.dumps(artifact_type.model_json_schema(), separators=(",", ":"))


def _canonical_payload(artifact_type, payload, request):
    if artifact_type.__name__ == "ProposalNarrativeDraftArtifact":
        return _canonical_proposal_payload(payload, request)
    if artifact_type.__name__ == "InventoryShortlistDraftArtifact":
        return _canonical_inventory_payload(payload)
    return payload


def _canonical_inventory_payload(payload):
    return {
        **payload,
        "interpretations": tuple({
            **item,
            "classification": EvidenceClassifications.AI_RECOMMENDATION.value,
        } for item in payload["interpretations"]),
    }


def _canonical_proposal_payload(payload, request):
    narrative = payload["executive_summary"]
    governed_facts = approved_fact_lines(request)
    exact_facts = "\n".join(governed_facts)
    available = 5_000 - len(exact_facts) - 2
    if available < 0:
        return {"executive_summary": exact_facts}
    provider_narrative = narrative.strip()[:available]
    executive_summary = (
        f"{provider_narrative}\n\n{exact_facts}"
        if provider_narrative else exact_facts
    )
    return {"executive_summary": executive_summary}


def wrap_artifact_output(artifact_type, payload, request):
    """Validate raw shape before normalization; operation services own grounding."""
    if (
        isinstance(payload, dict)
        and set(payload) == {"artifact"}
        and isinstance(payload["artifact"], dict)
    ):
        payload = payload["artifact"]
    # Do not fill omitted fields, discard unrecognised fields or silently erase forged
    # provenance before validation. The provider receipt is retained by the caller.
    artifact = artifact_type.model_validate_json(json.dumps(payload, separators=(",", ":")))
    canonical = _canonical_payload(artifact_type, artifact.model_dump(mode="python"), request)
    artifact = artifact_type.model_validate(canonical)
    invocation = request.invocation
    evidence_field = _EVIDENCE_FIELDS.get(artifact_type.__name__)
    bindings = (
        (EvidenceBinding(field_path=evidence_field,
                         evidence_item_ids=invocation.approved_evidence_item_ids),)
        if evidence_field and invocation.approved_evidence_item_ids else ()
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
            command_code="ReviewGeneratedArtifact", requires_human=True,
        ),
    )

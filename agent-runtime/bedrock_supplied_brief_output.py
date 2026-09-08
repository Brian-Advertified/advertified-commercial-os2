"""Wrap model-authored supplied-Brief data in Advertified-owned metadata."""
from __future__ import annotations

import json

from contracts import (
    GeneratedAgentOutput,
    OutputStatus,
    SuggestedNextAction,
)
from supplied_brief_contracts import SuppliedBriefArtifact


def supplied_brief_schema() -> str:
    return json.dumps(
        SuppliedBriefArtifact.model_json_schema(),
        separators=(",", ":"),
    )


def wrap_supplied_brief_output(
    artifact_type: type[SuppliedBriefArtifact],
    payload: object,
) -> GeneratedAgentOutput[SuppliedBriefArtifact]:
    if isinstance(payload, dict) and set(payload) == {"artifact"}:
        payload = payload["artifact"]
    artifact = artifact_type.model_validate_json(
        json.dumps(payload, separators=(",", ":")),
    )
    return GeneratedAgentOutput[artifact_type](
        schema_version="1.0.0",
        status=OutputStatus.REVIEW_REQUIRED,
        artifact=artifact,
        evidence_bindings=(),
        unknowns=(),
        assumptions=(),
        confidence=(),
        objections=(),
        rationale=(
            "Bedrock proposed a source-grounded Brief interpretation; "
            "deterministic validation and human review remain authoritative."
        ),
        suggested_next_action=SuggestedNextAction(
            command_code="ReviewSuppliedBrief",
            requires_human=True,
        ),
    )

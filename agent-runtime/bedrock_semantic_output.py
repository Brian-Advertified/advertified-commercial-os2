"""Wrap Bedrock source-processing artifacts in governed metadata."""

from __future__ import annotations

import json
from typing import TypeVar

from pydantic import BaseModel

from contracts import (
    ContractModel,
    GeneratedAgentOutput,
    OutputStatus,
    SuggestedNextAction,
)

ArtifactT = TypeVar("ArtifactT", bound=ContractModel)


def is_semantic_enrichment(request: BaseModel) -> bool:
    return getattr(request, "operation", None) in {
        "SOURCE_TRANSCRIPTION",
        "SEMANTIC_ENRICHMENT",
    }


def semantic_schema(artifact_type: type[ArtifactT]) -> str:
    return json.dumps(
        artifact_type.model_json_schema(),
        separators=(",", ":"),
    )


def wrap_semantic_output(
    artifact_type: type[ArtifactT],
    payload: object,
) -> GeneratedAgentOutput[ArtifactT]:
    artifact = artifact_type.model_validate_json(
        json.dumps(payload, separators=(",", ":")),
    )
    return governed_source_output(artifact_type, artifact)


def governed_source_output(
    artifact_type: type[ArtifactT],
    artifact: ArtifactT,
) -> GeneratedAgentOutput[ArtifactT]:
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
            "Bedrock proposed source-linked inventory processing; deterministic "
            "validation and human review remain authoritative."
        ),
        suggested_next_action=SuggestedNextAction(
            command_code="ReviewInventorySemanticExtraction",
            requires_human=True,
        ),
    )

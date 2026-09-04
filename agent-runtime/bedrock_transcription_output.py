"""Fixed-row Bedrock schema for inventory source transcription."""

from __future__ import annotations

import json
import re
from decimal import Decimal
from typing import Annotated, Literal, get_args

from pydantic import Field, model_validator

from bedrock_semantic_output import governed_source_output
from contracts import ContractModel, GeneratedAgentOutput
from inventory_semantic_contracts import (
    InventoryFieldName,
    InventorySemanticExtractionArtifact,
    ProposedInventoryCandidate,
    ProposedInventoryField,
    SourceLocator,
)

SOURCE_TRANSCRIPTION = "SOURCE_TRANSCRIPTION"
RESTRICTED_FIELDS = frozenset({
    "channel",
    "product_type",
    "description",
    "rate_type",
    "currency",
    "availability",
    "rate_valid_from",
    "rate_valid_to",
    "booking_deadline",
    "material_deadline",
})
TRANSCRIPTION_FIELDS = frozenset(get_args(InventoryFieldName)).difference(
    RESTRICTED_FIELDS
)
IDENTITY_FIELDS = frozenset({"name", "product_code"})


class BedrockTranscribedField(ContractModel):
    raw_value: Annotated[str, Field(min_length=1, max_length=2_000)]
    source_locator: SourceLocator
    transformation: Literal["TRIM", "DERIVED_FROM_SOURCE_CONTEXT"]
    confidence: Annotated[Decimal, Field(ge=0, le=1)]


class BedrockTranscriptionCandidate(ContractModel):
    source_locator: SourceLocator
    fields: Annotated[
        dict[str, BedrockTranscribedField],
        Field(min_length=1, max_length=80),
    ]
    ambiguity_notes: Annotated[
        tuple[Annotated[str, Field(min_length=1, max_length=500)], ...],
        Field(max_length=20),
    ] = ()

    @model_validator(mode="after")
    def validate_fields(self) -> BedrockTranscriptionCandidate:
        names = set(self.fields)
        if not names.issubset(TRANSCRIPTION_FIELDS):
            raise ValueError("Source transcription used an unsupported field.")
        if not names.intersection(IDENTITY_FIELDS):
            raise ValueError("Source transcription requires a row identity.")
        return self


class BedrockTranscriptionArtifact(ContractModel):
    candidates: Annotated[
        tuple[BedrockTranscriptionCandidate, ...],
        Field(max_length=200),
    ]
    omitted_source_locators: Annotated[
        tuple[SourceLocator, ...],
        Field(max_length=100),
    ] = ()


def is_source_transcription(request) -> bool:
    return getattr(request, "operation", None) == SOURCE_TRANSCRIPTION


def transcription_schema() -> str:
    schema = BedrockTranscriptionArtifact.model_json_schema()
    _bind_field_names(schema)
    return json.dumps(schema, separators=(",", ":"))


def wrap_transcription_output(
    artifact_type: type[InventorySemanticExtractionArtifact],
    payload: object,
) -> GeneratedAgentOutput[InventorySemanticExtractionArtifact]:
    proposed = BedrockTranscriptionArtifact.model_validate_json(
        json.dumps(payload, separators=(",", ":")),
    )
    artifact = artifact_type(
        candidates=tuple(
            _project_candidate(candidate)
            for candidate in proposed.candidates
            if not _is_header_candidate(candidate)
        ),
        omitted_source_locators=proposed.omitted_source_locators,
    )
    return governed_source_output(artifact_type, artifact)


def _project_candidate(
    candidate: BedrockTranscriptionCandidate,
) -> ProposedInventoryCandidate:
    return ProposedInventoryCandidate(
        source_locator=candidate.source_locator,
        fields=tuple(
            _project_field(name, value)
            for name, value in candidate.fields.items()
        ),
        ambiguity_notes=candidate.ambiguity_notes,
    )


def _project_field(
    name: str,
    value: BedrockTranscribedField,
) -> ProposedInventoryField:
    raw_value = value.raw_value
    transformation = value.transformation
    if name == "dimensions":
        match = re.fullmatch(
            r"\s*(\d+(?:[.,]\d+)?)\s+(\d+(?:[.,]\d+)?)\s*",
            raw_value,
        )
        if match:
            raw_value = f"{match.group(1)} x {match.group(2)}"
            transformation = "DERIVED_FROM_SOURCE_CONTEXT"
    return ProposedInventoryField(
        field_name=name,
        raw_value=raw_value,
        normalized_value=None,
        source_locator=value.source_locator,
        evidence_basis="SUPPLIER_SUPPLIED",
        transformation=transformation,
        confidence=value.confidence,
    )


def _is_header_candidate(
    candidate: BedrockTranscriptionCandidate,
) -> bool:
    labels = {
        name: _canonical_label(value.raw_value)
        for name, value in candidate.fields.items()
    }
    return (
        labels.get("placement") in {"adunit", "placement"}
        and labels.get("format") == "format"
        and labels.get("rate") in {"rate", "price", "cost"}
        and labels.get("dimensions") in {
            "dimensions", "widthheight", "widthxheight",
        }
    )


def _canonical_label(value: str) -> str:
    return "".join(character for character in value.casefold()
                   if character.isalnum())


def _bind_field_names(node: object) -> None:
    if isinstance(node, dict):
        properties = node.get("properties")
        if isinstance(properties, dict):
            fields = properties.get("fields")
            if isinstance(fields, dict):
                fields["propertyNames"] = {
                    "type": "string",
                    "enum": sorted(TRANSCRIPTION_FIELDS),
                }
        for value in node.values():
            _bind_field_names(value)
    elif isinstance(node, list):
        for value in node:
            _bind_field_names(value)

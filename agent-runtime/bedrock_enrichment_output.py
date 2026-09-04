"""Fixed-row Bedrock schema for semantic enrichment of existing inventory."""

from __future__ import annotations

import json
from decimal import Decimal
from typing import Annotated, Literal

from pydantic import Field, model_validator

from bedrock_semantic_output import governed_source_output
from contracts import ContractModel, GeneratedAgentOutput
from inventory_semantic_contracts import (
    InventorySemanticExtractionArtifact,
    ProposedInventoryCandidate,
    ProposedInventoryField,
    SourceLocator,
)

SEMANTIC_ENRICHMENT = "SEMANTIC_ENRICHMENT"
ENRICHMENT_FIELDS = frozenset({
    "channel",
    "product_type",
    "description",
})


class BedrockEnrichedField(ContractModel):
    raw_value: Annotated[str, Field(min_length=1, max_length=2_000)]
    normalized_value: Annotated[str, Field(min_length=1, max_length=2_000)]
    source_locator: SourceLocator
    transformation: Literal[
        "DERIVED_FROM_CHANNEL",
        "DERIVED_FROM_SOURCE_CONTEXT",
    ]
    confidence: Annotated[Decimal, Field(ge=0, le=1)]


class BedrockEnrichmentCandidate(ContractModel):
    source_locator: SourceLocator
    fields: Annotated[
        dict[str, BedrockEnrichedField],
        Field(min_length=1, max_length=3),
    ]
    ambiguity_notes: Annotated[
        tuple[Annotated[str, Field(min_length=1, max_length=500)], ...],
        Field(max_length=20),
    ] = ()

    @model_validator(mode="after")
    def validate_fields(self) -> BedrockEnrichmentCandidate:
        if not set(self.fields).issubset(ENRICHMENT_FIELDS):
            raise ValueError("Semantic enrichment used a commercial source field.")
        return self


class BedrockEnrichmentArtifact(ContractModel):
    candidates: Annotated[
        tuple[BedrockEnrichmentCandidate, ...],
        Field(max_length=200),
    ]
    omitted_source_locators: Annotated[
        tuple[SourceLocator, ...],
        Field(max_length=100),
    ] = ()


def is_enrichment(request) -> bool:
    return getattr(request, "operation", None) == SEMANTIC_ENRICHMENT


def enrichment_schema() -> str:
    schema = BedrockEnrichmentArtifact.model_json_schema()
    _bind_field_names(schema)
    return json.dumps(schema, separators=(",", ":"))


def wrap_enrichment_output(
    artifact_type: type[InventorySemanticExtractionArtifact],
    payload: object,
) -> GeneratedAgentOutput[InventorySemanticExtractionArtifact]:
    proposed = BedrockEnrichmentArtifact.model_validate_json(
        json.dumps(
            _normalize_wire_payload(payload),
            separators=(",", ":"),
        ),
    )
    artifact = artifact_type(
        candidates=tuple(
            ProposedInventoryCandidate(
                source_locator=candidate.source_locator,
                fields=tuple(
                    ProposedInventoryField(
                        field_name=name,
                        raw_value=value.raw_value,
                        normalized_value=value.normalized_value,
                        source_locator=value.source_locator,
                        evidence_basis="DERIVED_POLICY",
                        transformation=value.transformation,
                        confidence=value.confidence,
                    )
                    for name, value in candidate.fields.items()
                ),
                ambiguity_notes=candidate.ambiguity_notes,
            )
            for candidate in proposed.candidates
        ),
        omitted_source_locators=proposed.omitted_source_locators,
    )
    return governed_source_output(artifact_type, artifact)


def _normalize_wire_payload(payload: object) -> object:
    if not isinstance(payload, dict):
        return payload
    normalized = json.loads(json.dumps(payload))
    candidates = normalized.get("candidates")
    if not isinstance(candidates, list):
        return normalized
    for candidate in candidates:
        fields = candidate.get("fields") if isinstance(candidate, dict) else None
        if not isinstance(fields, dict):
            continue
        for name, field in fields.items():
            if not isinstance(field, dict):
                continue
            if field.get("transformation") == "DERIVED_POLICY":
                field["transformation"] = (
                    "DERIVED_FROM_CHANNEL"
                    if name == "product_type"
                    else "DERIVED_FROM_SOURCE_CONTEXT"
                )
            confidence = field.get("confidence")
            if isinstance(confidence, str):
                try:
                    numeric = float(confidence)
                except ValueError:
                    continue
                if 0 <= numeric <= 1:
                    field["confidence"] = numeric
    return normalized


def _bind_field_names(node: object) -> None:
    if isinstance(node, dict):
        properties = node.get("properties")
        if isinstance(properties, dict):
            fields = properties.get("fields")
            if isinstance(fields, dict):
                fields["propertyNames"] = {
                    "type": "string",
                    "enum": sorted(ENRICHMENT_FIELDS),
                }
        for value in node.values():
            _bind_field_names(value)
    elif isinstance(node, list):
        for value in node:
            _bind_field_names(value)

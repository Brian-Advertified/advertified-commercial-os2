"""Operation-specific Bedrock schema constraints for inventory source processing."""

import json

from pydantic import BaseModel, ValidationError
import pytest

from bedrock_enrichment_output import (
    enrichment_schema,
    wrap_enrichment_output,
)
from bedrock_schema import source_bound_schema
from bedrock_transcription_output import (
    BedrockTranscriptionArtifact,
    transcription_schema,
    wrap_transcription_output,
)
from inventory_semantic_contracts import InventorySemanticExtractionArtifact


class Located(BaseModel):
    locator: str


class Request(BaseModel):
    operation: str
    source_items: tuple[Located, ...]
    source_images: tuple[Located, ...]
    existing_rows: tuple[Located, ...]


def test_actual_transcription_schema_uses_unique_fields_per_row() -> None:
    schema = json.loads(source_bound_schema(
        transcription_schema(),
        Request(
            operation="SOURCE_TRANSCRIPTION",
            source_items=(Located(locator="source:1"),),
            source_images=(Located(locator="image:1"),),
            existing_rows=(),
        ),
    ))
    candidate = schema["$defs"][
        "BedrockTranscriptionCandidate"
    ]["properties"]
    field = schema["$defs"]["BedrockTranscribedField"]["properties"]
    allowed = candidate["fields"]["propertyNames"]["enum"]

    assert candidate["source_locator"]["enum"] == ["source:1", "image:1"]
    assert field["source_locator"]["enum"] == ["source:1", "image:1"]
    assert "name" in allowed
    assert "dimensions" in allowed
    assert "channel" not in allowed
    assert "rate_type" not in allowed
    assert candidate["fields"]["type"] == "object"

    payload = {
        "candidates": [{
            "source_locator": "image:1",
            "fields": {
                "name": {
                    "raw_value": "DStv Stream VOD",
                    "source_locator": "image:1",
                    "transformation": "TRIM",
                    "confidence": 1,
                },
                "rate": {
                    "raw_value": "R575",
                    "source_locator": "image:1",
                    "transformation": "TRIM",
                    "confidence": 1,
                },
            },
            "ambiguity_notes": [],
        }],
        "omitted_source_locators": ["source:1"],
    }
    wrapped = wrap_transcription_output(
        InventorySemanticExtractionArtifact, payload
    )
    assert [
        item.field_name for item in wrapped.artifact.candidates[0].fields
    ] == ["name", "rate"]
    with pytest.raises(ValidationError):
        BedrockTranscriptionArtifact.model_validate({
            **payload,
            "candidates": [{
                **payload["candidates"][0],
                "fields": [
                    payload["candidates"][0]["fields"]["name"],
                ],
            }],
        })


def test_transcription_drops_header_row_and_reconstructs_dimensions() -> None:
    def value(raw: str) -> dict:
        return {
            "raw_value": raw,
            "source_locator": "image:1",
            "transformation": "TRIM",
            "confidence": 1,
        }

    payload = {
        "candidates": [
            {
                "source_locator": "image:1",
                "fields": {
                    "name": value("Streaming"),
                    "placement": value("Ad Unit"),
                    "dimensions": value("Width x Height"),
                    "format": value("Format"),
                    "rate": value("Rate"),
                },
                "ambiguity_notes": [],
            },
            {
                "source_locator": "image:1",
                "fields": {
                    "name": value("DStv Stream VOD"),
                    "placement": value("Video Pre Roll"),
                    "dimensions": value("16 9"),
                    "format": value("MP4"),
                    "rate": value("R575"),
                },
                "ambiguity_notes": [],
            },
        ],
        "omitted_source_locators": ["source:1"],
    }

    wrapped = wrap_transcription_output(
        InventorySemanticExtractionArtifact, payload
    )

    candidate = wrapped.artifact.candidates[0]
    assert len(wrapped.artifact.candidates) == 1
    dimension = next(
        item for item in candidate.fields
        if item.field_name == "dimensions"
    )
    assert dimension.raw_value == "16 x 9"
    assert dimension.transformation == "DERIVED_FROM_SOURCE_CONTEXT"


def test_actual_enrichment_schema_targets_only_existing_rows() -> None:
    schema = json.loads(source_bound_schema(
        enrichment_schema(),
        Request(
            operation="SEMANTIC_ENRICHMENT",
            source_items=(Located(locator="source:1"),),
            source_images=(Located(locator="image:1"),),
            existing_rows=(Located(locator="row:1"),),
        ),
    ))
    candidate = schema["$defs"][
        "BedrockEnrichmentCandidate"
    ]["properties"]
    field = schema["$defs"]["BedrockEnrichedField"]["properties"]

    assert candidate["source_locator"]["enum"] == ["row:1"]
    assert field["source_locator"]["enum"] == [
        "source:1", "image:1", "row:1",
    ]
    assert candidate["fields"]["propertyNames"]["enum"] == [
        "channel", "description", "product_type",
    ]
    assert field["normalized_value"]["type"] == "string"

    payload = {
        "candidates": [{
            "source_locator": "row:1",
            "fields": {
                "channel": {
                    "raw_value": "DStv Stream VOD",
                    "normalized_value": "DIGITAL",
                    "source_locator": "source:1",
                    "transformation": "DERIVED_POLICY",
                    "confidence": "1",
                },
            },
            "ambiguity_notes": [],
        }],
        "omitted_source_locators": ["image:1"],
    }
    wrapped = wrap_enrichment_output(
        InventorySemanticExtractionArtifact, payload
    )
    field_value = wrapped.artifact.candidates[0].fields[0]
    assert field_value.field_name == "channel"
    assert field_value.evidence_basis == "DERIVED_POLICY"
    assert field_value.transformation == "DERIVED_FROM_SOURCE_CONTEXT"
    assert str(field_value.confidence) == "1"

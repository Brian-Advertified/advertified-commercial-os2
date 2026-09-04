"""Two-stage inventory source transcription and semantic enrichment rules."""

from __future__ import annotations

import re
from decimal import Decimal

from contracts import (
    AgentOutputEnvelope,
    ConfidenceAssessment,
    OutputStatus,
    ProviderUsage,
    SuggestedNextAction,
    UnknownItem,
)
from inventory_semantic_contracts import (
    InventorySemanticAgentRequest,
    InventorySemanticExtractionArtifact,
)

SOURCE_TRANSCRIPTION = "SOURCE_TRANSCRIPTION"
SEMANTIC_ENRICHMENT = "SEMANTIC_ENRICHMENT"

SEMANTIC_FIELDS = frozenset({
    "channel",
    "product_type",
    "description",
})
RESTRICTED_TRANSCRIPTION_FIELDS = frozenset({
    *SEMANTIC_FIELDS,
    "rate_type",
    "currency",
    "availability",
    "rate_valid_from",
    "rate_valid_to",
    "booking_deadline",
    "material_deadline",
})
IDENTITY_FIELDS = frozenset({"name", "product_code"})
TRANSCRIPTION_TRANSFORMATIONS = frozenset({
    "TRIM",
    "DERIVED_FROM_SOURCE_CONTEXT",
})
ENRICHMENT_TRANSFORMATIONS = frozenset({
    "DERIVED_FROM_CHANNEL",
    "DERIVED_FROM_SOURCE_CONTEXT",
})


def propose_semantic_extraction(
    request: InventorySemanticAgentRequest,
) -> AgentOutputEnvelope[InventorySemanticExtractionArtifact]:
    subject = (
        "source transcription"
        if request.operation == SOURCE_TRANSCRIPTION
        else "semantic enrichment"
    )
    return AgentOutputEnvelope(
        schema_version="1.0.0",
        status=OutputStatus.REVIEW_REQUIRED,
        artifact=InventorySemanticExtractionArtifact(candidates=()),
        evidence_bindings=(),
        unknowns=(UnknownItem(
            field_path="artifact.candidates",
            question=(
                f"Live inventory {subject} is disabled; the bounded source "
                "still requires human review."
            ),
            is_blocking=True,
        ),),
        assumptions=(),
        confidence=(ConfidenceAssessment(
            field_path="artifact.candidates",
            confidence=Decimal("0"),
        ),),
        objections=(),
        rationale=(
            "The deterministic runtime preserves unknown inventory facts "
            "instead of inventing them."
        ),
        suggested_next_action=SuggestedNextAction(
            command_code="ReviewInventorySemanticExtraction",
            requires_human=True,
        ),
        usage=ProviderUsage(
            provider="deterministic",
            model="fixture-v1",
            units=0,
            tool_calls=0,
            incremental_cost_minor=0,
            cache_status="FIXTURE",
        ),
    )


def validate_semantic_grounding(
    request: InventorySemanticAgentRequest,
    output: AgentOutputEnvelope[
        InventorySemanticExtractionArtifact
    ],
) -> None:
    artifact = output.artifact
    if artifact is None:
        return
    sources = {
        item.locator: _comparable(item.content)
        for item in request.source_items
    }
    images = {item.locator for item in request.source_images}
    existing = {
        item.locator: _comparable(_existing_source_text(item.values))
        for item in request.existing_rows
    }
    allowed_sources = {*sources, *images}
    if not set(artifact.omitted_source_locators).issubset(
            allowed_sources):
        raise ValueError(
            "Semantic output omitted a source locator that was not supplied."
        )
    for candidate in artifact.candidates:
        if request.operation == SOURCE_TRANSCRIPTION:
            _validate_transcription_candidate(
                candidate, allowed_sources)
        else:
            _validate_enrichment_candidate(
                candidate, set(existing)
            )
        for field in candidate.fields:
            _validate_field_source(
                field, sources, images, existing
            )
            if request.operation == SOURCE_TRANSCRIPTION:
                _validate_transcription_field(field)
            else:
                _validate_enrichment_field(request, field)
        if request.operation == SOURCE_TRANSCRIPTION:
            _validate_ambiguous_rates(candidate)
    _validate_image_accounting(artifact, images)


def _validate_transcription_candidate(
    candidate,
    allowed_sources: set[str],
) -> None:
    if candidate.source_locator not in allowed_sources:
        raise ValueError(
            "Source transcription referenced a source that was not supplied."
        )
    names = {field.field_name for field in candidate.fields}
    if not IDENTITY_FIELDS.intersection(names):
        raise ValueError(
            "A transcribed inventory candidate requires a source identity."
        )


def _validate_enrichment_candidate(
    candidate,
    existing_rows: set[str],
) -> None:
    if candidate.source_locator not in existing_rows:
        raise ValueError(
            "Semantic enrichment must target an existing deterministic row."
        )
    if any(
        field.field_name not in SEMANTIC_FIELDS
        for field in candidate.fields
    ):
        raise ValueError(
            "Semantic enrichment cannot author commercial source facts."
        )


def _validate_field_source(
    field,
    sources: dict[str, str],
    images: set[str],
    existing: dict[str, str],
) -> None:
    if field.source_locator in images:
        return
    source = sources.get(field.source_locator)
    if source is None:
        source = existing.get(field.source_locator)
    if (
        source is None
        or _comparable(field.raw_value) not in source
    ):
        raise ValueError(
            "Semantic field raw value is not present in its cited source."
        )


def _validate_transcription_field(field) -> None:
    if field.field_name in RESTRICTED_TRANSCRIPTION_FIELDS:
        raise ValueError(
            "Source transcription cannot author semantic, governed or dated "
            "facts."
        )
    if field.evidence_basis != "SUPPLIER_SUPPLIED":
        raise ValueError(
            "Source transcription must remain supplier-supplied."
        )
    if field.normalized_value is not None:
        raise ValueError(
            "Source transcription cannot normalize supplier facts."
        )
    if field.transformation not in TRANSCRIPTION_TRANSFORMATIONS:
        raise ValueError(
            "Source transcription used an unsupported transformation."
        )
    if (
        field.transformation == "DERIVED_FROM_SOURCE_CONTEXT"
        and field.field_name != "dimensions"
    ):
        raise ValueError(
            "Only source-visible dimensions may use contextual transcription."
        )


def _validate_enrichment_field(
    request: InventorySemanticAgentRequest,
    field,
) -> None:
    if field.evidence_basis != "DERIVED_POLICY":
        raise ValueError(
            "Semantic enrichment must be labelled as derived policy."
        )
    if not field.normalized_value:
        raise ValueError(
            "Semantic enrichment requires an explicit normalized value."
        )
    if field.transformation not in ENRICHMENT_TRANSFORMATIONS:
        raise ValueError(
            "Semantic enrichment used an unsupported transformation."
        )
    _validate_governed_code(request, field)


def _validate_governed_code(
    request: InventorySemanticAgentRequest,
    field,
) -> None:
    allowed = {
        "channel": request.governed_codes.channels,
        "product_type": request.governed_codes.product_types,
    }.get(field.field_name)
    if allowed is not None and field.normalized_value not in allowed:
        raise ValueError(
            f"Semantic governed field '{field.field_name}' uses a "
            "non-allow-listed code."
        )


def _validate_ambiguous_rates(candidate) -> None:
    ambiguous = any(
        re.search(r",\d{1,2}\s*$", field.raw_value)
        for field in candidate.fields
        if field.field_name == "rate"
    )
    if ambiguous and not candidate.ambiguity_notes:
        raise ValueError(
            "A visibly incomplete or ambiguous rate requires an ambiguity note."
        )


def _validate_image_accounting(artifact, images: set[str]) -> None:
    accounted_images = {
        *artifact.omitted_source_locators,
        *(candidate.source_locator
          for candidate in artifact.candidates),
        *(field.source_locator
          for candidate in artifact.candidates
          for field in candidate.fields),
    }
    if not images.issubset(accounted_images):
        raise ValueError(
            "Every semantic source image must be used or explicitly omitted."
        )


def _existing_source_text(values: dict) -> str:
    supplied = [str(value) for value in values.values()]
    return "\n".join(supplied) + "\n" + " | ".join(supplied)


def _comparable(value: str) -> str:
    return re.sub(r"\s+", " ", value).strip().casefold()

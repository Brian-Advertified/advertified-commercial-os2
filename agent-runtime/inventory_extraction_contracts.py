"""Versioned transport contract for generic Docling inventory projection."""

from __future__ import annotations

from typing import Any, Annotated

from pydantic import BaseModel, ConfigDict, Field


def _camel(name: str) -> str:
    head, *tail = name.split("_")
    return head + "".join(part.title() for part in tail)


class ProjectionModel(BaseModel):
    model_config = ConfigDict(
        alias_generator=_camel,
        populate_by_name=True,
        extra="forbid",
        frozen=True,
    )


class InventoryProjectionRequest(ProjectionModel):
    provider_document: dict[str, Any]


class ExtractedRateVariant(ProjectionModel):
    raw_value: Annotated[str, Field(min_length=1, max_length=2_000)]
    source_locator: Annotated[str, Field(min_length=1, max_length=1_000)]
    header_hierarchy: Annotated[str, Field(min_length=1, max_length=2_000)]
    header_locators: tuple[str, ...]
    position_json: str | None = None
    rate_type: str | None = None
    currency: str | None = None
    buying_unit: str | None = None
    valid_from: str | None = None
    valid_to: str | None = None
    geography: str | None = None
    daypart: str | None = None
    days: str | None = None
    duration_seconds: int | None = None
    dimensions: dict[str, str] | None = None


class ExtractedRow(ProjectionModel):
    number: Annotated[int, Field(ge=1)]
    locator: Annotated[str, Field(min_length=1, max_length=1_000)]
    values: dict[str, str]
    extraction_method: str
    confidence: float | None = None
    field_locators: dict[str, str] | None = None
    field_confidences: dict[str, float | None] | None = None
    field_evidence_bases: dict[str, str] | None = None
    field_transformations: dict[str, str] | None = None
    discovered_fields: None = None
    schema_warnings: tuple[str, ...] | None = None
    rate_variants: tuple[ExtractedRateVariant, ...] | None = None


class ExtractedSourceElement(ProjectionModel):
    locator: Annotated[str, Field(min_length=1, max_length=1_000)]
    structure_id: Annotated[str, Field(min_length=1, max_length=1_000)]
    structure_kind: Annotated[str, Field(min_length=1, max_length=100)]
    row: int
    column: int
    raw_value: Annotated[str, Field(min_length=1, max_length=100_000)]
    position_json: str | None = None


class InventoryProjectionResponse(ProjectionModel):
    schema_version: str = "advertified.inventory-extraction.python.v1"
    projector_version: str = "advertified-docling-python/1.7.0"
    rows: tuple[ExtractedRow, ...]
    source_elements: tuple[ExtractedSourceElement, ...]
    warnings: tuple[str, ...] = ()

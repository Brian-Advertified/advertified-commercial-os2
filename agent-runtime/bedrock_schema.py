"""Bind Bedrock output schemas to request-owned sources and operations."""

from __future__ import annotations

import json

from pydantic import BaseModel

SOURCE_TRANSCRIPTION = "SOURCE_TRANSCRIPTION"
SEMANTIC_ENRICHMENT = "SEMANTIC_ENRICHMENT"
SEMANTIC_FIELDS = ("channel", "product_type", "description")
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


def source_bound_schema(schema_json: str, request: BaseModel) -> str:
    schema = json.loads(schema_json)
    sources = _locators(request, "source_items", "source_images")
    existing = _locators(request, "existing_rows")
    operation = getattr(request, "operation", None)
    if not sources and not existing:
        return schema_json
    _bind(
        schema,
        sources,
        existing,
        operation,
    )
    return json.dumps(schema, separators=(",", ":"))


def structured_output_tool(schema_json: str) -> dict[str, object]:
    return {
        "tools": [
            {
                "toolSpec": {
                    "name": "submit_advertified_result",
                    "description": "Submit the governed extraction result.",
                    "inputSchema": {"json": json.loads(schema_json)},
                },
            }
        ],
        "toolChoice": {
            "tool": {"name": "submit_advertified_result"},
        },
    }


def _locators(request: BaseModel, *attributes: str) -> list[str]:
    return list(dict.fromkeys(
        item.locator
        for attribute in attributes
        for item in getattr(request, attribute, ())
    ))


def _bind(
    node: object,
    sources: list[str],
    existing: list[str],
    operation: str | None,
) -> None:
    if isinstance(node, dict):
        properties = node.get("properties", {})
        if isinstance(properties, dict):
            _bind_properties(
                properties,
                sources,
                existing,
                operation,
            )
        for value in node.values():
            _bind(value, sources, existing, operation)
    elif isinstance(node, list):
        for value in node:
            _bind(value, sources, existing, operation)


def _bind_properties(
    properties: dict[str, object],
    sources: list[str],
    existing: list[str],
    operation: str | None,
) -> None:
    is_candidate = "fields" in properties
    is_field = "field_name" in properties
    source_locator = properties.get("source_locator")
    if isinstance(source_locator, dict):
        allowed = _source_locator_values(
            sources,
            existing,
            operation,
            is_candidate,
        )
        source_locator.clear()
        source_locator.update({
            "type": "string",
            "enum": allowed,
        })
    omitted = properties.get("omitted_source_locators")
    if isinstance(omitted, dict):
        omitted["items"] = {
            "type": "string",
            "enum": sources,
        }
    if is_field:
        _bind_field_contract(properties, operation)


def _source_locator_values(
    sources: list[str],
    existing: list[str],
    operation: str | None,
    is_candidate: bool,
) -> list[str]:
    if operation == SEMANTIC_ENRICHMENT and is_candidate:
        return existing
    if operation == SEMANTIC_ENRICHMENT:
        return list(dict.fromkeys((*sources, *existing)))
    return sources


def _bind_field_contract(
    properties: dict[str, object],
    operation: str | None,
) -> None:
    field_name = properties.get("field_name")
    evidence_basis = properties.get("evidence_basis")
    transformation = properties.get("transformation")
    normalized = properties.get("normalized_value")
    if operation == SEMANTIC_ENRICHMENT:
        _set_enum(field_name, SEMANTIC_FIELDS)
        _set_enum(evidence_basis, ("DERIVED_POLICY",))
        _set_enum(
            transformation,
            (
                "DERIVED_FROM_CHANNEL",
                "DERIVED_FROM_SOURCE_CONTEXT",
            ),
        )
        if isinstance(normalized, dict):
            normalized.clear()
            normalized.update({
                "type": "string",
                "minLength": 1,
                "maxLength": 2_000,
            })
        return
    if operation != SOURCE_TRANSCRIPTION:
        return
    if isinstance(field_name, dict):
        allowed = tuple(
            value
            for value in field_name.get("enum", ())
            if value not in RESTRICTED_TRANSCRIPTION_FIELDS
        )
        _set_enum(field_name, allowed)
    _set_enum(evidence_basis, ("SUPPLIER_SUPPLIED",))
    _set_enum(
        transformation,
        ("TRIM", "DERIVED_FROM_SOURCE_CONTEXT"),
    )
    if isinstance(normalized, dict):
        normalized.clear()
        normalized.update({"type": "null"})


def _set_enum(node: object, values: tuple[str, ...]) -> None:
    if not isinstance(node, dict):
        return
    node.clear()
    node.update({
        "type": "string",
        "enum": list(values),
    })

"""Compact provider contract for governed Audience Intelligence.

The provider makes bounded decisions for each Brief-supplied audience. Advertified owns
identity, geography, provenance classification and final canonical narrative.
"""
from __future__ import annotations

from decimal import Decimal
import json
from typing import Annotated
from uuid import UUID

from pydantic import Field

from contracts import ContractModel
from planning_contracts import AudienceAgentRequest
from planning_service import segment_support_reference_ids


class AudienceProviderProposal(ContractModel):
    name: Annotated[str, Field(min_length=1, max_length=300)]
    need_state: Annotated[str | None, Field(max_length=1_000)] = None
    buying_context: Annotated[str | None, Field(max_length=1_000)] = None
    evidence_item_ids: tuple[UUID, ...] = ()
    reference_observation_ids: tuple[UUID, ...] = ()
    confidence: Annotated[Decimal, Field(ge=0, le=1)] | None = None
    is_target: bool


class AudienceProviderArtifact(ContractModel):
    audiences: Annotated[tuple[AudienceProviderProposal, ...], Field(max_length=20)]
    targeting_rationale: Annotated[str | None, Field(max_length=4_000)] = None
    positioning_statement: Annotated[str | None, Field(max_length=4_000)] = None


def required_audience_names(request: AudienceAgentRequest) -> list[str]:
    return list(dict.fromkeys(
        value.strip() for value in request.planning.audiences if value.strip()
    ))


def audience_schema(request: AudienceAgentRequest) -> str:
    """Use one named object per Brief audience instead of a provider-generated array.

    Haiku has repeatedly serialized the root audience array into malformed strings despite a
    forced tool schema. Fixed object slots remove that failure mode and also prevent the model
    from silently merging, renaming or omitting client-required audiences. Advertified converts
    these slots back to the canonical typed audience collection after provider validation.
    """
    required = required_audience_names(request)
    if not required:
        raise ValueError("Audience Intelligence requires at least one Brief-supplied audience.")
    brief_ids = [str(value) for value in request.invocation.approved_evidence_item_ids]
    reference_ids = [str(value) for value in segment_support_reference_ids(request)]
    properties: dict[str, object] = {}
    required_fields: list[str] = []
    for index, name in enumerate(required, start=1):
        key = f"audience_{index}"
        properties[key] = _audience_slot(name, brief_ids, reference_ids)
        required_fields.append(key)
    properties["positioning_statement"] = {
        "type": "string", "minLength": 1, "maxLength": 4000,
        "description": (
            "Optional artifact-level positioning direction only. Prefix exactly 'Hypothesis:' "
            "when it is not directly supported by approved evidence."
        ),
    }
    schema = {
        "type": "object",
        "additionalProperties": False,
        "properties": properties,
        "required": required_fields,
    }
    return json.dumps(schema, separators=(",", ":"))


def _audience_slot(name: str, brief_ids: list[str], reference_ids: list[str]) -> dict[str, object]:
    fields: dict[str, object] = {
        "name": {
            "type": "string", "const": name,
            "description": "Exact client-required audience name. Do not rename or generalise it.",
        },
        "need_state": {
            "type": "string", "minLength": 1, "maxLength": 1000,
            "description": "Optional. If unverified, prefix exactly 'Hypothesis:'. Omit rather than invent.",
        },
        "buying_context": {
            "type": "string", "minLength": 1, "maxLength": 1000,
            "description": "Optional. If unverified, prefix exactly 'Hypothesis:'. Omit rather than invent.",
        },
        "is_target": {
            "type": "boolean",
            "description": "Whether this client-required audience should be carried into media planning.",
        },
    }
    if brief_ids:
        fields["evidence_item_ids"] = {
            "type": "array", "items": {"type": "string", "enum": brief_ids},
            "uniqueItems": True, "description": "Optional exact approved Brief evidence IDs only.",
        }
    if reference_ids:
        fields["reference_observation_ids"] = {
            "type": "array", "items": {"type": "string", "enum": reference_ids},
            "uniqueItems": True,
            "description": "Optional exact AUDIENCE_SEGMENT_SUPPORT observation IDs only.",
        }
    if brief_ids or reference_ids:
        fields["confidence"] = {"type": "number", "minimum": 0, "maximum": 1}
    return {
        "type": "object",
        "additionalProperties": False,
        "properties": fields,
        "required": ["name", "is_target"],
    }

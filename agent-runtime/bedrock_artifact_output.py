"""Advertified-owned governance envelope for Bedrock business artifacts."""

from __future__ import annotations

import ast
import json

import yaml

from master_data_codes import EvidenceClassifications
from proposal_provider_facts import approved_fact_lines
from bedrock_audience_schema import AudienceProviderArtifact, required_audience_names, audience_schema
from bedrock_media_schema import MediaStrategyProviderArtifact
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


def _normalize_json_container_strings(value, schema, root_schema=None, *, allow_yaml=False):
    root = schema if root_schema is None else root_schema
    if isinstance(schema, dict) and "$ref" in schema:
        prefix = "#/$defs/"
        reference = schema["$ref"]
        if isinstance(reference, str) and reference.startswith(prefix):
            schema = root.get("$defs", {}).get(reference[len(prefix):], schema)
    if not isinstance(schema, dict):
        return value
    expected = schema.get("type")
    if isinstance(value, str) and expected in {"array", "object"}:
        parsed = None
        try:
            parsed = json.loads(value)
        except json.JSONDecodeError:
            try:
                parsed = json.loads(value, strict=False)
            except json.JSONDecodeError:
                # Haiku can occasionally return a schema-declared JSON container as a
                # Python-literal-like string inside tool input. ast.literal_eval parses
                # syntax only (no code execution) and lets the strict typed contract
                # decide whether the resulting value is acceptable.
                if len(value) <= 100_000 and value[:1] in {"[", "{"}:
                    try:
                        parsed = ast.literal_eval(value)
                    except (SyntaxError, ValueError):
                        if allow_yaml:
                            try:
                                parsed = yaml.safe_load(value)
                            except yaml.YAMLError:
                                parsed = None
                        else:
                            parsed = None
        if expected == "array" and isinstance(parsed, list):
            value = parsed
        elif expected == "object" and isinstance(parsed, dict):
            value = parsed
        else:
            return value
    if expected == "array" and isinstance(value, list):
        item_schema = schema.get("items", {})
        return [
            _normalize_json_container_strings(
                item, item_schema, root, allow_yaml=allow_yaml,
            )
            for item in value
        ]
    if expected == "object" and isinstance(value, dict):
        properties = schema.get("properties", {})
        return {
            key: _normalize_json_container_strings(
                item, properties.get(key, {}), root, allow_yaml=allow_yaml,
            )
            for key, item in value.items()
        }
    return value


def _normalize_media_strategy_advisory_arrays(payload):
    """Discard only unreadable advisory lists that canonical strategy rules rebuild.

    Channel recommendations are the provider's substantive decision and are never repaired here.
    The strategy canonicalizer deterministically rebuilds strategic principles and trade-offs and
    always appends request-owned evidence gaps, so an unparsable string in those advisory list
    fields can safely become an empty list without creating or laundering commercial facts.
    """
    if not isinstance(payload, dict):
        return payload
    result = dict(payload)
    for field in ("strategic_principles", "excluded_channels", "evidence_gaps"):
        if isinstance(result.get(field), str):
            result[field] = []
    recommendations = result.get("channel_recommendations")
    if isinstance(recommendations, list):
        repaired = []
        for item in recommendations:
            if not isinstance(item, dict):
                repaired.append(item)
                continue
            copy = dict(item)
            for field in ("trade_offs", "evidence_gaps"):
                if isinstance(copy.get(field), str):
                    copy[field] = []
            repaired.append(copy)
        result["channel_recommendations"] = repaired
    return result


def _normalize_audience_provider_shape(payload, request):
    """Convert fixed audience slots to the canonical compact collection.

    The live provider schema uses ``audience_1`` ... ``audience_n`` objects so Haiku never has
    to serialize the substantive audience collection as a root array. The legacy ``audiences``
    shape remains accepted here for deterministic boundary fixtures, but no new live request
    asks the provider to emit that shape.
    """
    if not isinstance(payload, dict):
        return payload
    if "audiences" in payload:
        legacy = _normalize_json_container_strings(
            payload, AudienceProviderArtifact.model_json_schema(), allow_yaml=True,
        )
        audiences = legacy.get("audiences") if isinstance(legacy, dict) else None
        if not isinstance(audiences, list):
            return legacy
        repaired = []
        for item in audiences:
            if not isinstance(item, dict):
                repaired.append(item)
                continue
            copy = dict(item)
            copy.pop("positioning_statement", None)
            repaired.append(copy)
        result = dict(legacy)
        result["audiences"] = repaired
        return result

    required = required_audience_names(request)
    items = []
    for index, _ in enumerate(required, start=1):
        item = payload.get(f"audience_{index}")
        if item is not None:
            items.append(item)
    result = {
        "audiences": items,
        "targeting_rationale": None,
        "positioning_statement": payload.get("positioning_statement"),
    }
    return result


def _expand_audience_provider_payload(payload, request):
    payload = _normalize_audience_provider_shape(payload, request)
    compact = AudienceProviderArtifact.model_validate_json(
        json.dumps(payload, separators=(",", ":"))
    )
    return {
        "audiences": [
            {
                "name": item.name,
                "description": "Provider audience proposal pending Advertified canonicalization.",
                "need_state": item.need_state,
                "buying_context": item.buying_context,
                "geographies": list(request.planning.geographies),
                "language": None,
                "life_stage": None,
                "lsm_sem": None,
                "lsm_sem_taxonomy": None,
                "lsm_sem_taxonomy_version": None,
                "lsm_sem_mandatory": False,
                "classification": EvidenceClassifications.HYPOTHESIS.value,
                "exclusions": ["Do not infer sensitive individual attributes."],
                "evidence_item_ids": [str(value) for value in item.evidence_item_ids],
                "reference_observation_ids": [str(value) for value in item.reference_observation_ids],
                "confidence": float(item.confidence) if item.confidence is not None else None,
                "is_target": item.is_target,
            }
            for item in compact.audiences
        ],
        "targeting_rationale": compact.targeting_rationale,
        "positioning_statement": compact.positioning_statement,
    }


def _expand_media_strategy_provider_payload(payload):
    compact = MediaStrategyProviderArtifact.model_validate_json(
        json.dumps(payload, separators=(",", ":"))
    )
    return {
        "summary": "Provider channel choices pending Advertified canonicalization.",
        "channel_recommendations": [
            {
                "channel": item.channel,
                "role": item.role,
                "rationale": "Provider channel choice pending Advertified canonicalization.",
                "objective_contribution": "Provider channel role pending Advertified canonicalization.",
                "geography_role": None,
                "classification": item.classification,
                "budget_guidance_percent": (
                    float(item.budget_guidance_percent)
                    if item.budget_guidance_percent is not None else None
                ),
                "trade_offs": [],
                "evidence_gaps": [],
            }
            for item in compact.channel_recommendations
        ],
        "strategic_principles": [],
        "excluded_channels": list(compact.excluded_channels),
        "evidence_gaps": [],
    }


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
    """Validate provider data after syntax-only JSON container normalization."""
    if (
        isinstance(payload, dict)
        and set(payload) == {"artifact"}
        and isinstance(payload["artifact"], dict)
    ):
        payload = payload["artifact"]
    # Some Bedrock models serialize a schema-declared array/object as a JSON string inside
    # tool input. Parse only those syntactic containers; never invent, drop or coerce values.
    # Audience Intelligence uses a compact provider contract so Haiku does not redundantly
    # echo request-owned geography/provenance fields or exhaust its structured-output budget.
    if artifact_type.__name__ == "AudienceDefinitionSetArtifact":
        payload = _normalize_json_container_strings(
            payload, json.loads(audience_schema(request)), allow_yaml=True,
        )
        payload = _expand_audience_provider_payload(payload, request)
    elif artifact_type.__name__ == "MediaStrategyArtifact":
        payload = _normalize_json_container_strings(
            payload, MediaStrategyProviderArtifact.model_json_schema(),
        )
        payload = _expand_media_strategy_provider_payload(payload)
    else:
        payload = _normalize_json_container_strings(payload, artifact_type.model_json_schema())
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

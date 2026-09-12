"""Typed Bedrock output decoding and evidence validation."""

from __future__ import annotations

import json
from typing import TypeVar

from pydantic import BaseModel, ValidationError

from bedrock_failure import BedrockProviderError
from bedrock_artifact_output import artifact_schema, wrap_artifact_output
from bedrock_enrichment_output import (
    enrichment_schema,
    wrap_enrichment_output,
)
from bedrock_location_output import location_synthesis_schema, wrap_location_synthesis_output
from bedrock_media_schema import media_strategy_schema
from bedrock_audience_schema import audience_schema
from planning_contracts import AudienceAgentRequest
from media_strategy_contracts import MediaStrategyRequest
from location_intelligence_contracts import LocationSynthesisRequest
from bedrock_schema import source_bound_schema
from bedrock_supplied_brief_output import (
    supplied_brief_schema,
    wrap_supplied_brief_output,
)
from bedrock_transcription_output import (
    transcription_schema,
    wrap_transcription_output,
)
from supplied_brief_contracts import SuppliedBriefArtifact, SuppliedBriefRequest
from contracts import (
    AgentInvocationEnvelope,
    AgentOutputEnvelope,
    ContractModel,
    GeneratedAgentOutput,
    ProviderUsage,
)

ArtifactT = TypeVar("ArtifactT", bound=ContractModel)


def output_schema(
    request: BaseModel,
    artifact_type: type[ArtifactT],
    generated_type,
    semantic: bool,
    transcription: bool,
) -> str:
    schema = (
        location_synthesis_schema(request)
        if isinstance(request, LocationSynthesisRequest)
        else media_strategy_schema(request)
        if isinstance(request, MediaStrategyRequest)
        else audience_schema(request)
        if isinstance(request, AudienceAgentRequest)
        else supplied_brief_schema()
        if isinstance(request, SuppliedBriefRequest)
        else transcription_schema()
        if transcription
        else enrichment_schema()
        if semantic
        else artifact_schema(artifact_type)
    )
    return source_bound_schema(schema, request)


def decode_generated_output(
    response: dict[str, object],
    artifact_type: type[ArtifactT],
    generated_type,
    semantic: bool,
    transcription: bool,
    request: BaseModel,
    usage: ProviderUsage,
):
    invocation = request.invocation  # type: ignore[attr-defined]
    payload = _decode_payload(response, usage)
    generated = _validate_typed_output(
        payload,
        artifact_type,
        generated_type,
        semantic,
        transcription,
        request,
        usage,
    )
    try:
        _validate_evidence(generated, invocation)
    except BedrockProviderError as error:
        raise BedrockProviderError(
            str(error),
            stage="EVIDENCE_VALIDATION",
            acceptance="ACCEPTED",
            usage=usage,
            rejected_output=payload,
        ) from error
    return generated


def _decode_payload(
    response: dict[str, object],
    usage: ProviderUsage,
) -> object:
    try:
        return _response_payload(response)
    except BedrockProviderError as error:
        raise BedrockProviderError(
            str(error),
            stage="OUTPUT_DECODE",
            acceptance="ACCEPTED",
            usage=usage,
        ) from error


def _validate_typed_output(
    payload: object,
    artifact_type: type[ArtifactT],
    generated_type,
    semantic: bool,
    transcription: bool,
    request: BaseModel,
    usage: ProviderUsage,
):
    try:
        if isinstance(request, LocationSynthesisRequest):
            return wrap_location_synthesis_output(payload, request)
        if artifact_type is SuppliedBriefArtifact:
            return wrap_supplied_brief_output(artifact_type, payload)
        if transcription:
            return wrap_transcription_output(artifact_type, payload)
        return (
            wrap_enrichment_output(artifact_type, payload)
            if semantic
            else wrap_artifact_output(
                artifact_type,
                payload,
                request,
            )
        )
    except ValidationError as error:
        details = "; ".join(
            ".".join(str(part) for part in item["loc"]) + ":" + item["type"]
            for item in error.errors(
                include_input=False,
                include_context=False,
            )[:12]
        )
        raise BedrockProviderError(
            "Bedrock output failed the typed contract"
            + (f" ({details})." if details else "."),
            stage="TYPED_CONTRACT",
            acceptance="ACCEPTED",
            usage=usage,
            rejected_output=payload,
        ) from error


def _response_payload(response: dict[str, object]) -> object:
    try:
        output = response["output"]
        message = output["message"]  # type: ignore[index]
        content = message["content"]  # type: ignore[index]
        tool_parts = [
            item["toolUse"]
            for item in content  # type: ignore[union-attr]
            if "toolUse" in item
        ]
    except (KeyError, TypeError) as error:
        raise BedrockProviderError(
            "Bedrock response did not contain structured output."
        ) from error
    if len(tool_parts) != 1 or not isinstance(tool_parts[0], dict):
        raise BedrockProviderError(
            "Bedrock response must contain one structured output."
        )
    tool = tool_parts[0]
    if tool.get("name") != "submit_advertified_result" or "input" not in tool:
        raise BedrockProviderError(
            "Bedrock returned an unexpected structured output."
        )
    return tool["input"]


def _validate_evidence(
    generated: GeneratedAgentOutput[ArtifactT],
    invocation: AgentInvocationEnvelope,
) -> None:
    approved = set(invocation.approved_evidence_item_ids)
    if any(
        not set(binding.evidence_item_ids).issubset(approved)
        for binding in generated.evidence_bindings
    ):
        raise BedrockProviderError(
            "Bedrock output referenced unapproved evidence."
        )


def attach_usage(
    generated,
    artifact_type: type[ArtifactT],
    usage: ProviderUsage,
) -> AgentOutputEnvelope[ArtifactT]:
    return AgentOutputEnvelope[artifact_type](
        schema_version=generated.schema_version,
        status=generated.status,
        artifact=generated.artifact,
        evidence_bindings=generated.evidence_bindings,
        unknowns=generated.unknowns,
        assumptions=generated.assumptions,
        confidence=generated.confidence,
        objections=generated.objections,
        rationale=generated.rationale,
        suggested_next_action=generated.suggested_next_action,
        usage=usage,
    )

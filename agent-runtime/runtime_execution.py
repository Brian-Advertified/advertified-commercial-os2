"""Route one typed agent request to deterministic or Bedrock execution."""

from __future__ import annotations

import json
from collections.abc import Callable

from fastapi import HTTPException
from pydantic import BaseModel, ValidationError

from agent_registry import AgentCode
from bedrock_provider import (
    BEDROCK_MODE,
    BedrockProviderError,
    generate_with_bedrock,
)
from creative_contracts import CreativeAgentRequest, CreativeConceptSetArtifact
from creative_service import generate_creative_concepts
from inventory_semantic_contracts import (
    InventorySemanticAgentRequest,
    InventorySemanticExtractionArtifact,
)
from inventory_semantic_service import (
    SEMANTIC_ENRICHMENT,
    SOURCE_TRANSCRIPTION,
    propose_semantic_extraction,
    validate_semantic_grounding,
)
from measurement_contracts import (
    MeasurementAgentRequest,
    MeasurementInterpretationArtifact,
)
from measurement_service import interpret_measurement
from opportunity_contracts import (
    BriefDraftArtifact,
    BusinessInterpretationArtifact,
    CriticReportArtifact,
    OpportunityAgentRequest,
    OpportunityAngleSetArtifact,
    StrategyArtifact,
)
from opportunity_service import HANDLERS
from planning_contracts import (
    AudienceAgentRequest,
    AudienceDefinitionSetArtifact,
    InventoryIntelligenceAgentRequest,
    InventoryShortlistDraftArtifact,
    MediaMixDraftArtifact,
    MediaPlanningAgentRequest,
)
from planning_service import (
    interpret_inventory,
    propose_audiences,
    propose_media_mix,
)
from proposal_contracts import (
    ProposalNarrativeAgentRequest,
    ProposalNarrativeDraftArtifact,
)
from proposal_service import propose_narrative

DETERMINISTIC_MODE = "deterministic"

ArtifactType = type[BaseModel]
Handler = Callable[[BaseModel], BaseModel]


OPPORTUNITY_ARTIFACTS: dict[AgentCode, ArtifactType] = {
    AgentCode.BUSINESS_INTERPRETATION: BusinessInterpretationArtifact,
    AgentCode.OPPORTUNITY_INTELLIGENCE: OpportunityAngleSetArtifact,
    AgentCode.STRATEGY: StrategyArtifact,
    AgentCode.CRITIC_READINESS: CriticReportArtifact,
    AgentCode.BRIEF_DRAFTING: BriefDraftArtifact,
}

TRANSCRIPTION_INSTRUCTION = (
    "Transcribe sellable inventory source facts from only the bounded source "
    "items and attached images. This is source transcription, not semantic "
    "classification. Extract every visible commercial table row as a distinct "
    "candidate. Preserve exact supplier, platform, ad unit, format, dimensions, "
    "rate text and non-governed source terms. Do not return channel, product "
    "type, description, currency, rate type, availability or commercial dates "
    "during transcription; deterministic parsing, human review or stage-two "
    "enrichment owns those fields. Use the exact source locator for every field "
    "and copy raw_value verbatim. Set normalized_value to null for every field. "
    "Mark every field SUPPLIER_SUPPLIED. Never infer geography, missing digits "
    "or any other commercial meaning. A price does not establish FLAT_RATE or "
    "any buying basis. A file "
    "name, file timestamp, current date or document metadata does not establish "
    "commercial validity. If a rate such as R1,10 is visibly incomplete or "
    "ambiguous, preserve it exactly and add an ambiguity note. Do not repair it. "
    "A Platform cell is the candidate name. An Ad Unit cell is placement, never "
    "an address. Do not invent product codes. Do not create candidates from a "
    "logo alone, but transcribe an explicitly named supplier or brand and attach "
    "explicitly matching positioning copy to the relevant products. Every "
    "candidate must contain name or product_code and no field may repeat. "
    "For structured output, fields is an object keyed once by field name, not "
    "a list or a set of columns. Create one candidate object per physical data "
    "row; never flatten multiple rows into one candidate. Do not emit headings "
    "or labels such as a rate-card title, Streaming, Ad Unit, Width, Format or "
    "Rate as row values. The Platform data cell is name and the Ad Unit data "
    "cell is placement; never map Ad Unit to address. "
    "Account for every attached image by citing it or listing its locator in "
    "omitted_source_locators. Return only the requested artifact; deterministic "
    "code owns status, governance and acceptance."
)

ENRICHMENT_INSTRUCTION = (
    "Enrich only the deterministic inventory rows supplied in existing_rows. "
    "Do not create inventory and do not repeat, replace or reinterpret supplier, "
    "price, currency, buying basis, validity, availability, geography, format, "
    "placement, dimensions or other commercial facts. Each candidate "
    "source_locator must exactly equal the target existing-row locator. You may "
    "return only channel, product_type and description fields. Mark every field "
    "DERIVED_POLICY. For channel and product_type, normalized_value must be one "
    "exact code from governed_codes. For description, raw_value must be a "
    "verbatim source excerpt and normalized_value may be concise searchable copy "
    "that does not add facts. Streaming and YouTube inventory may classify as "
    "DIGITAL when the source supports it. Never infer FLAT_RATE or any rate_type. "
    "Never add dates. Omit a field when evidence is insufficient. Account for "
    "every attached image by citing it or listing its locator in "
    "omitted_source_locators. Return only the requested artifact; deterministic "
    "code owns status, governance and acceptance."
)

INSTRUCTIONS: dict[AgentCode, str] = {
    AgentCode.BUSINESS_INTERPRETATION: (
        "Interpret the business from approved evidence only."
    ),
    AgentCode.OPPORTUNITY_INTELLIGENCE: (
        "Propose ranked commercial opportunity angles grounded in approved "
        "evidence."
    ),
    AgentCode.STRATEGY: (
        "Propose a growth strategy from approved evidence and selected prior "
        "artefacts."
    ),
    AgentCode.CRITIC_READINESS: (
        "Critique the proposed strategy and identify material evidence gaps."
    ),
    AgentCode.BRIEF_DRAFTING: (
        "Draft the canonical campaign brief proposal without inventing missing "
        "facts."
    ),
    AgentCode.AUDIENCE: (
        "Propose evidence-labelled audience definitions and positioning."
    ),
    AgentCode.MEDIA_PLANNING: (
        "Propose a budget-reconciled media mix using only the allowed channels."
    ),
    AgentCode.INVENTORY_INTELLIGENCE: (
        "Explain supplied deterministic inventory eligibility and benchmark "
        "facts without changing them."
    ),
    AgentCode.PROPOSAL_NARRATIVE: (
        "Draft proposal wording that preserves every supplied commercial fact "
        "exactly."
    ),
    AgentCode.CREATIVE: (
        "Propose creative territories using only rights-cleared assets and "
        "verified facts."
    ),
    AgentCode.MEASUREMENT: (
        "Interpret reviewed performance evidence without unsupported causality."
    ),
}


def execute_agent(
    agent_code: AgentCode,
    body: bytes,
    runtime_mode: str,
) -> dict[str, object]:
    request, artifact_type, deterministic = _contract(agent_code, body)
    policy = request.invocation.provider_policy  # type: ignore[attr-defined]
    if runtime_mode == DETERMINISTIC_MODE:
        if policy.provider != "deterministic":
            raise HTTPException(
                status_code=503,
                detail="Requested provider is not enabled.",
            )
        output = deterministic(request)
        _validate_operation_output(request, output)
        return output.model_dump(mode="json")
    if runtime_mode == BEDROCK_MODE:
        if policy.provider != "bedrock":
            raise HTTPException(
                status_code=503,
                detail="Requested provider is not enabled.",
            )
        if (
            isinstance(request, InventorySemanticAgentRequest)
            and request.operation == SOURCE_TRANSCRIPTION
        ):
            raise HTTPException(
                status_code=503,
                detail=(
                    "Inventory source transcription must be completed by "
                    "deterministic extraction or human review."
                ),
            )
        instruction = (
            _semantic_instruction(request)
            if isinstance(request, InventorySemanticAgentRequest)
            else INSTRUCTIONS[agent_code]
        )
        try:
            output = generate_with_bedrock(
                agent_code,
                request,
                artifact_type,
                instruction,
            )
            try:
                _validate_operation_output(request, output)
            except ValueError as error:
                raise BedrockProviderError(
                    str(error),
                    stage="GROUNDING_VALIDATION",
                    acceptance="ACCEPTED",
                    usage=output.usage,
                    rejected_output=output.model_dump(
                        mode="json",
                        exclude={"usage"},
                    ),
                ) from error
        except BedrockProviderError as error:
            raise HTTPException(
                status_code=503,
                detail=error.detail(),
            ) from error
        return output.model_dump(mode="json")
    raise HTTPException(
        status_code=503,
        detail="Agent runtime provider is disabled.",
    )


def implemented_agents() -> set[AgentCode]:
    return {
        *HANDLERS,
        AgentCode.AUDIENCE,
        AgentCode.MEDIA_PLANNING,
        AgentCode.INVENTORY_INTELLIGENCE,
        AgentCode.PROPOSAL_NARRATIVE,
        AgentCode.CREATIVE,
        AgentCode.MEASUREMENT,
    }


def _contract(
    agent_code: AgentCode,
    body: bytes,
) -> tuple[BaseModel, ArtifactType, Handler]:
    if agent_code in OPPORTUNITY_ARTIFACTS:
        request = _validate(OpportunityAgentRequest, body)
        _require_agent_match(request.invocation.agent_code, agent_code)
        return (
            request,
            OPPORTUNITY_ARTIFACTS[agent_code],
            HANDLERS[agent_code],
        )  # type: ignore[return-value]
    if agent_code == AgentCode.AUDIENCE:
        request = _validate(AudienceAgentRequest, body)
        _require_agent_match(request.invocation.agent_code, agent_code)
        return (
            request,
            AudienceDefinitionSetArtifact,
            propose_audiences,
        )  # type: ignore[return-value]
    if agent_code == AgentCode.MEDIA_PLANNING:
        request = _validate(MediaPlanningAgentRequest, body)
        _require_agent_match(request.invocation.agent_code, agent_code)
        return (
            request,
            MediaMixDraftArtifact,
            propose_media_mix,
        )  # type: ignore[return-value]
    if agent_code == AgentCode.INVENTORY_INTELLIGENCE:
        if _operation(body) in {
            SOURCE_TRANSCRIPTION,
            SEMANTIC_ENRICHMENT,
        }:
            request = _validate(InventorySemanticAgentRequest, body)
            _require_agent_match(request.invocation.agent_code, agent_code)
            return (
                request,
                InventorySemanticExtractionArtifact,
                propose_semantic_extraction,
            )  # type: ignore[return-value]
        request = _validate(InventoryIntelligenceAgentRequest, body)
        _require_agent_match(request.invocation.agent_code, agent_code)
        return (
            request,
            InventoryShortlistDraftArtifact,
            interpret_inventory,
        )  # type: ignore[return-value]
    if agent_code == AgentCode.PROPOSAL_NARRATIVE:
        request = _validate(ProposalNarrativeAgentRequest, body)
        _require_agent_match(request.invocation.agent_code, agent_code)
        return (
            request,
            ProposalNarrativeDraftArtifact,
            propose_narrative,
        )  # type: ignore[return-value]
    if agent_code == AgentCode.CREATIVE:
        request = _validate(CreativeAgentRequest, body)
        _require_agent_match(request.invocation.agent_code, agent_code)
        return (
            request,
            CreativeConceptSetArtifact,
            generate_creative_concepts,
        )  # type: ignore[return-value]
    if agent_code == AgentCode.MEASUREMENT:
        request = _validate(MeasurementAgentRequest, body)
        _require_agent_match(request.invocation.agent_code, agent_code)
        return (
            request,
            MeasurementInterpretationArtifact,
            interpret_measurement,
        )  # type: ignore[return-value]
    raise HTTPException(
        status_code=404,
        detail="Agent is not implemented.",
    )


def _semantic_instruction(
    request: InventorySemanticAgentRequest,
) -> str:
    return (
        TRANSCRIPTION_INSTRUCTION
        if request.operation == SOURCE_TRANSCRIPTION
        else ENRICHMENT_INSTRUCTION
    )


def _validate_operation_output(
    request: BaseModel,
    output: BaseModel,
) -> None:
    if isinstance(request, InventorySemanticAgentRequest):
        validate_semantic_grounding(
            request,
            output,  # type: ignore[arg-type]
        )


def _operation(body: bytes) -> str | None:
    try:
        payload = json.loads(body)
    except (json.JSONDecodeError, UnicodeDecodeError):
        return None
    return payload.get("operation") if isinstance(payload, dict) else None


def _validate(model_type, body: bytes):
    try:
        return model_type.model_validate_json(body)
    except ValidationError as error:
        raise HTTPException(
            status_code=422,
            detail=error.errors(
                include_input=False,
                include_context=False,
            ),
        ) from error


def _require_agent_match(
    contract_code: AgentCode,
    route_code: AgentCode,
) -> None:
    if contract_code != route_code:
        raise HTTPException(
            status_code=400,
            detail="Agent contract does not match the route.",
        )

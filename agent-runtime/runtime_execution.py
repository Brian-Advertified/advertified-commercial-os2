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
from inventory_schema_contracts import (
    OPERATION as SCHEMA_DISCOVERY, SchemaDiscoveryRequest, InventorySchemaProposal,
)
from inventory_schema_service import (
    INSTRUCTION as SCHEMA_INSTRUCTION, unavailable_schema_discovery, validate_schema_grounding,
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
            SCHEMA_INSTRUCTION if isinstance(request, SchemaDiscoveryRequest) else ENRICHMENT_INSTRUCTION
            if isinstance(request, InventorySemanticAgentRequest)
            else INSTRUCTIONS[agent_code]
        )
        output = _grounded_bedrock_output(agent_code, request, artifact_type, instruction)
        return output.model_dump(mode="json")
    raise HTTPException(
        status_code=503,
        detail="Agent runtime provider is disabled.",
    )


def _grounded_bedrock_output(agent_code, request, artifact_type, instruction):
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
    return output


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
        types = (OpportunityAgentRequest, OPPORTUNITY_ARTIFACTS[agent_code], HANDLERS[agent_code])
    elif agent_code == AgentCode.INVENTORY_INTELLIGENCE:
        types = _inventory_contract_types(body)
    else:
        types = {
            AgentCode.AUDIENCE: (AudienceAgentRequest, AudienceDefinitionSetArtifact, propose_audiences),
            AgentCode.MEDIA_PLANNING: (MediaPlanningAgentRequest, MediaMixDraftArtifact, propose_media_mix),
            AgentCode.PROPOSAL_NARRATIVE: (ProposalNarrativeAgentRequest, ProposalNarrativeDraftArtifact, propose_narrative),
            AgentCode.CREATIVE: (CreativeAgentRequest, CreativeConceptSetArtifact, generate_creative_concepts),
            AgentCode.MEASUREMENT: (MeasurementAgentRequest, MeasurementInterpretationArtifact, interpret_measurement),
        }.get(agent_code)
    if types is None:
        raise HTTPException(status_code=404, detail="Agent is not implemented.")
    request_type, artifact_type, handler = types
    request = _validate(request_type, body)
    _require_agent_match(request.invocation.agent_code, agent_code)
    return request, artifact_type, handler


def _inventory_contract_types(body: bytes):
    operation = _operation(body)
    if operation == SCHEMA_DISCOVERY:
        return SchemaDiscoveryRequest, InventorySchemaProposal, unavailable_schema_discovery
    if operation in {SOURCE_TRANSCRIPTION, SEMANTIC_ENRICHMENT}:
        return InventorySemanticAgentRequest, InventorySemanticExtractionArtifact, propose_semantic_extraction
    return InventoryIntelligenceAgentRequest, InventoryShortlistDraftArtifact, interpret_inventory


def _validate_operation_output(
    request: BaseModel,
    output: BaseModel,
) -> None:
    if isinstance(request, InventorySemanticAgentRequest):
        validate_semantic_grounding(
            request,
            output,  # type: ignore[arg-type]
        )
    elif isinstance(request, SchemaDiscoveryRequest):
        validate_schema_grounding(request, output)


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

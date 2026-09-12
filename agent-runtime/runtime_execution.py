"""Route one typed agent request to deterministic or Bedrock execution."""

from __future__ import annotations

import json
import logging
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
from inventory_processing_control import ensure_inventory_processing
from inventory_strategy import INTERPRETATION_INSTRUCTION
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
from location_intelligence_contracts import (
    RESEARCH_OPERATION as LOCATION_RESEARCH,
    SYNTHESIS_OPERATION as LOCATION_SYNTHESIS,
    LocationIntelligenceArtifact,
    LocationResearchPlanArtifact,
    LocationResearchPlanRequest,
    LocationSynthesisRequest,
)
from location_intelligence_service import (
    RESEARCH_INSTRUCTION as LOCATION_RESEARCH_INSTRUCTION,
    SYNTHESIS_INSTRUCTION as LOCATION_SYNTHESIS_INSTRUCTION,
    propose_location_research,
    synthesize_location,
)
from location_research_validation import (
    canonicalize_location_research,
    validate_location_research_grounding,
)
from location_intelligence_validation import (
    canonicalize_location_synthesis,
    validate_location_synthesis_grounding,
)
from market_intelligence_contracts import MarketIntelligenceAgentRequest, MarketIntelligenceArtifact
from market_intelligence_service import (
    INSTRUCTION as MARKET_INTELLIGENCE_INSTRUCTION,
    canonicalize_market_intelligence,
    propose_market_intelligence,
    validate_market_grounding,
)
from media_strategy_contracts import OPERATION as MEDIA_STRATEGY_ANALYSIS, MediaStrategyArtifact, MediaStrategyRequest
from media_strategy_service import (
    INSTRUCTION as MEDIA_STRATEGY_INSTRUCTION,
    canonicalize_media_strategy,
    unavailable_media_strategy,
    validate_media_strategy_grounding,
)
from opportunity_contracts import OpportunityAgentRequest
from opportunity_service import HANDLERS
from planning_contracts import (
    AudienceAgentRequest,
    AudienceDefinitionSetArtifact,
    InventoryIntelligenceAgentRequest,
    InventoryShortlistDraftArtifact,
)
from planning_inventory_interpretation import interpret_inventory
from planning_service import (
    canonicalize_audiences,
    propose_audiences,
    validate_audience_grounding,
)
from proposal_contracts import (
    ProposalNarrativeAgentRequest,
    ProposalNarrativeDraftArtifact,
)
from proposal_service import propose_narrative
from proposal_provider_facts import proposal_model_input, validate_proposal_numeric_claims
from inventory_model_input import _inventory_model_input
from supplied_brief_contracts import OPERATION as SUPPLIED_BRIEF, SuppliedBriefRequest, SuppliedBriefArtifact
from supplied_brief_model_input import build_model_input as build_brief_model_input
from supplied_brief_service import (
    INSTRUCTION as SUPPLIED_BRIEF_INSTRUCTION, canonicalize_grounding as canonicalize_brief_grounding,
    unavailable_fixture, validate_source as validate_brief_source,
    validate_grounding as validate_brief_grounding,
)

logger = logging.getLogger(__name__)

DETERMINISTIC_MODE = "deterministic"

Handler = Callable[[BaseModel], BaseModel]

from audience_model_input import audience_model_input
from runtime_instructions import (
    ENRICHMENT_INSTRUCTION,
    INSTRUCTIONS,
    OPPORTUNITY_ARTIFACTS,
    TRANSCRIPTION_INSTRUCTION,
)


def execute_agent(
    agent_code: AgentCode,
    body: bytes,
    runtime_mode: str,
) -> dict[str, object]:
    request, artifact_type, deterministic = _contract(agent_code, body)
    if isinstance(request, SuppliedBriefRequest):
        try:
            validate_brief_source(request)
        except ValueError as error:
            raise HTTPException(422, str(error)) from error
    if isinstance(request, (SchemaDiscoveryRequest, InventorySemanticAgentRequest)):
        ensure_inventory_processing()
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
        instruction = (
            SUPPLIED_BRIEF_INSTRUCTION if isinstance(request, SuppliedBriefRequest) else
            LOCATION_RESEARCH_INSTRUCTION if isinstance(request, LocationResearchPlanRequest) else
            LOCATION_SYNTHESIS_INSTRUCTION if isinstance(request, LocationSynthesisRequest) else
            MEDIA_STRATEGY_INSTRUCTION if isinstance(request, MediaStrategyRequest) else
            SCHEMA_INSTRUCTION if isinstance(request, SchemaDiscoveryRequest) else ENRICHMENT_INSTRUCTION
            if isinstance(request, InventorySemanticAgentRequest)
            and request.operation == SEMANTIC_ENRICHMENT else TRANSCRIPTION_INSTRUCTION
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
        model_input = (
            build_brief_model_input(request)
            if isinstance(request, SuppliedBriefRequest)
            else proposal_model_input(request)
            if isinstance(request, ProposalNarrativeAgentRequest)
            else audience_model_input(request)
            if isinstance(request, AudienceAgentRequest)
            else _inventory_model_input(request)
            if isinstance(request, InventoryIntelligenceAgentRequest)
            else None
        )
        output = generate_with_bedrock(
            agent_code,
            request,
            artifact_type,
            instruction,
            model_input=model_input,
        )
        provider_output = output
        try:
            output = _canonicalize_operation_output(request, output)
            _validate_operation_output(request, output)
        except ValueError as error:
            raise BedrockProviderError(
                str(error),
                stage="GROUNDING_VALIDATION",
                acceptance="ACCEPTED",
                usage=provider_output.usage,
                rejected_output=provider_output.model_dump(
                    mode="json", exclude={"usage"},
                ),
            ) from error
    except BedrockProviderError as error:
        logger.warning(
            "Bedrock provider rejected output: stage=%s acceptance=%s",
            error.stage,
            error.acceptance,
        )
        raise HTTPException(
            status_code=503,
            detail=error.detail(),
        ) from error
    return output


def _canonicalize_operation_output(request, output):
    if isinstance(request, SuppliedBriefRequest):
        return canonicalize_brief_grounding(request, output)
    if isinstance(request, AudienceAgentRequest):
        return canonicalize_audiences(request, output)
    if isinstance(request, MarketIntelligenceAgentRequest):
        return canonicalize_market_intelligence(request, output)
    if isinstance(request, LocationResearchPlanRequest):
        return canonicalize_location_research(request, output)
    if isinstance(request, LocationSynthesisRequest):
        return canonicalize_location_synthesis(request, output)
    if isinstance(request, MediaStrategyRequest):
        return canonicalize_media_strategy(request, output)
    return output


def implemented_agents() -> set[AgentCode]:
    return {
        *HANDLERS,
        AgentCode.MARKET_INTELLIGENCE,
        AgentCode.AUDIENCE_INTELLIGENCE,
        AgentCode.LOCATION_INTELLIGENCE,
        AgentCode.MEDIA_STRATEGY,
        AgentCode.INVENTORY_INTELLIGENCE,
        AgentCode.PROPOSAL_NARRATIVE,
        AgentCode.CREATIVE,
        AgentCode.MEASUREMENT,
    }


def _contract(
    agent_code: AgentCode,
    body: bytes,
) -> tuple[BaseModel, ArtifactType, Handler]:
    if agent_code == AgentCode.BRIEF_DRAFTING and _operation(body) == SUPPLIED_BRIEF:
        types = (SuppliedBriefRequest, SuppliedBriefArtifact, unavailable_fixture)
    elif agent_code in OPPORTUNITY_ARTIFACTS:
        types = (OpportunityAgentRequest, OPPORTUNITY_ARTIFACTS[agent_code], HANDLERS[agent_code])
    elif agent_code == AgentCode.INVENTORY_INTELLIGENCE:
        types = _inventory_contract_types(body)
    elif agent_code == AgentCode.LOCATION_INTELLIGENCE:
        types = _location_contract_types(body)
    elif agent_code == AgentCode.MEDIA_STRATEGY and _operation(body) == MEDIA_STRATEGY_ANALYSIS:
        types = (MediaStrategyRequest, MediaStrategyArtifact, unavailable_media_strategy)
    else:
        types = {
            AgentCode.MARKET_INTELLIGENCE: (MarketIntelligenceAgentRequest, MarketIntelligenceArtifact, propose_market_intelligence),
            AgentCode.AUDIENCE_INTELLIGENCE: (AudienceAgentRequest, AudienceDefinitionSetArtifact, propose_audiences),
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


def _location_contract_types(body: bytes):
    operation = _operation(body)
    if operation == LOCATION_RESEARCH:
        return LocationResearchPlanRequest, LocationResearchPlanArtifact, propose_location_research
    if operation == LOCATION_SYNTHESIS:
        return LocationSynthesisRequest, LocationIntelligenceArtifact, synthesize_location
    return None


def _inventory_contract_types(body: bytes):
    operation = _operation(body)
    if operation == SCHEMA_DISCOVERY:
        return SchemaDiscoveryRequest, InventorySchemaProposal, unavailable_schema_discovery
    if operation in {SOURCE_TRANSCRIPTION, SEMANTIC_ENRICHMENT}:
        return InventorySemanticAgentRequest, InventorySemanticExtractionArtifact, propose_semantic_extraction
    return InventoryIntelligenceAgentRequest, InventoryShortlistDraftArtifact, interpret_inventory


def _validate_operation_output(request: BaseModel, output: BaseModel) -> None:
    if isinstance(request, InventorySemanticAgentRequest):
        validate_semantic_grounding(
            request,
            output,  # type: ignore[arg-type]
        )
    elif isinstance(request, SchemaDiscoveryRequest):
        validate_schema_grounding(request, output)
    elif isinstance(request, SuppliedBriefRequest):
        validate_brief_grounding(request, output)
    elif isinstance(request, MarketIntelligenceAgentRequest):
        validate_market_grounding(request, output)  # type: ignore[arg-type]
    elif isinstance(request, AudienceAgentRequest):
        validate_audience_grounding(request, output)  # type: ignore[arg-type]
    elif isinstance(request, LocationResearchPlanRequest):
        validate_location_research_grounding(request, output)  # type: ignore[arg-type]
    elif isinstance(request, LocationSynthesisRequest):
        validate_location_synthesis_grounding(request, output)  # type: ignore[arg-type]
    elif isinstance(request, ProposalNarrativeAgentRequest):
        validate_proposal_numeric_claims(request, output)
    elif isinstance(request, MediaStrategyRequest):
        validate_media_strategy_grounding(request, output)  # type: ignore[arg-type]


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
        details = error.errors(
            include_input=False,
            include_context=False,
        )
        safe_locations = "; ".join(
            ".".join(str(part) for part in item["loc"]) + ":" + item["type"]
            for item in details[:20]
        )
        logger.warning("Agent request failed typed validation: %s", safe_locations)
        raise HTTPException(
            status_code=422,
            detail=details,
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

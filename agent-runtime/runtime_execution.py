"""Route one typed agent request to deterministic or Bedrock proposal execution."""

from __future__ import annotations

from collections.abc import Callable

from fastapi import HTTPException
from pydantic import BaseModel, ValidationError

from agent_registry import AgentCode
from bedrock_provider import BEDROCK_MODE, BedrockProviderError, generate_with_bedrock
from creative_contracts import CreativeAgentRequest, CreativeConceptSetArtifact
from creative_service import generate_creative_concepts
from measurement_contracts import MeasurementAgentRequest, MeasurementInterpretationArtifact
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
from planning_service import interpret_inventory, propose_audiences, propose_media_mix
from proposal_contracts import ProposalNarrativeAgentRequest, ProposalNarrativeDraftArtifact
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

INSTRUCTIONS: dict[AgentCode, str] = {
    AgentCode.BUSINESS_INTERPRETATION: "Interpret the business from approved evidence only.",
    AgentCode.OPPORTUNITY_INTELLIGENCE: (
        "Propose ranked commercial opportunity angles grounded in approved evidence."
    ),
    AgentCode.STRATEGY: (
        "Propose a growth strategy from approved evidence and selected prior artefacts."
    ),
    AgentCode.CRITIC_READINESS: (
        "Critique the proposed strategy and identify material evidence gaps."
    ),
    AgentCode.BRIEF_DRAFTING: (
        "Draft the canonical campaign brief proposal without inventing missing facts."
    ),
    AgentCode.AUDIENCE: "Propose evidence-labelled audience definitions and positioning.",
    AgentCode.MEDIA_PLANNING: (
        "Propose a budget-reconciled media mix using only the allowed channels."
    ),
    AgentCode.INVENTORY_INTELLIGENCE: (
        "Explain supplied deterministic inventory eligibility and benchmark facts "
        "without changing them."
    ),
    AgentCode.PROPOSAL_NARRATIVE: (
        "Draft proposal wording that preserves every supplied commercial fact exactly."
    ),
    AgentCode.CREATIVE: (
        "Propose creative territories using only rights-cleared assets and verified facts."
    ),
    AgentCode.MEASUREMENT: (
        "Interpret reviewed performance evidence without unsupported causality."
    ),
}


def execute_agent(agent_code: AgentCode, body: bytes, runtime_mode: str) -> dict[str, object]:
    request, artifact_type, deterministic = _contract(agent_code, body)
    policy = request.invocation.provider_policy  # type: ignore[attr-defined]
    if runtime_mode == DETERMINISTIC_MODE:
        if policy.provider != "deterministic":
            raise HTTPException(status_code=503, detail="Requested provider is not enabled.")
        return deterministic(request).model_dump(mode="json")
    if runtime_mode == BEDROCK_MODE:
        if policy.provider != "bedrock":
            raise HTTPException(status_code=503, detail="Requested provider is not enabled.")
        try:
            output = generate_with_bedrock(
                agent_code,
                request,
                artifact_type,
                INSTRUCTIONS[agent_code],
            )
        except BedrockProviderError as error:
            raise HTTPException(status_code=503, detail=str(error)) from error
        return output.model_dump(mode="json")
    raise HTTPException(status_code=503, detail="Agent runtime provider is disabled.")


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
        return request, OPPORTUNITY_ARTIFACTS[agent_code], HANDLERS[agent_code]  # type: ignore[return-value]
    if agent_code == AgentCode.AUDIENCE:
        request = _validate(AudienceAgentRequest, body)
        _require_agent_match(request.invocation.agent_code, agent_code)
        return request, AudienceDefinitionSetArtifact, propose_audiences  # type: ignore[return-value]
    if agent_code == AgentCode.MEDIA_PLANNING:
        request = _validate(MediaPlanningAgentRequest, body)
        _require_agent_match(request.invocation.agent_code, agent_code)
        return request, MediaMixDraftArtifact, propose_media_mix  # type: ignore[return-value]
    if agent_code == AgentCode.INVENTORY_INTELLIGENCE:
        request = _validate(InventoryIntelligenceAgentRequest, body)
        _require_agent_match(request.invocation.agent_code, agent_code)
        return request, InventoryShortlistDraftArtifact, interpret_inventory  # type: ignore[return-value]
    if agent_code == AgentCode.PROPOSAL_NARRATIVE:
        request = _validate(ProposalNarrativeAgentRequest, body)
        _require_agent_match(request.invocation.agent_code, agent_code)
        return request, ProposalNarrativeDraftArtifact, propose_narrative  # type: ignore[return-value]
    if agent_code == AgentCode.CREATIVE:
        request = _validate(CreativeAgentRequest, body)
        _require_agent_match(request.invocation.agent_code, agent_code)
        return request, CreativeConceptSetArtifact, generate_creative_concepts  # type: ignore[return-value]
    if agent_code == AgentCode.MEASUREMENT:
        request = _validate(MeasurementAgentRequest, body)
        _require_agent_match(request.invocation.agent_code, agent_code)
        return request, MeasurementInterpretationArtifact, interpret_measurement  # type: ignore[return-value]
    raise HTTPException(status_code=404, detail="Agent is not implemented.")


def _validate(model_type, body: bytes):
    try:
        return model_type.model_validate_json(body)
    except ValidationError as error:
        raise HTTPException(
            status_code=422,
            detail=error.errors(include_input=False, include_context=False),
        ) from error


def _require_agent_match(contract_code: AgentCode, route_code: AgentCode) -> None:
    if contract_code != route_code:
        raise HTTPException(status_code=400, detail="Agent contract does not match the route.")

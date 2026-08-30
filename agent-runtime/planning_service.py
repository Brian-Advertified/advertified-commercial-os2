"""Zero-cost Audience and Media Planning proposals from approved Brief inputs."""

from __future__ import annotations

from decimal import Decimal
from uuid import UUID

from contracts import (
    AgentOutputEnvelope,
    ConfidenceAssessment,
    EvidenceBinding,
    OutputStatus,
    PlanningAssumption,
    ProviderUsage,
    SuggestedNextAction,
    UnknownItem,
)
from master_data_codes import EvidenceClassifications
from planning_contracts import (
    AudienceAgentRequest,
    AudienceDefinition,
    AudienceDefinitionSetArtifact,
    MediaAllocation,
    MediaMixDraftArtifact,
    MediaPlanningAgentRequest,
)


def propose_audiences(
    request: AudienceAgentRequest,
) -> AgentOutputEnvelope[AudienceDefinitionSetArtifact]:
    evidence_ids = request.invocation.approved_evidence_item_ids
    classification = (
        EvidenceClassifications.INFERENCE.value
        if evidence_ids else EvidenceClassifications.HYPOTHESIS.value
    )
    audiences = tuple(
        _audience(request, name, classification, evidence_ids)
        for name in request.planning.audiences
    )
    names = ", ".join(item.name for item in audiences)
    markets = ", ".join(request.planning.geographies)
    artifact = AudienceDefinitionSetArtifact(
        audiences=audiences,
        targeting_rationale=(
            f"Prioritise {names} in {markets} because the approved Brief identifies "
            "those audiences and markets for the stated objective."
        ),
        positioning_statement=(
            f"For {names}, position the advertised offer as a credible route to "
            f"{request.planning.objective.lower()}."
        ),
    )
    return AgentOutputEnvelope(
        schema_version="1.0.0",
        status=OutputStatus.COMPLETED,
        artifact=artifact,
        evidence_bindings=_bindings(evidence_ids, "artifact.audiences"),
        unknowns=(UnknownItem(
            field_path="artifact.audiences.buying_context",
            question="Which buying contexts should planning validate?",
            is_blocking=False,
        ),),
        assumptions=(),
        confidence=(ConfidenceAssessment(
            field_path="artifact.audiences",
            confidence=Decimal("0.70") if evidence_ids else Decimal("0.45"),
        ),),
        objections=(),
        rationale=(
            "Audience proposals use only the approved Brief and keep unsupported detail unknown."
        ),
        suggested_next_action=SuggestedNextAction(
            command_code="GenerateMediaMix",
            requires_human=False,
        ),
        usage=_usage(),
    )


def propose_media_mix(
    request: MediaPlanningAgentRequest,
) -> AgentOutputEnvelope[MediaMixDraftArtifact]:
    channels = tuple(sorted(set(request.planning.available_channels)))[:3]
    allocations = _allocate(request.planning.budget_minor, channels)
    artifact = MediaMixDraftArtifact(
        allocations=allocations,
        assumptions=(
            "Channel allocation is a planning hypothesis pending human plan review.",
        ),
    )
    evidence_ids = request.invocation.approved_evidence_item_ids
    return AgentOutputEnvelope(
        schema_version="1.0.0",
        status=OutputStatus.COMPLETED,
        artifact=artifact,
        evidence_bindings=_bindings(evidence_ids, "artifact.allocations"),
        unknowns=(UnknownItem(
            field_path="artifact.allocations.reach_baseline",
            question="What verified reach or response baseline is available?",
            is_blocking=False,
        ),),
        assumptions=(PlanningAssumption(
            field_path="artifact.allocations",
            value="Allocate evenly across up to three approved channels.",
            impact="The planner must review channel contribution before approval.",
            validation_needed="Reconcile against verified inventory and forecast evidence.",
        ),),
        confidence=(ConfidenceAssessment(
            field_path="artifact.allocations",
            confidence=Decimal("0.55"),
        ),),
        objections=(),
        rationale=(
            "The proposal uses only allowed channels and reconciles exactly to the Brief budget."
        ),
        suggested_next_action=SuggestedNextAction(
            command_code="ReviewMediaMix",
            requires_human=True,
        ),
        usage=_usage(),
    )


def _audience(
    request: AudienceAgentRequest,
    name: str,
    classification: str,
    evidence_ids: tuple[UUID, ...],
) -> AudienceDefinition:
    return AudienceDefinition(
        name=name,
        description=f"People described by the approved Brief as {name}.",
        need_state=request.planning.objective,
        buying_context="Buying context is not supplied and remains a planning question.",
        geographies=request.planning.geographies,
        language=None,
        life_stage=None,
        lsm_sem=None,
        classification=classification,
        exclusions=("Do not infer sensitive individual attributes.",),
        evidence_item_ids=evidence_ids,
        confidence=Decimal("0.70") if evidence_ids else Decimal("0.45"),
        is_target=True,
    )


def _allocate(budget: int, channels: tuple[str, ...]) -> tuple[MediaAllocation, ...]:
    even, remainder = divmod(budget, len(channels))
    return tuple(
        MediaAllocation(
            channel=channel,
            budget_minor=even + (remainder if index == 0 else 0),
            role="Primary response channel" if index == 0 else "Supporting reach channel",
        )
        for index, channel in enumerate(channels)
    )


def _bindings(evidence_ids: tuple[UUID, ...], path: str) -> tuple[EvidenceBinding, ...]:
    return () if not evidence_ids else (EvidenceBinding(
        field_path=path,
        evidence_item_ids=evidence_ids,
    ),)


def _usage() -> ProviderUsage:
    return ProviderUsage(
        provider="deterministic",
        model="fixture-v1",
        units=0,
        tool_calls=0,
        incremental_cost_minor=0,
        cache_status="FIXTURE",
    )

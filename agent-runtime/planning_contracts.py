"""Strict Audience and Media Planning agent contracts."""

from __future__ import annotations

from decimal import Decimal
from typing import Annotated
from uuid import UUID

from pydantic import Field, model_validator

from contracts import AgentInvocationEnvelope, ContractModel, StableCode


class PlanningBriefContext(ContractModel):
    brief_version_id: UUID
    objective: Annotated[str, Field(min_length=1, max_length=4_000)]
    audiences: Annotated[tuple[str, ...], Field(min_length=1, max_length=20)]
    geographies: Annotated[tuple[str, ...], Field(min_length=1, max_length=50)]


class AudienceAgentRequest(ContractModel):
    invocation: AgentInvocationEnvelope
    planning: PlanningBriefContext

    @model_validator(mode="after")
    def validate_brief_reference(self) -> AudienceAgentRequest:
        _require_brief_reference(self.invocation, self.planning.brief_version_id)
        return self


class MediaPlanningContext(PlanningBriefContext):
    budget_minor: Annotated[int, Field(ge=0)]
    currency: Annotated[str, Field(pattern=r"^[A-Z]{3}$")]
    available_channels: Annotated[tuple[StableCode, ...], Field(min_length=1)]


class MediaPlanningAgentRequest(ContractModel):
    invocation: AgentInvocationEnvelope
    planning: MediaPlanningContext

    @model_validator(mode="after")
    def validate_brief_reference(self) -> MediaPlanningAgentRequest:
        _require_brief_reference(self.invocation, self.planning.brief_version_id)
        return self


class AudienceDefinition(ContractModel):
    name: Annotated[str, Field(min_length=1, max_length=300)]
    description: Annotated[str, Field(min_length=1, max_length=2_000)]
    need_state: Annotated[str, Field(min_length=1, max_length=1_000)]
    buying_context: Annotated[str, Field(min_length=1, max_length=1_000)]
    geographies: tuple[str, ...]
    language: str | None
    life_stage: str | None
    lsm_sem: str | None
    classification: StableCode
    exclusions: tuple[str, ...]
    evidence_item_ids: tuple[UUID, ...]
    confidence: Annotated[Decimal, Field(ge=0, le=1)]
    is_target: bool


class AudienceDefinitionSetArtifact(ContractModel):
    audiences: Annotated[tuple[AudienceDefinition, ...], Field(min_length=1)]
    targeting_rationale: Annotated[str, Field(min_length=1, max_length=4_000)]
    positioning_statement: Annotated[str, Field(min_length=1, max_length=4_000)]


class MediaAllocation(ContractModel):
    channel: StableCode
    budget_minor: Annotated[int, Field(ge=0)]
    role: Annotated[str, Field(min_length=1, max_length=1_000)]


class MediaMixDraftArtifact(ContractModel):
    allocations: Annotated[tuple[MediaAllocation, ...], Field(min_length=1)]
    assumptions: tuple[str, ...]


def _require_brief_reference(invocation: AgentInvocationEnvelope, brief_id: UUID) -> None:
    if not any(
        reference.resource_type == "BriefVersion" and reference.resource_id == brief_id
        for reference in invocation.resource_refs
    ):
        raise ValueError("The exact BriefVersion resource reference is required.")

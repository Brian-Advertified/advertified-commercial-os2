"""Typed contracts for first-class Media Strategy Intelligence.

Media Strategy is intentionally upstream of inventory. It recommends channel roles and
trade-offs from the commercial problem and approved strategic context; it does not select
supplier inventory or pretend an execution worksheet is strategy.
"""

from __future__ import annotations

from decimal import Decimal
from typing import Annotated, Literal
from uuid import UUID

from pydantic import Field, field_validator, model_validator

from contracts import AgentInvocationEnvelope, ContractModel, StableCode
from planning_contracts import AudienceClassification

OPERATION = "MEDIA_STRATEGY_ANALYSIS"

# One vocabulary for the provider schema and the existing grounding validator.
UNSUPPORTED_CLAIM_MARKERS = (
    "audience is likely to be found", "high-traffic", "high traffic",
    "maximize reach", "maximise reach", "enhance campaign impact", "guaranteed reach",
    "verified footfall", "proven cpm", "proven effectiveness", "inventory availability",
    "supplier rate", "specific placement",
)


class MediaStrategyAudienceInput(ContractModel):
    segment_id: UUID
    name: Annotated[str, Field(min_length=1, max_length=300)]
    description: Annotated[str, Field(min_length=1, max_length=2_000)]
    geographies: tuple[Annotated[str, Field(min_length=1, max_length=300)], ...] = ()
    classification: AudienceClassification


class MediaStrategyLocationInput(ContractModel):
    name: Annotated[str, Field(min_length=1, max_length=500)]
    geography: Annotated[str, Field(min_length=1, max_length=500)]
    rationale: Annotated[str, Field(min_length=1, max_length=2_000)]
    classification: Literal["INFERENCE", "HYPOTHESIS"]


class MediaStrategyContext(ContractModel):
    brief_version_id: UUID
    audience_artifact_id: UUID
    audience_artifact_version: int
    location_artifact_id: UUID | None = None
    location_artifact_version: int | None = None
    client_name: Annotated[str, Field(min_length=1, max_length=500)]
    business_problem: Annotated[str, Field(min_length=1, max_length=4_000)]
    objective: Annotated[str, Field(min_length=1, max_length=4_000)]
    geographies: Annotated[tuple[str, ...], Field(min_length=1, max_length=50)]
    media_requirements: tuple[Annotated[str, Field(min_length=1, max_length=1_000)], ...] = ()
    constraints: tuple[Annotated[str, Field(min_length=1, max_length=1_000)], ...] = ()
    success_measures: tuple[Annotated[str, Field(min_length=1, max_length=1_000)], ...] = ()
    budget_minor: Annotated[int | None, Field(ge=0)] = None
    currency: Annotated[str | None, Field(pattern=r"^[A-Z]{3}$")] = None
    available_channels: Annotated[tuple[StableCode, ...], Field(min_length=1)]
    target_audiences: Annotated[tuple[MediaStrategyAudienceInput, ...], Field(min_length=1)]
    location_opportunities: tuple[MediaStrategyLocationInput, ...] = ()
    audience_unknowns: tuple[Annotated[str, Field(min_length=1, max_length=1_000)], ...] = ()
    location_evidence_gaps: tuple[Annotated[str, Field(min_length=1, max_length=1_000)], ...] = ()

    @model_validator(mode="after")
    def validate_location_version(self) -> "MediaStrategyContext":
        if (self.location_artifact_id is None) != (self.location_artifact_version is None):
            raise ValueError("Location artifact identity and version must be supplied together.")
        return self


class MediaStrategyRequest(ContractModel):
    operation: Literal["MEDIA_STRATEGY_ANALYSIS"]
    invocation: AgentInvocationEnvelope
    media_strategy: MediaStrategyContext

    @model_validator(mode="after")
    def validate_resources(self) -> "MediaStrategyRequest":
        _require_reference(
            self.invocation,
            "BriefVersion",
            self.media_strategy.brief_version_id,
        )
        _require_reference(
            self.invocation,
            "IntelligenceArtifact",
            self.media_strategy.audience_artifact_id,
            self.media_strategy.audience_artifact_version,
        )
        if self.media_strategy.location_artifact_id is not None:
            _require_reference(
                self.invocation,
                "IntelligenceArtifact",
                self.media_strategy.location_artifact_id,
                self.media_strategy.location_artifact_version,
            )
        return self


class MediaChannelRecommendation(ContractModel):
    channel: StableCode
    role: Annotated[str, Field(min_length=1, max_length=1_000)]
    rationale: Annotated[str, Field(min_length=1, max_length=2_000)]
    objective_contribution: Annotated[str, Field(min_length=1, max_length=1_000)]
    geography_role: Annotated[str | None, Field(max_length=1_000)] = None
    classification: Literal["INFERENCE", "HYPOTHESIS"]
    budget_guidance_percent: Annotated[Decimal | None, Field(ge=0, le=100)] = None
    trade_offs: tuple[Annotated[str, Field(min_length=1, max_length=1_000)], ...] = ()
    evidence_gaps: tuple[Annotated[str, Field(min_length=1, max_length=1_000)], ...] = ()

    @field_validator("budget_guidance_percent", mode="before")
    @classmethod
    def normalize_null_budget_guidance(cls, value):
        if value is None:
            return None
        if isinstance(value, str) and value.strip().casefold() == "null":
            return None
        if isinstance(value, (int, float, str)):
            return Decimal(str(value))
        return value


class MediaStrategyArtifact(ContractModel):
    summary: Annotated[str, Field(min_length=1, max_length=4_000)]
    channel_recommendations: Annotated[tuple[MediaChannelRecommendation, ...], Field(max_length=20)]
    strategic_principles: tuple[Annotated[str, Field(min_length=1, max_length=1_000)], ...] = ()
    excluded_channels: tuple[StableCode, ...] = ()
    evidence_gaps: tuple[Annotated[str, Field(min_length=1, max_length=1_000)], ...] = ()


def _require_reference(
    invocation: AgentInvocationEnvelope,
    resource_type: str,
    resource_id: UUID,
    version: int | None = None,
) -> None:
    if not any(
        item.resource_type == resource_type
        and item.resource_id == resource_id
        and (version is None or item.version == version)
        for item in invocation.resource_refs
    ):
        raise ValueError(f"The exact {resource_type} resource reference is required.")

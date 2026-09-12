"""Typed contracts for Location Intelligence research and synthesis."""

from __future__ import annotations

from decimal import Decimal
from typing import Annotated, Literal
from uuid import UUID

from pydantic import Field, model_validator

from contracts import AgentInvocationEnvelope, ContractModel, StableCode
from reference_evidence import ReferenceObservation
from supplied_brief_contracts import BriefConflict

RESEARCH_OPERATION = "LOCATION_RESEARCH_PLAN"
SYNTHESIS_OPERATION = "LOCATION_SYNTHESIS"


class LocationAudienceSegment(ContractModel):
    segment_id: UUID
    name: Annotated[str, Field(min_length=1, max_length=300)]
    description: Annotated[str, Field(min_length=1, max_length=2_000)]
    geographies: tuple[Annotated[str, Field(min_length=1, max_length=300)], ...]
    classification: StableCode
    evidence_item_ids: tuple[UUID, ...] = ()
    reference_observation_ids: tuple[UUID, ...] = ()
    is_target: bool


class LocationPoiCategoryOption(ContractModel):
    code: StableCode
    label: Annotated[str, Field(min_length=1, max_length=200)]


class LocationCommercialContext(ContractModel):
    brief_version_id: UUID
    audience_artifact_id: UUID
    audience_artifact_version: int
    client_name: Annotated[str, Field(min_length=1, max_length=500)]
    business_problem: Annotated[str, Field(min_length=1, max_length=4_000)]
    objective: Annotated[str, Field(min_length=1, max_length=4_000)]
    geographies: Annotated[tuple[str, ...], Field(min_length=1, max_length=50)]
    media_requirements: tuple[Annotated[str, Field(min_length=1, max_length=1_000)], ...] = ()
    constraints: tuple[Annotated[str, Field(min_length=1, max_length=1_000)], ...] = ()
    conflicts: tuple[BriefConflict, ...] = ()
    target_segments: tuple[LocationAudienceSegment, ...] = ()
    reference_evidence: tuple[ReferenceObservation, ...] = ()
    available_poi_categories: Annotated[tuple[LocationPoiCategoryOption, ...], Field(min_length=1)]


class LocationResearchPlanRequest(ContractModel):
    operation: Literal["LOCATION_RESEARCH_PLAN"]
    invocation: AgentInvocationEnvelope
    location: LocationCommercialContext

    @model_validator(mode="after")
    def validate_resources(self) -> LocationResearchPlanRequest:
        _require_reference(self.invocation, "BriefVersion", self.location.brief_version_id)
        _require_reference(
            self.invocation,
            "IntelligenceArtifact",
            self.location.audience_artifact_id,
            self.location.audience_artifact_version,
        )
        return self


class PlaceResearchQuery(ContractModel):
    poi_category: StableCode
    anchor_geography: Annotated[str, Field(min_length=2, max_length=300)]
    purpose: Annotated[str, Field(min_length=1, max_length=1_000)]
    priority: Literal["REQUIRED", "PREFERRED"]
    classification: Literal["INFERENCE", "HYPOTHESIS"]
    reference_observation_ids: tuple[UUID, ...] = ()


class LocationResearchPlanArtifact(ContractModel):
    queries: Annotated[tuple[PlaceResearchQuery, ...], Field(max_length=8)]
    rationale: Annotated[str, Field(min_length=1, max_length=4_000)]
    evidence_gaps: tuple[Annotated[str, Field(min_length=1, max_length=1_000)], ...] = ()


class ResolvedPlace(ContractModel):
    place_id: Annotated[str, Field(min_length=1, max_length=300)]
    query: Annotated[str, Field(min_length=2, max_length=200)]
    purpose: Annotated[str, Field(min_length=1, max_length=1_000)]
    name: Annotated[str, Field(min_length=1, max_length=500)]
    address: Annotated[str, Field(min_length=1, max_length=1_000)]
    latitude: Annotated[Decimal, Field(ge=-90, le=90)]
    longitude: Annotated[Decimal, Field(ge=-180, le=180)]
    source_locator: Annotated[str, Field(min_length=1, max_length=2_000)]
    attribution: Annotated[str, Field(min_length=1, max_length=500)]
    geometry_basis: Annotated[str, Field(min_length=1, max_length=100)]


class LocationSynthesisContext(LocationCommercialContext):
    research_plan: LocationResearchPlanArtifact
    resolved_places: tuple[ResolvedPlace, ...] = ()


class LocationSynthesisRequest(ContractModel):
    operation: Literal["LOCATION_SYNTHESIS"]
    invocation: AgentInvocationEnvelope
    location: LocationSynthesisContext

    @model_validator(mode="after")
    def validate_resources(self) -> LocationSynthesisRequest:
        _require_reference(self.invocation, "BriefVersion", self.location.brief_version_id)
        _require_reference(
            self.invocation,
            "IntelligenceArtifact",
            self.location.audience_artifact_id,
            self.location.audience_artifact_version,
        )
        return self


class LocationOpportunityArea(ContractModel):
    name: Annotated[str, Field(min_length=1, max_length=500)]
    geography: Annotated[str, Field(min_length=1, max_length=500)]
    rationale: Annotated[str, Field(min_length=1, max_length=2_000)]
    classification: Literal["INFERENCE", "HYPOTHESIS"]
    place_ids: tuple[Annotated[str, Field(min_length=1, max_length=300)], ...] = ()
    reference_observation_ids: tuple[UUID, ...] = ()
    confidence: Annotated[Decimal, Field(ge=0, le=1)] | None = None
    evidence_gaps: tuple[Annotated[str, Field(min_length=1, max_length=1_000)], ...] = ()


class LocationSynthesisProposal(ContractModel):
    summary: Annotated[str, Field(min_length=1, max_length=4_000)]
    opportunities: Annotated[tuple[LocationOpportunityArea, ...], Field(max_length=20)]
    evidence_gaps: tuple[Annotated[str, Field(min_length=1, max_length=1_000)], ...] = ()


class LocationIntelligenceArtifact(LocationSynthesisProposal):
    research_queries: tuple[PlaceResearchQuery, ...]
    resolved_places: tuple[ResolvedPlace, ...]


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

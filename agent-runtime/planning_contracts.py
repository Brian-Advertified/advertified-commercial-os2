"""Strict Audience, Media Planning and Inventory Intelligence contracts."""

from __future__ import annotations

from decimal import Decimal
from typing import Annotated, Literal
from uuid import UUID

from pydantic import Field, NonNegativeInt, field_validator, model_validator

from contracts import AgentInvocationEnvelope, ContractModel, StableCode
from master_data_codes import EvidenceClassifications
from inventory_strategy_contracts import InventoryStrategyContext
from audience_evidence import AudienceEvidenceFact
from reference_evidence import ReferenceObservation
from supplied_brief_contracts import BriefConflict
from buy_assessment_contracts import InventoryBuyAssessmentFacts


class PlanningBriefContext(ContractModel):
    brief_version_id: UUID
    client_name: Annotated[str, Field(min_length=1, max_length=500)]
    business_problem: Annotated[str, Field(min_length=1, max_length=4_000)]
    objective: Annotated[str, Field(min_length=1, max_length=4_000)]
    audiences: Annotated[tuple[str, ...], Field(max_length=20)]
    geographies: Annotated[tuple[str, ...], Field(min_length=1, max_length=50)]
    media_requirements: tuple[Annotated[str, Field(min_length=1, max_length=1_000)], ...] = ()
    constraints: tuple[Annotated[str, Field(min_length=1, max_length=1_000)], ...] = ()
    conflicts: tuple[BriefConflict, ...] = ()
    success_measures: tuple[Annotated[str, Field(min_length=1, max_length=1_000)], ...] = ()
    budget_minor: Annotated[int | None, Field(ge=0)] = None
    currency: Annotated[str | None, Field(pattern=r"^[A-Z]{3}$")] = None
    audience_evidence: tuple[AudienceEvidenceFact, ...] | None = None
    reference_evidence: tuple[ReferenceObservation, ...] = ()


class AudienceAgentRequest(ContractModel):
    invocation: AgentInvocationEnvelope
    planning: PlanningBriefContext

    @model_validator(mode="after")
    def validate_brief_reference(self) -> AudienceAgentRequest:
        _require_resource_reference(
            self.invocation, "BriefVersion", self.planning.brief_version_id
        )
        return self


class InventoryBenchmarkFacts(ContractModel):
    policy_version: StableCode
    geography_basis: Annotated[str, Field(min_length=1, max_length=500)]
    cohort_size: NonNegativeInt
    median_minor: Annotated[int | None, Field(ge=0)] = None
    percentile: Annotated[Decimal | None, Field(ge=0, le=100)] = None
    position: StableCode
    confidence: Annotated[Decimal, Field(ge=0, le=1)]
    exclusions: tuple[Annotated[str, Field(min_length=1, max_length=500)], ...]


class InventoryDeliveryMeasurementFacts(ContractModel):
    metric_type: StableCode
    value: Annotated[Decimal | None, Field(ge=0)] = None
    unit: StableCode | None = None
    universe: str | None = None
    measurement_source: str | None = None
    measurement_period: str | None = None
    methodology: str | None = None
    limitations: str | None = None


class InventoryAudienceFitFacts(ContractModel):
    language_score: Annotated[Decimal | None, Field(ge=0, le=1)] = None
    life_stage_score: Annotated[Decimal | None, Field(ge=0, le=1)] = None
    lsm_sem_score: Annotated[Decimal | None, Field(ge=0, le=1)] = None
    evidence_gaps: tuple[Annotated[str, Field(min_length=1, max_length=200)], ...]
    measurement_source: str | None = None
    measurement_period: str | None = None
    methodology: str | None = None
    taxonomy_name: str | None = None
    taxonomy_version: str | None = None
    delivery_measurements: tuple[InventoryDeliveryMeasurementFacts, ...] = ()
    delivery_evidence_gaps: tuple[
        Annotated[str, Field(min_length=1, max_length=200)], ...
    ] = ()


class InventorySuitabilityFacts(ContractModel):
    buy_assessment: InventoryBuyAssessmentFacts | None = None
    policy_version: StableCode
    geography: Annotated[Decimal, Field(ge=0, le=1)]
    audience_context: Annotated[Decimal, Field(ge=0, le=1)]
    objective_format: Annotated[Decimal, Field(ge=0, le=1)]
    budget_efficiency: Annotated[Decimal, Field(ge=0, le=1)]
    evidence_quality_freshness: Annotated[Decimal, Field(ge=0, le=1)]
    portfolio_coverage_diversity: Annotated[Decimal, Field(ge=0, le=1)]
    total: Annotated[Decimal, Field(ge=0, le=1)]
    evidence_gaps: tuple[Annotated[str, Field(min_length=1, max_length=200)], ...]


class InventoryCandidateFacts(ContractModel):
    candidate_id: UUID
    product_version_id: UUID
    name: Annotated[str, Field(min_length=1, max_length=300)]
    channel: StableCode
    geography: Annotated[str, Field(min_length=1, max_length=500)]
    rate_amount_minor: Annotated[int | None, Field(ge=0)] = None
    currency: Annotated[str | None, Field(pattern=r"^[A-Z]{3}$")] = None
    is_eligible: bool
    rejection_reason: StableCode | None = None
    rejection_detail: Annotated[str | None, Field(min_length=1, max_length=1_000)] = None
    score: Annotated[Decimal | None, Field(ge=0, le=1)] = None
    audience_fit: InventoryAudienceFitFacts
    suitability: InventorySuitabilityFacts
    benchmark: InventoryBenchmarkFacts | None = None

    @model_validator(mode="after")
    def validate_eligibility_facts(self) -> InventoryCandidateFacts:
        if self.is_eligible:
            if (
                self.rate_amount_minor is None
                or self.currency is None
                or self.score is None
                or self.score != self.suitability.total
                or self.rejection_reason is not None
                or self.rejection_detail is not None
            ):
                raise ValueError(
                    "Eligible inventory requires rate, currency and the matching governed "
                    "suitability score without rejection facts."
                )
        elif (
            self.rejection_reason is None
            or self.rejection_detail is None
            or self.score is not None
            or self.suitability.total != 0
        ):
            raise ValueError(
                "Ineligible inventory requires its deterministic rejection, zero "
                "suitability and no score."
            )
        return self


class InventoryIntelligenceContext(ContractModel):
    brief_version_id: UUID
    shortlist_version_id: UUID
    candidates: Annotated[tuple[InventoryCandidateFacts, ...], Field(min_length=1)]
    strategy: InventoryStrategyContext

    @model_validator(mode="after")
    def validate_unique_candidates(self) -> InventoryIntelligenceContext:
        ids = [item.candidate_id for item in self.candidates]
        if len(ids) != len(set(ids)):
            raise ValueError("Inventory candidate IDs must be unique.")
        return self


class InventoryIntelligenceAgentRequest(ContractModel):
    operation: Literal["INVENTORY_INTERPRETATION"] = "INVENTORY_INTERPRETATION"
    invocation: AgentInvocationEnvelope
    inventory: InventoryIntelligenceContext

    @model_validator(mode="after")
    def validate_exact_resources(self) -> InventoryIntelligenceAgentRequest:
        _require_resource_reference(
            self.invocation, "BriefVersion", self.inventory.brief_version_id
        )
        _require_resource_reference(
            self.invocation,
            "InventoryShortlistVersion",
            self.inventory.shortlist_version_id,
        )
        if strategy := self.inventory.strategy:
            for resource_type, resource_id, version in (
                ("IntelligenceArtifact", strategy.audience_artifact_id, strategy.audience_artifact_version),
                ("IntelligenceArtifact", strategy.media_strategy_artifact_id, strategy.media_strategy_artifact_version),
                ("MediaMixVersion", strategy.media_mix_version_id, strategy.media_mix_version),
            ):
                if not any(item.resource_type == resource_type and item.resource_id == resource_id
                           and item.version == version for item in self.invocation.resource_refs):
                    raise ValueError(f"The exact approved {resource_type} version is required.")
        return self


AudienceClassification = Literal[
    EvidenceClassifications.CLIENT_REQUIREMENT,
    EvidenceClassifications.INFERENCE,
    EvidenceClassifications.HYPOTHESIS,
]


class AudienceDefinition(ContractModel):
    name: Annotated[str, Field(min_length=1, max_length=300)]
    description: Annotated[str, Field(min_length=1, max_length=2_000)]
    need_state: Annotated[str | None, Field(max_length=1_000)] = None
    buying_context: Annotated[str | None, Field(max_length=1_000)] = None
    geographies: tuple[str, ...]
    language: str | None
    life_stage: str | None
    lsm_sem: str | None
    lsm_sem_taxonomy: str | None = None
    lsm_sem_taxonomy_version: str | None = None
    lsm_sem_mandatory: bool = False
    classification: AudienceClassification
    exclusions: tuple[str, ...]
    evidence_item_ids: tuple[UUID, ...]
    reference_observation_ids: tuple[UUID, ...] = ()
    confidence: Annotated[Decimal, Field(ge=0, le=1)] | None = None
    is_target: bool

    @field_validator(
        "need_state", "buying_context", "language", "life_stage", "lsm_sem",
        "lsm_sem_taxonomy", "lsm_sem_taxonomy_version", "confidence",
        mode="before",
    )
    @classmethod
    def normalize_nullable_wire_value(cls, value):
        if isinstance(value, str) and value.strip().casefold() == "null":
            return None
        if isinstance(value, (int, float)) and not isinstance(value, bool):
            return Decimal(str(value))
        return value


class AudienceDefinitionSetArtifact(ContractModel):
    audiences: Annotated[tuple[AudienceDefinition, ...], Field(max_length=20)]
    targeting_rationale: Annotated[str | None, Field(max_length=4_000)] = None
    positioning_statement: Annotated[str | None, Field(max_length=4_000)] = None


class InventoryCandidateInterpretation(ContractModel):
    candidate_id: UUID
    rationale: Annotated[str, Field(min_length=1, max_length=1_000)]
    classification: StableCode


class InventoryShortlistDraftArtifact(ContractModel):
    interpretations: Annotated[
        tuple[InventoryCandidateInterpretation, ...],
        Field(min_length=1),
    ]


def _require_resource_reference(
    invocation: AgentInvocationEnvelope,
    resource_type: str,
    resource_id: UUID,
) -> None:
    if not any(
        reference.resource_type == resource_type
        and reference.resource_id == resource_id
        for reference in invocation.resource_refs
    ):
        raise ValueError(f"The exact {resource_type} resource reference is required.")

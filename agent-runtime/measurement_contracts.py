"""Strict Measurement agent input and interpretation contracts."""

from __future__ import annotations

from datetime import date
from decimal import Decimal
from typing import Annotated, Literal
from uuid import UUID

from pydantic import Field, PositiveInt, model_validator

from contracts import AgentInvocationEnvelope, AgentOutputEnvelope, ContractModel, StableCode


class DeliveryProofReference(ContractModel):
    id: UUID
    version: PositiveInt


class PerformanceMetricFact(ContractModel):
    id: UUID
    evidence_set_id: UUID
    metric_type: StableCode
    value: Annotated[Decimal, Field(ge=0)]
    unit: StableCode
    period_start: date
    period_end: date
    source_locator: Annotated[str, Field(min_length=1, max_length=500)]

    @model_validator(mode="after")
    def validate_period(self) -> PerformanceMetricFact:
        if self.period_end < self.period_start:
            raise ValueError("A metric period cannot end before it starts.")
        return self


class PerformanceEvidenceFactSet(ContractModel):
    id: UUID
    version: PositiveInt
    quality_status: Literal["VERIFIED", "LIMITED"]
    methodology: Annotated[str, Field(min_length=1, max_length=2_000)]
    limitations: Annotated[tuple[str, ...], Field(min_length=1, max_length=20)]
    metrics: Annotated[tuple[PerformanceMetricFact, ...], Field(min_length=1, max_length=100)]

    @model_validator(mode="after")
    def validate_metrics(self) -> PerformanceEvidenceFactSet:
        if any(metric.evidence_set_id != self.id for metric in self.metrics):
            raise ValueError("Every metric must reference its supplied evidence set.")
        return self


class MeasurementContext(ContractModel):
    campaign_id: UUID
    campaign_version: PositiveInt
    measurement_plan: Annotated[tuple[str, ...], Field(min_length=1, max_length=50)]
    delivery_proofs: Annotated[
        tuple[DeliveryProofReference, ...], Field(min_length=1, max_length=100)
    ]
    evidence_sets: Annotated[
        tuple[PerformanceEvidenceFactSet, ...], Field(min_length=1, max_length=100)
    ]


class MeasurementAgentRequest(ContractModel):
    invocation: AgentInvocationEnvelope
    measurement: MeasurementContext

    @model_validator(mode="after")
    def validate_exact_inputs(self) -> MeasurementAgentRequest:
        references = {
            (reference.resource_type, reference.resource_id): reference.version
            for reference in self.invocation.resource_refs
        }
        context = self.measurement
        if references.get(("Campaign", context.campaign_id)) != context.campaign_version:
            raise ValueError("The exact Campaign reference is required.")
        if any(
            references.get(("DeliveryProof", proof.id)) != proof.version
            for proof in context.delivery_proofs
        ):
            raise ValueError("Every DeliveryProof requires its exact resource reference.")
        if any(
            references.get(("PerformanceEvidence", evidence.id)) != evidence.version
            for evidence in context.evidence_sets
        ):
            raise ValueError("Every evidence set requires its exact resource reference.")
        metric_ids = [
            metric.id
            for evidence in context.evidence_sets
            for metric in evidence.metrics
        ]
        if len(set(metric_ids)) != len(metric_ids):
            raise ValueError("Metric identifiers must be unique.")
        if set(metric_ids) != set(self.invocation.approved_evidence_item_ids):
            raise ValueError(
                "The invocation must approve every and only supplied metric identifier."
            )
        return self


class MeasurementFinding(ContractModel):
    title: Annotated[str, Field(min_length=1, max_length=300)]
    summary: Annotated[str, Field(min_length=1, max_length=1_000)]
    metric_ids: Annotated[tuple[UUID, ...], Field(min_length=1, max_length=100)]
    causality_status: Literal["NOT_ESTABLISHED"]


class LearningProposal(ContractModel):
    text: Annotated[str, Field(min_length=1, max_length=1_000)]
    requires_new_approval: Literal[True]


class MeasurementInterpretationArtifact(ContractModel):
    executive_summary: Annotated[str, Field(min_length=1, max_length=2_000)]
    findings: Annotated[tuple[MeasurementFinding, ...], Field(min_length=1, max_length=100)]
    limitations: Annotated[tuple[str, ...], Field(min_length=1, max_length=2_000)]
    learning_proposals: Annotated[tuple[LearningProposal, ...], Field(min_length=1, max_length=20)]
    causality_status: Literal["NOT_ESTABLISHED"]


MeasurementAgentResponse = AgentOutputEnvelope[MeasurementInterpretationArtifact]

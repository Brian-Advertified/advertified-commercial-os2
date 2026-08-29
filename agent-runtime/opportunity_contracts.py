"""Strict Gate 4 opportunity-agent request and artifact contracts."""

from __future__ import annotations

from decimal import Decimal
from typing import Annotated
from uuid import UUID

from pydantic import Field

from contracts import AgentInvocationEnvelope, ContractModel


class ApprovedEvidenceItem(ContractModel):
    id: UUID
    claim_type: Annotated[str, Field(min_length=1, max_length=100)]
    structured_value: dict[str, object]
    excerpt: Annotated[str, Field(min_length=1, max_length=2_000)]


class OpportunityContext(ContractModel):
    id: UUID
    title: Annotated[str, Field(min_length=1, max_length=200)]
    problem_summary: Annotated[str, Field(max_length=2_000)] | None = None
    objective_summary: Annotated[str, Field(max_length=2_000)] | None = None


class PriorArtifact(ContractModel):
    artifact_type: Annotated[str, Field(min_length=1, max_length=100)]
    artifact_id: UUID
    version: Annotated[int, Field(gt=0)]
    value: dict[str, object]


class OpportunityAgentRequest(ContractModel):
    invocation: AgentInvocationEnvelope
    opportunity: OpportunityContext
    approved_evidence: Annotated[tuple[ApprovedEvidenceItem, ...], Field(min_length=1)]
    prior_artifacts: tuple[PriorArtifact, ...] = ()


class BusinessInterpretationArtifact(ContractModel):
    offering: str
    customer_groups: tuple[str, ...]
    buying_occasions: tuple[str, ...]
    geographies: tuple[str, ...]
    commercial_context: str


class OpportunityAngle(ContractModel):
    rank: Annotated[int, Field(gt=0)]
    title: Annotated[str, Field(min_length=1, max_length=300)]
    rationale: Annotated[str, Field(min_length=1, max_length=2_000)]
    evidence_item_ids: Annotated[tuple[UUID, ...], Field(min_length=1)]
    confidence: Annotated[Decimal, Field(ge=0, le=1)]


class OpportunityAngleSetArtifact(ContractModel):
    angles: Annotated[tuple[OpportunityAngle, ...], Field(min_length=2, max_length=3)]


class StrategyArtifact(ContractModel):
    diagnosis: str
    growth_thesis: str
    objectives: tuple[str, ...]
    audience_hypotheses: tuple[str, ...]
    proposition: str
    message: str
    channel_implications: tuple[str, ...]
    risks: tuple[str, ...]


class CriticReportArtifact(ContractModel):
    readiness: Annotated[str, Field(pattern=r"^(READY|REVIEW_REQUIRED)$")]
    summary: Annotated[str, Field(min_length=1, max_length=1_000)]

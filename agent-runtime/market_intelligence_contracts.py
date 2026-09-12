"""Strict Market Intelligence request and artifact contracts."""

from __future__ import annotations

from decimal import Decimal
from typing import Annotated
from uuid import UUID

from pydantic import Field, model_validator

from contracts import AgentInvocationEnvelope, ContractModel, StableCode
from master_data_codes import EvidenceClassifications
from opportunity_contracts import ApprovedEvidenceItem
from supplied_brief_contracts import BriefConflict


class MarketIntelligenceContext(ContractModel):
    brief_version_id: UUID
    client_name: Annotated[str, Field(min_length=1, max_length=500)]
    business_problem: Annotated[str, Field(min_length=1, max_length=4_000)]
    objective: Annotated[str, Field(min_length=1, max_length=4_000)]
    audiences: tuple[Annotated[str, Field(min_length=1, max_length=500)], ...]
    geographies: tuple[Annotated[str, Field(min_length=1, max_length=500)], ...]
    media_requirements: tuple[Annotated[str, Field(min_length=1, max_length=1_000)], ...] = ()
    constraints: tuple[Annotated[str, Field(min_length=1, max_length=1_000)], ...] = ()
    conflicts: tuple[BriefConflict, ...] = ()
    success_measures: tuple[Annotated[str, Field(min_length=1, max_length=1_000)], ...] = ()
    budget_minor: Annotated[int | None, Field(ge=0)] = None
    currency: Annotated[str | None, Field(pattern=r"^[A-Z]{3}$")] = None


class MarketIntelligenceAgentRequest(ContractModel):
    invocation: AgentInvocationEnvelope
    market: MarketIntelligenceContext
    approved_evidence: Annotated[tuple[ApprovedEvidenceItem, ...], Field(max_length=100)] = ()

    @model_validator(mode="after")
    def validate_brief_reference(self) -> "MarketIntelligenceAgentRequest":
        if not any(
            item.resource_type == "BriefVersion"
            and item.resource_id == self.market.brief_version_id
            for item in self.invocation.resource_refs
        ):
            raise ValueError("The exact BriefVersion resource is required.")
        evidence_ids = [item.id for item in self.approved_evidence]
        if len(evidence_ids) != len(set(evidence_ids)) or not set(evidence_ids).issubset(
            self.invocation.approved_evidence_item_ids
        ):
            raise ValueError("Market evidence must be distinct and approved for this invocation.")
        return self


class MarketFinding(ContractModel):
    title: Annotated[str, Field(min_length=1, max_length=300)]
    finding: Annotated[str, Field(min_length=1, max_length=2_000)]
    commercial_implication: Annotated[str, Field(min_length=1, max_length=2_000)]
    classification: EvidenceClassifications
    evidence_item_ids: tuple[UUID, ...] = ()
    confidence: Annotated[Decimal | None, Field(ge=0, le=1)] = None


class MarketOpportunity(ContractModel):
    title: Annotated[str, Field(min_length=1, max_length=300)]
    rationale: Annotated[str, Field(min_length=1, max_length=2_000)]
    required_evidence: tuple[Annotated[str, Field(min_length=1, max_length=500)], ...] = ()
    priority: StableCode


class MarketIntelligenceArtifact(ContractModel):
    category_situation: Annotated[str, Field(min_length=1, max_length=4_000)]
    findings: Annotated[tuple[MarketFinding, ...], Field(max_length=20)]
    opportunities: Annotated[tuple[MarketOpportunity, ...], Field(max_length=10)]
    strategic_implications: Annotated[tuple[Annotated[str, Field(min_length=1, max_length=1_000)], ...], Field(min_length=1, max_length=12)]
    evidence_gaps: tuple[Annotated[str, Field(min_length=1, max_length=1_000)], ...] = ()

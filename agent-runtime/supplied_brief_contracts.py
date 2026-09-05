"""Source-first brief understanding through the existing brief-drafting agent boundary."""

from decimal import Decimal
from typing import Annotated, Literal

from pydantic import Field

from contracts import AgentInvocationEnvelope, ContractModel

OPERATION = "SUPPLIED_BRIEF_UNDERSTANDING"
Text = Annotated[str, Field(max_length=4000)]


class Clarification(ContractModel):
    field_path: Annotated[str, Field(min_length=1, max_length=200)]
    value: Text


class SuppliedSource(ContractModel):
    source_title: Annotated[str, Field(min_length=1, max_length=300)]
    source_content: Annotated[str, Field(min_length=1, max_length=262144)]
    source_hash: Annotated[str, Field(pattern=r"^[a-f0-9]{64}$")]
    clarifications: tuple[Clarification, ...]


class SuppliedBriefRequest(ContractModel):
    operation: Literal["SUPPLIED_BRIEF_UNDERSTANDING"]
    invocation: AgentInvocationEnvelope
    source: SuppliedSource


class BriefQuestion(ContractModel):
    field_path: Text
    question: Text
    is_blocking: bool
    options: tuple[Text, ...]


class BriefEvidence(ContractModel):
    field_path: Text
    kind: Text
    excerpt: Text
    confidence: Annotated[Decimal, Field(ge=0, le=1)]
    source_locator: Text


class BriefUnknown(ContractModel):
    field_path: Text
    question: Text
    is_blocking: bool


class BriefAssumption(ContractModel):
    field_path: Text
    value: Text
    impact: Text
    validation_needed: Text


class BriefConflict(ContractModel):
    field_path: Text
    description: Text
    severity: Text
    resolved: bool
    resolution: Text | None


class SuppliedDraft(ContractModel):
    business_problem: Text
    objective: Text
    audiences: tuple[Text, ...]
    geographies: tuple[Text, ...]
    timing: Text
    budget_minor: Annotated[int | None, Field(ge=0)]
    budget_unknown: bool
    currency: Annotated[str | None, Field(pattern=r"^[A-Z]{3}$")]
    vat_status: Text | None
    fees_minor: Annotated[int | None, Field(ge=0)]
    media_requirements: tuple[Text, ...]
    constraints: tuple[Text, ...]
    measurement: tuple[Text, ...]
    facts: tuple[Text, ...]
    unknowns: tuple[BriefUnknown, ...]
    assumptions: tuple[BriefAssumption, ...]
    conflicts: tuple[BriefConflict, ...]


class SuppliedBriefArtifact(ContractModel):
    source_hash: Annotated[str, Field(pattern=r"^[a-f0-9]{64}$")]
    client_name: Text | None
    title: Annotated[str, Field(min_length=1, max_length=300)]
    campaign_mode: Literal["OOH_ONLY", "FULL_CAMPAIGN"] | None
    campaign_mode_confidence: Annotated[Decimal, Field(ge=0, le=1)]
    requires_human_clarification: bool
    campaign_mode_rationale: Text
    draft: SuppliedDraft
    questions: tuple[BriefQuestion, ...]
    evidence: tuple[BriefEvidence, ...]

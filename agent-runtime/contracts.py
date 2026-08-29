"""Versioned provider-neutral agent contracts."""

from __future__ import annotations

from decimal import Decimal
from enum import StrEnum
from typing import Annotated, Generic, Literal, TypeVar
from uuid import UUID

from pydantic import BaseModel, ConfigDict, Field, NonNegativeInt, PositiveInt, model_validator

from agent_registry import AgentCode

StableCode = Annotated[
    str,
    Field(min_length=1, max_length=100, pattern=r"^[A-Za-z0-9][A-Za-z0-9._:-]*$"),
]
VersionCode = Annotated[
    str,
    Field(pattern=r"^[1-9][0-9]*\.[0-9]+\.[0-9]+$"),
]
ArtifactT = TypeVar("ArtifactT")


class ContractModel(BaseModel):
    model_config = ConfigDict(extra="forbid", frozen=True, strict=True)


class ResourceReference(ContractModel):
    resource_type: StableCode
    resource_id: UUID
    version: PositiveInt


class ToolPolicy(ContractModel):
    allowed_tools: tuple[StableCode, ...]
    max_tool_calls: NonNegativeInt
    consequence_policy: Literal["PROPOSE_ONLY"]


class ProviderPolicy(ContractModel):
    provider: Literal["deterministic"]
    model: Literal["fixture-v1"]
    temperature: Literal[0]
    timeout_seconds: PositiveInt
    max_attempts: PositiveInt
    cost_cap_minor: Literal[0]
    allow_live: Literal[False]


class ResumeContext(ContractModel):
    checkpoint_id: UUID | None = None
    prior_validated_output_ref: ResourceReference | None = None
    prior_usage_ref: ResourceReference | None = None


class AgentInvocationEnvelope(ContractModel):
    schema_version: Literal["1.0.0"]
    tenant_id: UUID
    actor_id: UUID
    effective_role: StableCode
    run_id: UUID
    step_id: UUID
    correlation_id: UUID
    agent_code: AgentCode
    contract_version: VersionCode
    prompt_version: VersionCode
    resource_refs: Annotated[tuple[ResourceReference, ...], Field(min_length=1)]
    approved_evidence_item_ids: tuple[UUID, ...]
    locale: StableCode
    account_policy_version: VersionCode
    tool_policy: ToolPolicy
    provider_policy: ProviderPolicy
    resume: ResumeContext = ResumeContext()


class OutputStatus(StrEnum):
    COMPLETED = "COMPLETED"
    REVIEW_REQUIRED = "REVIEW_REQUIRED"
    FAILED = "FAILED"


class EvidenceBinding(ContractModel):
    field_path: StableCode
    evidence_item_ids: Annotated[tuple[UUID, ...], Field(min_length=1)]


class UnknownItem(ContractModel):
    field_path: StableCode
    question: Annotated[str, Field(min_length=1, max_length=500)]
    is_blocking: bool


class PlanningAssumption(ContractModel):
    field_path: StableCode
    value: Annotated[str, Field(min_length=1, max_length=500)]
    impact: Annotated[str, Field(min_length=1, max_length=500)]
    validation_needed: Annotated[str, Field(min_length=1, max_length=500)]


class ConfidenceAssessment(ContractModel):
    field_path: StableCode
    confidence: Annotated[Decimal, Field(ge=0, le=1)]


class ObjectionSeverity(StrEnum):
    CRITICAL = "CRITICAL"
    MATERIAL = "MATERIAL"
    ADVISORY = "ADVISORY"


class Objection(ContractModel):
    severity: ObjectionSeverity
    field_path: StableCode
    evidence_gap: Annotated[str, Field(min_length=1, max_length=500)]
    recommended_resolution: Annotated[str, Field(min_length=1, max_length=500)]


class SuggestedNextAction(ContractModel):
    command_code: StableCode
    requires_human: bool


class ProviderUsage(ContractModel):
    provider: Literal["deterministic"]
    model: Literal["fixture-v1"]
    units: NonNegativeInt
    tool_calls: NonNegativeInt
    incremental_cost_minor: Literal[0]
    cache_status: Literal["FIXTURE"]


class AgentOutputEnvelope(ContractModel, Generic[ArtifactT]):
    schema_version: Literal["1.0.0"]
    status: OutputStatus
    artifact: ArtifactT | None
    evidence_bindings: tuple[EvidenceBinding, ...]
    unknowns: tuple[UnknownItem, ...]
    assumptions: tuple[PlanningAssumption, ...]
    confidence: tuple[ConfidenceAssessment, ...]
    objections: tuple[Objection, ...]
    rationale: Annotated[str, Field(min_length=1, max_length=1_000)]
    suggested_next_action: SuggestedNextAction | None
    usage: ProviderUsage

    @model_validator(mode="after")
    def validate_status(self) -> AgentOutputEnvelope[ArtifactT]:
        if self.status == OutputStatus.COMPLETED and self.artifact is None:
            raise ValueError("A completed output requires a typed artifact.")
        if self.status == OutputStatus.FAILED and self.artifact is not None:
            raise ValueError("A failed output cannot present an artifact as valid.")
        return self


class EvaluationExpectation(ContractModel):
    expected_status: OutputStatus
    material_field_paths: tuple[StableCode, ...]
    max_incremental_cost_minor: Literal[0]


class EvaluationFixture(ContractModel, Generic[ArtifactT]):
    fixture_version: Literal["1.0.0"]
    fixture_id: StableCode
    invocation: AgentInvocationEnvelope
    output: AgentOutputEnvelope[ArtifactT]
    expectation: EvaluationExpectation

    @model_validator(mode="after")
    def validate_fixture(self) -> EvaluationFixture[ArtifactT]:
        if self.output.status != self.expectation.expected_status:
            raise ValueError("Fixture output status differs from its expectation.")
        if self.output.usage.tool_calls > self.invocation.tool_policy.max_tool_calls:
            raise ValueError("Fixture exceeds its tool-call boundary.")
        if self.output.usage.provider != self.invocation.provider_policy.provider:
            raise ValueError("Fixture provider differs from invocation policy.")
        if self.output.usage.model != self.invocation.provider_policy.model:
            raise ValueError("Fixture model differs from invocation policy.")

        evidence_ids = set(self.invocation.approved_evidence_item_ids)
        if any(
            not set(binding.evidence_item_ids).issubset(evidence_ids)
            for binding in self.output.evidence_bindings
        ):
            raise ValueError("Fixture binds evidence that the invocation did not approve.")

        classified_paths = {
            *(binding.field_path for binding in self.output.evidence_bindings),
            *(item.field_path for item in self.output.unknowns),
            *(item.field_path for item in self.output.assumptions),
        }
        if not set(self.expectation.material_field_paths).issubset(classified_paths):
            raise ValueError("Every material field must be evidence-bound, assumed, or unknown.")

        return self

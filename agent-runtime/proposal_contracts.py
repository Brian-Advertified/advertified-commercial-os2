"""Strict Proposal Narrative agent contracts."""

from __future__ import annotations

from typing import Annotated
from uuid import UUID

from pydantic import Field, model_validator

from contracts import AgentInvocationEnvelope, ContractModel, StableCode


class ProposalOptionInput(ContractModel):
    plan_version_id: UUID
    plan_version: Annotated[int, Field(gt=0)]
    label: Annotated[str, Field(min_length=1, max_length=200)]
    outcome: Annotated[str, Field(min_length=1, max_length=2_000)]
    budget_minor: Annotated[int, Field(ge=0)]
    currency: Annotated[str, Field(pattern=r"^[A-Z]{3}$")]
    channels: Annotated[tuple[StableCode, ...], Field(min_length=1)]


class ProposalContext(ContractModel):
    brief_version_id: UUID
    brief_objective: Annotated[str, Field(min_length=1, max_length=4_000)]
    options: Annotated[tuple[ProposalOptionInput, ...], Field(min_length=1, max_length=3)]


class ProposalNarrativeAgentRequest(ContractModel):
    invocation: AgentInvocationEnvelope
    proposal: ProposalContext

    @model_validator(mode="after")
    def validate_brief_reference(self) -> ProposalNarrativeAgentRequest:
        references = {
            (reference.resource_type, reference.resource_id, reference.version)
            for reference in self.invocation.resource_refs
        }
        required = {
            ("BriefVersion", self.proposal.brief_version_id, None),
            *(
                ("MediaPlanVersion", option.plan_version_id, option.plan_version)
                for option in self.proposal.options
            ),
        }
        brief_is_pinned = any(
            resource_type == "BriefVersion" and resource_id == self.proposal.brief_version_id
            for resource_type, resource_id, _ in references
        )
        plan_refs = {reference for reference in required if reference[2] is not None}
        if not brief_is_pinned or not plan_refs.issubset(references):
            raise ValueError("Exact BriefVersion and MediaPlanVersion references are required.")
        return self


class ProposalNarrativeDraftArtifact(ContractModel):
    executive_summary: Annotated[str, Field(min_length=1, max_length=5_000)]

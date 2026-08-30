"""Zero-cost proposal narrative from approved immutable plan facts."""

from __future__ import annotations

from contracts import (
    AgentOutputEnvelope,
    EvidenceBinding,
    OutputStatus,
    ProviderUsage,
    SuggestedNextAction,
)
from proposal_contracts import (
    ProposalNarrativeAgentRequest,
    ProposalNarrativeDraftArtifact,
)


def propose_narrative(
    request: ProposalNarrativeAgentRequest,
) -> AgentOutputEnvelope[ProposalNarrativeDraftArtifact]:
    options = " ".join(
        f"{item.label} invests {_money(item.budget_minor, item.currency)} across "
        f"{', '.join(item.channels)} to {_lower_first(item.outcome)}."
        for item in request.proposal.options
    )
    artifact = ProposalNarrativeDraftArtifact(
        executive_summary=(
            "The approved campaign objective is "
            f"{_lower_first(request.proposal.brief_objective)}. {options}"
        ),
    )
    evidence_ids = request.invocation.approved_evidence_item_ids
    bindings = () if not evidence_ids else (EvidenceBinding(
        field_path="artifact.executive_summary",
        evidence_item_ids=evidence_ids,
    ),)
    return AgentOutputEnvelope(
        schema_version="1.0.0",
        status=OutputStatus.COMPLETED,
        artifact=artifact,
        evidence_bindings=bindings,
        unknowns=(),
        assumptions=(),
        confidence=(),
        objections=(),
        rationale=(
            "The narrative restates supplied approved facts without changing commercial values."
        ),
        suggested_next_action=SuggestedNextAction(
            command_code="ReviewProposal",
            requires_human=True,
        ),
        usage=_usage(),
    )


def _money(amount_minor: int, currency: str) -> str:
    major, minor = divmod(amount_minor, 100)
    return f"{currency} {major:,}" if minor == 0 else f"{currency} {major:,}.{minor:02d}"


def _lower_first(value: str) -> str:
    return value[:1].lower() + value[1:]


def _usage() -> ProviderUsage:
    return ProviderUsage(
        provider="deterministic",
        model="fixture-v1",
        units=0,
        tool_calls=0,
        incremental_cost_minor=0,
        cache_status="FIXTURE",
    )

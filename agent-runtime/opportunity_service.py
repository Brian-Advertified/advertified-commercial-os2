"""Zero-cost deterministic implementations for the four Gate 4 proposal agents."""

from __future__ import annotations

from decimal import Decimal

from agent_registry import AgentCode
from contracts import (
    AgentOutputEnvelope,
    ConfidenceAssessment,
    EvidenceBinding,
    Objection,
    ObjectionSeverity,
    OutputStatus,
    ProviderUsage,
    SuggestedNextAction,
    UnknownItem,
)
from opportunity_contracts import (
    BusinessInterpretationArtifact,
    CriticReportArtifact,
    OpportunityAgentRequest,
    OpportunityAngle,
    OpportunityAngleSetArtifact,
    StrategyArtifact,
)


def _usage() -> ProviderUsage:
    return ProviderUsage(
        provider="deterministic",
        model="fixture-v1",
        units=0,
        tool_calls=0,
        incremental_cost_minor=0,
        cache_status="FIXTURE",
    )


def _evidence_ids(request: OpportunityAgentRequest) -> tuple:
    return tuple(item.id for item in request.approved_evidence)


def _statement(request: OpportunityAgentRequest) -> str:
    first = request.approved_evidence[0]
    values = ", ".join(str(value) for value in first.structured_value.values())
    return values or first.excerpt


def interpret(
    request: OpportunityAgentRequest,
) -> AgentOutputEnvelope[BusinessInterpretationArtifact]:
    evidence_ids = _evidence_ids(request)
    artifact = BusinessInterpretationArtifact(
        offering=_statement(request),
        customer_groups=(),
        buying_occasions=(),
        geographies=(),
        commercial_context="Interpretation is limited to the approved source claims.",
    )
    return AgentOutputEnvelope(
        schema_version="1.0.0",
        status=OutputStatus.COMPLETED,
        artifact=artifact,
        evidence_bindings=(EvidenceBinding(
            field_path="artifact.offering",
            evidence_item_ids=evidence_ids,
        ),),
        unknowns=(
            UnknownItem(
                field_path="artifact.customer_groups",
                question="Which customer groups are commercially material?",
                is_blocking=False,
            ),
            UnknownItem(
                field_path="artifact.buying_occasions",
                question="Which buying occasions drive demand?",
                is_blocking=False,
            ),
        ),
        assumptions=(),
        confidence=(ConfidenceAssessment(
            field_path="artifact.offering",
            confidence=Decimal("1"),
        ),),
        objections=(),
        rationale="Only approved evidence was interpreted; unsupported context remains unknown.",
        suggested_next_action=SuggestedNextAction(
            command_code="ConfirmBusinessInterpretation",
            requires_human=True,
        ),
        usage=_usage(),
    )


def generate_angles(
    request: OpportunityAgentRequest,
) -> AgentOutputEnvelope[OpportunityAngleSetArtifact]:
    evidence_ids = _evidence_ids(request)
    subject = _statement(request)
    artifact = OpportunityAngleSetArtifact(angles=(
        OpportunityAngle(
            rank=1,
            title="Make the verified offering easier to discover",
            rationale=f"Use the approved offering context as the factual anchor: {subject}",
            evidence_item_ids=evidence_ids,
            confidence=Decimal("0.80"),
        ),
        OpportunityAngle(
            rank=2,
            title="Turn verified interest into qualified enquiries",
            rationale="Test a response-led angle while treating conversion behaviour as unknown.",
            evidence_item_ids=evidence_ids,
            confidence=Decimal("0.65"),
        ),
    ))
    return AgentOutputEnvelope(
        schema_version="1.0.0",
        status=OutputStatus.COMPLETED,
        artifact=artifact,
        evidence_bindings=(EvidenceBinding(
            field_path="artifact.angles",
            evidence_item_ids=evidence_ids,
        ),),
        unknowns=(UnknownItem(
            field_path="artifact.angles.1.conversion_baseline",
            question="What is the current enquiry-to-sale baseline?",
            is_blocking=False,
        ),),
        assumptions=(),
        confidence=(ConfidenceAssessment(
            field_path="artifact.angles",
            confidence=Decimal("0.65"),
        ),),
        objections=(),
        rationale="The alternatives are proposals linked to the same approved evidence.",
        suggested_next_action=SuggestedNextAction(
            command_code="SelectOpportunityAngle",
            requires_human=True,
        ),
        usage=_usage(),
    )


def generate_strategy(
    request: OpportunityAgentRequest,
) -> AgentOutputEnvelope[StrategyArtifact]:
    evidence_ids = _evidence_ids(request)
    artifact = StrategyArtifact(
        diagnosis="The verified offering needs a clearer path from discovery to enquiry.",
        growth_thesis="Use the selected evidence-backed angle to create measurable demand.",
        objectives=("Increase qualified enquiries",),
        audience_hypotheses=("People with a demonstrated need for the verified offering",),
        proposition="A practical response to the need documented in approved evidence.",
        message="Move from interest to a clear next step.",
        channel_implications=("Prioritise measurable response channels during planning",),
        risks=("Audience and conversion baselines remain unknown",),
    )
    return AgentOutputEnvelope(
        schema_version="1.0.0",
        status=OutputStatus.COMPLETED,
        artifact=artifact,
        evidence_bindings=(EvidenceBinding(
            field_path="artifact.diagnosis",
            evidence_item_ids=evidence_ids,
        ),),
        unknowns=(UnknownItem(
            field_path="artifact.audience_hypotheses",
            question="Which audience hypothesis should be validated first?",
            is_blocking=False,
        ),),
        assumptions=(),
        confidence=(ConfidenceAssessment(
            field_path="artifact.diagnosis",
            confidence=Decimal("0.70"),
        ),),
        objections=(),
        rationale="The strategy is a proposal and does not claim unsupported audience facts.",
        suggested_next_action=SuggestedNextAction(
            command_code="RunCriticReadiness",
            requires_human=False,
        ),
        usage=_usage(),
    )


def critique(
    request: OpportunityAgentRequest,
) -> AgentOutputEnvelope[CriticReportArtifact]:
    artifact = CriticReportArtifact(
        readiness="REVIEW_REQUIRED",
        summary="The direction is usable after the measurement gap is explicitly resolved.",
    )
    return AgentOutputEnvelope(
        schema_version="1.0.0",
        status=OutputStatus.REVIEW_REQUIRED,
        artifact=artifact,
        evidence_bindings=(),
        unknowns=(),
        assumptions=(),
        confidence=(),
        objections=(Objection(
            severity=ObjectionSeverity.MATERIAL,
            field_path="artifact.objectives",
            evidence_gap="No approved conversion baseline is present.",
            recommended_resolution="Record the baseline as unknown and define a measurement task.",
        ),),
        rationale="The critic retains the evidence gap instead of manufacturing a baseline.",
        suggested_next_action=SuggestedNextAction(
            command_code="ResolveCriticObjection",
            requires_human=True,
        ),
        usage=_usage(),
    )


HANDLERS = {
    AgentCode.BUSINESS_INTERPRETATION: interpret,
    AgentCode.OPPORTUNITY_INTELLIGENCE: generate_angles,
    AgentCode.STRATEGY: generate_strategy,
    AgentCode.CRITIC_READINESS: critique,
}

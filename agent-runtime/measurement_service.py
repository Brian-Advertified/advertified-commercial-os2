"""Zero-cost Measurement interpretation from reviewed canonical facts."""

from __future__ import annotations

from decimal import Decimal

from contracts import (
    AgentOutputEnvelope,
    ConfidenceAssessment,
    EvidenceBinding,
    OutputStatus,
    ProviderUsage,
    SuggestedNextAction,
    UnknownItem,
)
from measurement_contracts import (
    LearningProposal,
    MeasurementAgentRequest,
    MeasurementFinding,
    MeasurementInterpretationArtifact,
    PerformanceMetricFact,
)


def interpret_measurement(
    request: MeasurementAgentRequest,
) -> AgentOutputEnvelope[MeasurementInterpretationArtifact]:
    metrics = tuple(
        metric
        for evidence in request.measurement.evidence_sets
        for metric in evidence.metrics
    )
    metric_ids = tuple(metric.id for metric in metrics)
    limitations = _limitations(request)
    artifact = _artifact(metrics, limitations)
    return AgentOutputEnvelope(
        schema_version="1.0.0",
        status=OutputStatus.COMPLETED,
        artifact=artifact,
        evidence_bindings=(
            EvidenceBinding(field_path="artifact.findings", evidence_item_ids=metric_ids),
            EvidenceBinding(field_path="artifact.limitations", evidence_item_ids=metric_ids),
        ),
        unknowns=(
            UnknownItem(
                field_path="artifact.causality_status",
                question="What approved measurement design would establish causality?",
                is_blocking=False,
            ),
        ),
        assumptions=(),
        confidence=tuple(_confidence(request, metric) for metric in metrics),
        objections=(),
        rationale=(
            "The deterministic interpretation references every reviewed metric exactly once "
            "and does not infer attribution, ROI or an optimisation action."
        ),
        suggested_next_action=SuggestedNextAction(
            command_code="ReviewMeasurementReport",
            requires_human=True,
        ),
        usage=ProviderUsage(
            provider="deterministic",
            model="fixture-v1",
            units=0,
            tool_calls=0,
            incremental_cost_minor=0,
            cache_status="FIXTURE",
        ),
    )


def _artifact(
    metrics: tuple[PerformanceMetricFact, ...],
    limitations: tuple[str, ...],
) -> MeasurementInterpretationArtifact:
    return MeasurementInterpretationArtifact(
        executive_summary=(
            "Reviewed campaign performance facts are presented with their approved "
            "source quality, methodology and limitations."
        ),
        findings=tuple(_finding(metric) for metric in metrics),
        limitations=limitations,
        learning_proposals=(
            LearningProposal(
                text=(
                    "Use the reviewed facts as learning inputs; require a new human-approved "
                    "plan before any material campaign or spend change."
                ),
                requires_new_approval=True,
            ),
        ),
        causality_status="NOT_ESTABLISHED",
    )


def _finding(metric: PerformanceMetricFact) -> MeasurementFinding:
    return MeasurementFinding(
        title=f"{metric.metric_type.replace('_', ' ').title()} reported",
        summary=(
            "The approved source reports this metric for the supplied campaign period. "
            "It is an observed fact and does not establish a causal effect."
        ),
        metric_ids=(metric.id,),
        causality_status="NOT_ESTABLISHED",
    )


def _limitations(request: MeasurementAgentRequest) -> tuple[str, ...]:
    return tuple(dict.fromkeys(
        limitation
        for evidence in request.measurement.evidence_sets
        for limitation in evidence.limitations
    ))


def _confidence(
    request: MeasurementAgentRequest,
    metric: PerformanceMetricFact,
) -> ConfidenceAssessment:
    evidence = next(
        item for item in request.measurement.evidence_sets if item.id == metric.evidence_set_id
    )
    value = Decimal("0.95") if evidence.quality_status == "VERIFIED" else Decimal("0.65")
    return ConfidenceAssessment(field_path="artifact.findings", confidence=value)

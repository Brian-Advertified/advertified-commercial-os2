"""Deterministic inventory interpretation rationale for planning proposals."""

from __future__ import annotations

from decimal import Decimal

from contracts import (
    AgentOutputEnvelope,
    ConfidenceAssessment,
    OutputStatus,
    ProviderUsage,
    SuggestedNextAction,
    UnknownItem,
)
from inventory_strategy import strategy_unknowns
from master_data_codes import EvidenceClassifications
from planning_contracts import (
    InventoryAudienceFitFacts,
    InventoryCandidateFacts,
    InventoryCandidateInterpretation,
    InventoryIntelligenceAgentRequest,
    InventoryShortlistDraftArtifact,
)

def interpret_inventory(
    request: InventoryIntelligenceAgentRequest,
) -> AgentOutputEnvelope[InventoryShortlistDraftArtifact]:
    interpretations = tuple(
        InventoryCandidateInterpretation(
            candidate_id=candidate.candidate_id,
            rationale=_inventory_rationale(candidate),
            classification=EvidenceClassifications.AI_RECOMMENDATION.value,
        )
        for candidate in request.inventory.candidates
    )
    unbenchmarked = any(
        candidate.is_eligible and candidate.benchmark is None
        for candidate in request.inventory.candidates
    )
    confidence = min(
        (_inventory_confidence(candidate) for candidate in request.inventory.candidates),
        default=Decimal("1"),
    )
    return AgentOutputEnvelope(
        schema_version="1.0.0",
        status=OutputStatus.COMPLETED,
        artifact=InventoryShortlistDraftArtifact(interpretations=interpretations),
        evidence_bindings=(),
        unknowns=strategy_unknowns(request.inventory.strategy) + ((UnknownItem(
            field_path="artifact.interpretations",
            question=(
                "A deterministic comparative benchmark is unavailable for one or more "
                "eligible candidates."
            ),
            is_blocking=False,
        ),) if unbenchmarked else ()),
        assumptions=(),
        confidence=(ConfidenceAssessment(
            field_path="artifact.interpretations",
            confidence=confidence,
        ),),
        objections=(),
        rationale=(
            "Each explanation restates the supplied governed eligibility and benchmark "
            "facts without changing commercial calculations."
        ),
        suggested_next_action=SuggestedNextAction(
            command_code="SelectInventoryShortlist",
            requires_human=True,
        ),
        usage=_usage(),
    )


def _inventory_rationale(candidate: InventoryCandidateFacts) -> str:
    if not candidate.is_eligible:
        return (
            "Excluded by governed hard eligibility: "
            f"{candidate.rejection_detail}"
        )
    suitability = _suitability_rationale(candidate)
    audience = _audience_fit_rationale(candidate)
    benchmark = candidate.benchmark
    if benchmark is None:
        return (
            f"{candidate.name} is eligible after governed hard constraints. "
            "No deterministic comparative benchmark applies. A low visible rate does not "
            f"establish audience value; review the actual buy and evidence. {suitability} {audience}"
        )
    if benchmark.cohort_size < 2 or benchmark.median_minor is None:
        return (
            f"{candidate.name} is eligible after governed hard constraints. "
            f"The {benchmark.geography_basis.replace('_', ' ').lower()} benchmark has "
            f"{benchmark.cohort_size} compatible peer(s), which is insufficient for a "
            f"defensible market-price conclusion. {suitability} {audience}"
        )
    position = benchmark.position.replace("_", " ").lower()
    return (
        f"{candidate.name} is eligible after governed hard constraints. Its published "
        f"rate is {position} across {benchmark.cohort_size} compatible peers using "
        f"{benchmark.geography_basis.replace('_', ' ').lower()}; deterministic "
        f"benchmark confidence is {int(benchmark.confidence * 100)}%. "
        f"{suitability} {audience}"
    )


def _suitability_rationale(candidate: InventoryCandidateFacts) -> str:
    suitability = candidate.suitability
    components = (
        ("geography", suitability.geography),
        ("audience", suitability.audience_context),
        ("evidence quality/freshness", suitability.evidence_quality_freshness),
    )
    detail = ", ".join(
        f"{name} {int(value * 100)}%" for name, value in components
    )
    gaps = (
        " Evidence gaps: " + ", ".join(suitability.evidence_gaps[:5]) + "."
        if suitability.evidence_gaps
        else ""
    )
    return (
        f"The partial evidence score is {int(suitability.total * 100)}% under "
        f"{suitability.policy_version}: {detail}. This is not a probability of success. "
        "Creative effectiveness, comparable target cost and incremental reach remain "
        f"unscored until supported by evidence.{gaps}"
    )


def _audience_fit_rationale(candidate: InventoryCandidateFacts) -> str:
    fit = candidate.audience_fit
    if fit.evidence_gaps:
        return (
            "Audience fit remains unscored because evidence is incomplete: "
            + ", ".join(fit.evidence_gaps)
            + "."
        )
    scores = (
        ("language", fit.language_score),
        ("life-stage", fit.life_stage_score),
        ("LSM/SEM", fit.lsm_sem_score),
    )
    supplied = [f"{name} {int(value * 100)}%" for name, value in scores if value is not None]
    audience = (
        "Evidence-backed audience fit: " + ", ".join(supplied) + "."
        if supplied
        else "The approved target audiences contain no structured audience dimensions to compare."
    )
    return audience + " " + _delivery_measurement_rationale(fit)


def _delivery_measurement_rationale(fit: InventoryAudienceFitFacts) -> str:
    if fit.delivery_evidence_gaps:
        return (
            "Delivery evidence remains incomplete: "
            + ", ".join(fit.delivery_evidence_gaps)
            + "."
        )
    measurements = [
        f"{item.metric_type.replace('_', ' ').lower()} {item.value} {item.unit}"
        for item in fit.delivery_measurements
        if item.value is not None and item.unit is not None
    ]
    return (
        "Supplied delivery measurements: " + ", ".join(measurements) + "."
        if measurements
        else "No delivery measurement was supplied."
    )


def _inventory_confidence(candidate: InventoryCandidateFacts) -> Decimal:
    if not candidate.is_eligible:
        return Decimal("1")
    return (
        candidate.benchmark.confidence
        if candidate.benchmark is not None
        else Decimal("0.50")
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

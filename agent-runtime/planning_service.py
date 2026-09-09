"""Zero-cost Audience and Media Planning proposals from approved Brief inputs."""

from __future__ import annotations

from decimal import Decimal
from uuid import UUID

from contracts import (
    AgentOutputEnvelope,
    ConfidenceAssessment,
    EvidenceBinding,
    OutputStatus,
    PlanningAssumption,
    ProviderUsage,
    SuggestedNextAction,
    UnknownItem,
)
from audience_candidates import audience_research_unknowns, candidate_audience_names
from audience_evidence import grounded_audience
from inventory_strategy import strategy_unknowns
from master_data_codes import EvidenceClassifications
from planning_contracts import (
    AudienceAgentRequest,
    AudienceDefinition,
    AudienceDefinitionSetArtifact,
    InventoryAudienceFitFacts,
    InventoryCandidateFacts,
    InventoryCandidateInterpretation,
    InventoryIntelligenceAgentRequest,
    InventoryShortlistDraftArtifact,
    MediaAllocation,
    MediaMixDraftArtifact,
    MediaPlanningAgentRequest,
)


def canonicalize_audiences(request: AudienceAgentRequest, output):
    """Enforce approved audience facts while retaining provider-authored wording."""
    artifact = output.artifact
    if artifact is None:
        return output
    approved = request.invocation.approved_evidence_item_ids
    classification = (
        EvidenceClassifications.INFERENCE.value
        if approved else EvidenceClassifications.HYPOTHESIS.value
    )
    allowed_geographies = set(request.planning.geographies)
    audiences = tuple(
        grounded_audience(request, item.model_copy(update={
            "geographies": tuple(
                value for value in item.geographies
                if value in allowed_geographies
            ) or request.planning.geographies,
            "language": None,
            "life_stage": None,
            "lsm_sem": None,
            "lsm_sem_taxonomy": None,
            "lsm_sem_taxonomy_version": None,
            "lsm_sem_mandatory": False,
            "classification": classification,
            "evidence_item_ids": approved,
        }))
        for item in artifact.audiences
    )
    return output.model_copy(update={
        "artifact": artifact.model_copy(update={"audiences": audiences}),
        "unknowns": audience_research_unknowns(output.unknowns),
    })


def propose_audiences(
    request: AudienceAgentRequest,
) -> AgentOutputEnvelope[AudienceDefinitionSetArtifact]:
    evidence_ids = request.invocation.approved_evidence_item_ids
    classification = (
        EvidenceClassifications.INFERENCE.value
        if evidence_ids else EvidenceClassifications.HYPOTHESIS.value
    )
    candidate_names = candidate_audience_names(request)
    audiences = tuple(
        grounded_audience(request, _audience(
            request,
            name,
            classification,
            evidence_ids,
            is_target=True,
        ))
        for name in candidate_names
    )
    names = ", ".join(item.name for item in audiences if item.is_target)
    markets = ", ".join(request.planning.geographies)
    artifact = AudienceDefinitionSetArtifact(
        audiences=audiences,
        targeting_rationale=(
            f"Prioritise {names} in {markets} because the approved Brief identifies "
            "those audiences and markets for the stated objective."
        ),
        positioning_statement=(
            f"Positioning for {names} requires a validated consumer need and product "
            "proposition; the campaign objective alone does not establish either."
        ),
    )
    return AgentOutputEnvelope(
        schema_version="1.0.0",
        status=OutputStatus.COMPLETED,
        artifact=artifact,
        evidence_bindings=_bindings(evidence_ids, "artifact.audiences"),
        unknowns=audience_research_unknowns(),
        assumptions=(),
        confidence=(ConfidenceAssessment(
            field_path="artifact.audiences",
            confidence=Decimal("0.70") if evidence_ids else Decimal("0.45"),
        ),),
        objections=(),
        rationale=(
            "Audience proposals use only the approved Brief and keep unsupported detail unknown."
        ),
        suggested_next_action=SuggestedNextAction(
            command_code="GenerateMediaMix",
            requires_human=False,
        ),
        usage=_usage(),
    )


def propose_media_mix(
    request: MediaPlanningAgentRequest,
) -> AgentOutputEnvelope[MediaMixDraftArtifact]:
    channels = tuple(sorted(set(request.planning.available_channels)))[:3]
    allocations = _allocate(request.planning.budget_minor, channels)
    artifact = MediaMixDraftArtifact(
        allocations=allocations,
        assumptions=(
            "Channel allocation is a planning hypothesis pending human plan review.",
        ),
    )
    evidence_ids = request.invocation.approved_evidence_item_ids
    return AgentOutputEnvelope(
        schema_version="1.0.0",
        status=OutputStatus.COMPLETED,
        artifact=artifact,
        evidence_bindings=_bindings(evidence_ids, "artifact.allocations"),
        unknowns=(UnknownItem(
            field_path="artifact.allocations.reach_baseline",
            question="What verified reach or response baseline is available?",
            is_blocking=False,
        ),),
        assumptions=(PlanningAssumption(
            field_path="artifact.allocations",
            value="Allocate evenly across up to three approved channels.",
            impact="The planner must review channel contribution before approval.",
            validation_needed="Reconcile against verified inventory and forecast evidence.",
        ),),
        confidence=(ConfidenceAssessment(
            field_path="artifact.allocations",
            confidence=Decimal("0.55"),
        ),),
        objections=(),
        rationale=(
            "The proposal uses only allowed channels and reconciles exactly to the Brief budget."
        ),
        suggested_next_action=SuggestedNextAction(
            command_code="ReviewMediaMix",
            requires_human=True,
        ),
        usage=_usage(),
    )


def interpret_inventory(
    request: InventoryIntelligenceAgentRequest,
) -> AgentOutputEnvelope[InventoryShortlistDraftArtifact]:
    interpretations = tuple(
        InventoryCandidateInterpretation(
            candidate_id=candidate.candidate_id,
            rationale=_inventory_rationale(candidate),
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


def _audience(
    request: AudienceAgentRequest,
    name: str,
    classification: str,
    evidence_ids: tuple[UUID, ...],
    *,
    is_target: bool,
) -> AudienceDefinition:
    return AudienceDefinition(
        name=name,
        description=(
            f"An audience supplied in the approved Brief: {name}. "
            "Human validation is required before media planning."
        ),
        need_state="Consumer need is not supplied; validate it separately from the campaign objective.",
        buying_context=(
            "Purchase occasion, decision-making role and intent are not supplied; "
            "validate them before using this audience to justify a media purchase."
        ),
        geographies=request.planning.geographies,
        language=None,
        life_stage=None,
        lsm_sem=None,
        lsm_sem_taxonomy=None,
        lsm_sem_taxonomy_version=None,
        lsm_sem_mandatory=False,
        classification=classification,
        exclusions=("Do not infer sensitive individual attributes.",),
        evidence_item_ids=evidence_ids,
        confidence=(
            Decimal("0.70") if evidence_ids
            else Decimal("0.45")
        ),
        is_target=is_target,
    )


def _allocate(budget: int, channels: tuple[str, ...]) -> tuple[MediaAllocation, ...]:
    even, remainder = divmod(budget, len(channels))
    return tuple(
        MediaAllocation(
            channel=channel,
            budget_minor=even + (remainder if index == 0 else 0),
            role="Primary response channel" if index == 0 else "Supporting reach channel",
        )
        for index, channel in enumerate(channels)
    )


def _bindings(evidence_ids: tuple[UUID, ...], path: str) -> tuple[EvidenceBinding, ...]:
    return () if not evidence_ids else (EvidenceBinding(
        field_path=path,
        evidence_item_ids=evidence_ids,
    ),)


def _usage() -> ProviderUsage:
    return ProviderUsage(
        provider="deterministic",
        model="fixture-v1",
        units=0,
        tool_calls=0,
        incremental_cost_minor=0,
        cache_status="FIXTURE",
    )

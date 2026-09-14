"""Audience proposals, source provenance and governed audience normalization."""

from __future__ import annotations

import re
from uuid import UUID

from contracts import AgentOutputEnvelope, EvidenceBinding, OutputStatus, ProviderUsage, SuggestedNextAction
from audience_candidates import audience_research_unknowns, candidate_audience_names
from audience_evidence import grounded_audience
from master_data_codes import EvidenceClassifications
from planning_contracts import AudienceAgentRequest, AudienceDefinition, AudienceDefinitionSetArtifact


def canonicalize_audiences(request: AudienceAgentRequest, output):
    """Preserve Brief-required audiences while rejecting forged provider provenance."""
    artifact = output.artifact
    if artifact is None:
        return output
    _validate_audience_sources(request, artifact, require_brief_audiences=False)
    supplied = list(dict.fromkeys(value.strip() for value in request.planning.audiences if value.strip()))
    by_key = {_audience_key(item.name): item for item in artifact.audiences}
    required = tuple(
        by_key.get(_audience_key(name))
        or grounded_audience(request, _audience(request, _display_label(name), is_target=True))
        for name in supplied
    )
    supplied_keys = {_audience_key(value) for value in supplied}
    extras = tuple(item for item in artifact.audiences if _audience_key(item.name) not in supplied_keys)
    artifact = artifact.model_copy(update={"audiences": (*required, *extras)})
    _validate_audience_sources(request, artifact)
    audiences = tuple(_canonicalize_audience(request, item)
                      for item in artifact.audiences)
    evidence_ids = tuple(dict.fromkeys(evidence_id for audience in audiences
                                      for evidence_id in audience.evidence_item_ids))
    targeting_rationale = _canonical_targeting_rationale(request, audiences)
    target_label = ", ".join(_display_label(item.name) for item in audiences if item.is_target)
    positioning_statement = _working_hypothesis(artifact.positioning_statement) or (
        _positioning_hypothesis(request, target_label) if target_label else None
    )
    return output.model_copy(update={
        "artifact": artifact.model_copy(update={
            "audiences": audiences, "targeting_rationale": targeting_rationale,
            "positioning_statement": positioning_statement,
        }),
        "evidence_bindings": _bindings(evidence_ids, "artifact.audiences"),
        "unknowns": audience_research_unknowns(output.unknowns),
    })


def _canonicalize_audience(
    request: AudienceAgentRequest,
    item: AudienceDefinition,
) -> AudienceDefinition:
    grounded = grounded_audience(request, item.model_copy(update={
        "name": _canonical_audience_name(request, item.name),
        "geographies": request.planning.geographies,
        "language": None,
        "life_stage": None,
        "lsm_sem": None,
        "lsm_sem_taxonomy": None,
        "lsm_sem_taxonomy_version": None,
        "lsm_sem_mandatory": False,
    }))
    name = grounded.name.strip()
    display_name = _display_label(name)
    brief_audiences = {_audience_key(value) for value in request.planning.audiences if value.strip()}
    markets = ", ".join(_display_label(value) for value in grounded.geographies) or "the approved geography"
    has_evidence = bool(grounded.evidence_item_ids or grounded.reference_observation_ids)
    is_client_requirement = _audience_key(name) in brief_audiences
    if is_client_requirement:
        grounded = grounded.model_copy(update={
            "need_state": grounded.need_state or _need_hypothesis(request, display_name),
            "buying_context": grounded.buying_context or _buying_context_hypothesis(request, display_name),
        })
        description = (
            f"Brief-supplied audience: {display_name}. Additional motivations, buying intent, affiliations and behaviours "
            "are not established unless separately supported by approved evidence."
        )
    elif has_evidence:
        description = (
            f"Evidence-supported audience candidate: {display_name}, within {markets}. Only separately retained evidence "
            "fields are established; unstated motivations, buying intent, affiliations and behaviours remain unestablished."
        )
    else:
        description = (
            f"Audience hypothesis: {display_name}, considered for the stated objective within {markets}. Unstated motivations, "
            "buying intent, affiliations and behaviours are not established."
        )
    return grounded.model_copy(update={
        "description": description, "is_target": True if is_client_requirement else grounded.is_target,
    })


def _canonical_targeting_rationale(
    request: AudienceAgentRequest,
    audiences: tuple[AudienceDefinition, ...],
) -> str | None:
    targets = [_display_label(item.name) for item in audiences if item.is_target]
    if not targets:
        return None
    markets = [_display_label(value) for value in request.planning.geographies if value.strip()]
    supported = sum(bool(item.evidence_item_ids or item.reference_observation_ids) for item in audiences if item.is_target)
    provenance = (
        f" {supported} target audience{'s' if supported != 1 else ''} also retain approved supporting evidence; "
        "only those retained fields may be treated as established."
        if supported else
        " The audience identities come from the approved Brief; need, buying-context and behavioural detail remain working hypotheses unless separately supported."
    )
    geography = f" across {', '.join(markets)}" if markets else ""
    objective = request.planning.objective.strip().rstrip(".")
    return (
        f"Prioritise {', '.join(targets)}{geography} because the approved Brief explicitly names them for the stated objective: {objective}."
        f"{provenance} Governed aggregate market research may inform planning context but does not prove individual media preference or buying behaviour."
    )[:4000]


def _audience_key(value: str) -> str:
    return value.strip().rstrip(" .,:;!?").casefold()


def _canonical_audience_name(request: AudienceAgentRequest, value: str) -> str:
    key = _audience_key(value)
    for supplied in request.planning.audiences:
        if _audience_key(supplied) == key:
            return supplied.strip()
    return value.strip()


def _display_label(value: str) -> str:
    return value.strip().rstrip(" .,:;!?")


def _allowed_geography_keys(values) -> set[str]:
    allowed: set[str] = set()
    for value in values:
        normalized = value.strip()
        if not normalized:
            continue
        allowed.add(normalized.casefold())
        allowed.add(_display_label(normalized).casefold())
        for part in re.split(r"\s*(?:,|;|\band\b)\s*", normalized, flags=re.IGNORECASE):
            part = _display_label(part)
            if part:
                allowed.add(part.casefold())
    return allowed


def segment_support_reference_ids(request: AudienceAgentRequest) -> tuple[UUID, ...]:
    return tuple(item.observation_id for item in request.planning.reference_evidence
                 if item.activation_policy == "AUDIENCE_SEGMENT_SUPPORT")


def _validate_audience_sources(
    request: AudienceAgentRequest,
    artifact: AudienceDefinitionSetArtifact,
    *,
    require_brief_audiences: bool = True,
):
    names = [_audience_key(item.name) for item in artifact.audiences]
    if len(names) != len(set(names)):
        raise ValueError("Audience Intelligence returned duplicate audience identities.")
    required = {_audience_key(name) for name in request.planning.audiences if name.strip()}
    if require_brief_audiences and not required.issubset(names):
        raise ValueError("Audience Intelligence omitted an audience required by the approved Brief.")
    allowed_evidence = set(request.invocation.approved_evidence_item_ids)
    references = {item.observation_id: item for item in request.planning.reference_evidence}
    segment_references = set(segment_support_reference_ids(request))
    allowed_geographies = _allowed_geography_keys(request.planning.geographies)
    for audience in artifact.audiences:
        if any(value.strip().casefold() not in allowed_geographies for value in audience.geographies):
            raise ValueError("Audience Intelligence returned geography outside the approved Brief.")
        if any(item not in allowed_evidence for item in audience.evidence_item_ids):
            raise ValueError("Audience Intelligence cited an unapproved Brief evidence item.")
        if any(item not in references for item in audience.reference_observation_ids):
            raise ValueError("Audience Intelligence cited a reference observation it was not supplied.")
        if any(item not in segment_references for item in audience.reference_observation_ids):
            raise ValueError("Audience segments may cite only references approved for segment support.")


def validate_audience_grounding(
    request: AudienceAgentRequest,
    output: AgentOutputEnvelope[AudienceDefinitionSetArtifact],
) -> None:
    artifact = output.artifact
    if artifact is None:
        return
    _validate_audience_sources(request, artifact)
    brief_audiences = {_audience_key(value) for value in request.planning.audiences if value.strip()}
    for audience in artifact.audiences:
        if audience.reference_observation_ids and audience.classification == EvidenceClassifications.FACT.value:
            raise ValueError("Aggregate reference observations support an inference, not a constructed audience fact.")
        is_client_requirement = _audience_key(audience.name) in brief_audiences
        expected = (EvidenceClassifications.CLIENT_REQUIREMENT.value if is_client_requirement
                    else EvidenceClassifications.HYPOTHESIS.value)
        if audience.classification == EvidenceClassifications.CLIENT_REQUIREMENT.value and not is_client_requirement:
            raise ValueError("Only audiences explicitly named in the approved Brief may be client requirements.")
        if not audience.evidence_item_ids and not audience.reference_observation_ids:
            if audience.classification != expected:
                raise ValueError("Evidence-free audience classification does not match its approved Brief provenance.")
            if audience.confidence is not None:
                raise ValueError("Evidence-free audiences may not carry numeric confidence.")


def propose_audiences(request: AudienceAgentRequest) -> AgentOutputEnvelope[AudienceDefinitionSetArtifact]:
    audiences = tuple(grounded_audience(request, _audience(request, _display_label(name), is_target=True))
                      for name in candidate_audience_names(request))
    evidence_ids = tuple(dict.fromkeys(evidence_id for audience in audiences
                                      for evidence_id in audience.evidence_item_ids))
    names = ", ".join(_display_label(item.name) for item in audiences if item.is_target)
    markets = ", ".join(_display_label(value) for value in request.planning.geographies)
    artifact = AudienceDefinitionSetArtifact(
        audiences=audiences,
        targeting_rationale=(
            f"Prioritise {names} in {markets} because the approved Brief identifies "
            "those audiences and markets for the stated objective. Treat the audience names as client requirements "
            "and the need/buying statements as working hypotheses until validated by research."
        ) if names else None,
        positioning_statement=_positioning_hypothesis(request, names) if names else None,
    )
    return AgentOutputEnvelope(
        schema_version="1.0.0", status=OutputStatus.COMPLETED, artifact=artifact,
        evidence_bindings=_bindings(evidence_ids, "artifact.audiences"),
        unknowns=audience_research_unknowns(), assumptions=(), confidence=(), objections=(),
        rationale="Audience proposals use only the approved Brief and keep unsupported detail unknown.",
        suggested_next_action=SuggestedNextAction(command_code="ReviewAudienceStrategy", requires_human=True),
        usage=_usage(),
    )


def _audience(request: AudienceAgentRequest, name: str, *, is_target: bool) -> AudienceDefinition:
    display_name = _display_label(name)
    return AudienceDefinition(
        name=display_name,
        description=f"An audience supplied in the approved Brief: {display_name}. Human validation is required before media planning.",
        need_state=_need_hypothesis(request, display_name),
        buying_context=_buying_context_hypothesis(request, display_name),
        geographies=request.planning.geographies,
        language=None, life_stage=None, lsm_sem=None, lsm_sem_taxonomy=None,
        lsm_sem_taxonomy_version=None, lsm_sem_mandatory=False,
        classification=EvidenceClassifications.HYPOTHESIS.value,
        exclusions=("Do not infer sensitive individual attributes.",),
        evidence_item_ids=(), confidence=None, is_target=is_target,
    )


def _need_hypothesis(request: AudienceAgentRequest, name: str) -> str:
    display_name = _display_label(name)
    objective = request.planning.objective.strip().rstrip(".")
    return (
        f"Hypothesis: {display_name} may have a need connected to the stated campaign objective — {objective}. "
        "Validate the underlying consumer need before treating this as evidence."
    )[:1000]


def _buying_context_hypothesis(request: AudienceAgentRequest, name: str) -> str:
    display_name = _display_label(name)
    return (
        f"Hypothesis: The decision context for {display_name} may be influenced by the supplied campaign objective and offer context. "
        "Product, price, purchase occasion and decision-making role still require validation."
    )[:1000]


def _positioning_hypothesis(request: AudienceAgentRequest, names: str) -> str:
    objective = request.planning.objective.strip().rstrip(".")
    return (
        f"Hypothesis: Position the campaign for {names} around the stated objective — {objective}. "
        "Validate the audience benefit and proposition before client-facing use."
    )[:4000]


def _optional_text(value: str | None) -> str | None:
    if value is None:
        return None
    normalized = value.strip()
    return normalized or None


def _working_hypothesis(value: str | None) -> str | None:
    normalized = _optional_text(value)
    if normalized is None or not normalized.casefold().startswith("hypothesis:"):
        return None
    return normalized


def _bindings(evidence_ids: tuple[UUID, ...], path: str) -> tuple[EvidenceBinding, ...]:
    return () if not evidence_ids else (EvidenceBinding(field_path=path, evidence_item_ids=evidence_ids),)


def _usage() -> ProviderUsage:
    return ProviderUsage(provider="deterministic", model="fixture-v1", units=0,
                         tool_calls=0, incremental_cost_minor=0, cache_status="FIXTURE")

"""Audience proposals, source provenance and governed audience normalization."""

from __future__ import annotations

from uuid import UUID

from contracts import AgentOutputEnvelope, EvidenceBinding, OutputStatus, ProviderUsage, SuggestedNextAction
from audience_candidates import audience_research_unknowns, candidate_audience_names
from audience_evidence import grounded_audience
from master_data_codes import EvidenceClassifications
from planning_contracts import AudienceAgentRequest, AudienceDefinition, AudienceDefinitionSetArtifact


def canonicalize_audiences(request: AudienceAgentRequest, output):
    """Validate provenance first; normalization must never launder a forged citation."""
    artifact = output.artifact
    if artifact is None:
        return output
    _validate_audience_sources(request, artifact)
    allowed_geographies = {value.strip().casefold(): value for value in request.planning.geographies}
    audiences = tuple(_canonicalize_audience(request, item, allowed_geographies)
                      for item in artifact.audiences)
    target_names = [item.name for item in audiences if item.is_target]
    evidence_ids = tuple(dict.fromkeys(evidence_id for audience in audiences
                                      for evidence_id in audience.evidence_item_ids))
    targeting_rationale = (
        "Prioritise " + ", ".join(target_names) +
        f" for the stated objective within {', '.join(request.planning.geographies)}. "
        "This recommendation does not establish unstated consumer needs, buying contexts or behaviours; "
        "those remain research gaps."
        if target_names else None
    )
    return output.model_copy(update={
        "artifact": artifact.model_copy(update={
            "audiences": audiences, "targeting_rationale": targeting_rationale,
            "positioning_statement": None,
        }),
        "evidence_bindings": _bindings(evidence_ids, "artifact.audiences"),
        "unknowns": audience_research_unknowns(output.unknowns),
    })


def _canonicalize_audience(
    request: AudienceAgentRequest,
    item: AudienceDefinition,
    allowed_geographies: dict[str, str],
) -> AudienceDefinition:
    grounded = grounded_audience(request, item.model_copy(update={
        "geographies": tuple(dict.fromkeys(
            allowed_geographies[value.strip().casefold()] for value in item.geographies
        )),
        "language": None,
        "life_stage": None,
        "lsm_sem": None,
        "lsm_sem_taxonomy": None,
        "lsm_sem_taxonomy_version": None,
        "lsm_sem_mandatory": False,
    }))
    name = grounded.name.strip()
    brief_audiences = {value.strip().casefold() for value in request.planning.audiences if value.strip()}
    markets = ", ".join(grounded.geographies) or "the approved geography"
    has_evidence = bool(grounded.evidence_item_ids or grounded.reference_observation_ids)
    is_client_requirement = name.casefold() in brief_audiences
    if is_client_requirement:
        description = (
            f"Brief-supplied audience: {name}. Additional motivations, buying intent, affiliations and behaviours "
            "are not established unless separately supported by approved evidence."
        )
    elif has_evidence:
        description = (
            f"Evidence-supported audience candidate: {name}, within {markets}. Only separately retained evidence "
            "fields are established; unstated motivations, buying intent, affiliations and behaviours remain unestablished."
        )
    else:
        description = (
            f"Audience hypothesis: {name}, considered for the stated objective within {markets}. Unstated motivations, "
            "buying intent, affiliations and behaviours are not established."
        )
    return grounded.model_copy(update={
        "description": description, "is_target": True if is_client_requirement else grounded.is_target,
    })


def segment_support_reference_ids(request: AudienceAgentRequest) -> tuple[UUID, ...]:
    return tuple(item.observation_id for item in request.planning.reference_evidence
                 if item.activation_policy == "AUDIENCE_SEGMENT_SUPPORT")


def _validate_audience_sources(request: AudienceAgentRequest, artifact: AudienceDefinitionSetArtifact):
    names = [item.name.strip().casefold() for item in artifact.audiences]
    if len(names) != len(set(names)):
        raise ValueError("Audience Intelligence returned duplicate audience identities.")
    required = {name.strip().casefold() for name in request.planning.audiences if name.strip()}
    if not required.issubset(names):
        raise ValueError("Audience Intelligence omitted an audience required by the approved Brief.")
    allowed_evidence = set(request.invocation.approved_evidence_item_ids)
    references = {item.observation_id: item for item in request.planning.reference_evidence}
    geographies = {item.strip().casefold() for item in request.planning.geographies}
    segment_references = set(segment_support_reference_ids(request))
    for audience in artifact.audiences:
        if any(item not in allowed_evidence for item in audience.evidence_item_ids):
            raise ValueError("Audience Intelligence cited an unapproved Brief evidence item.")
        if any(item not in references for item in audience.reference_observation_ids):
            raise ValueError("Audience Intelligence cited a reference observation it was not supplied.")
        if not audience.geographies or any(item.strip().casefold() not in geographies
                                           for item in audience.geographies):
            raise ValueError("Audience Intelligence omitted or moved outside the approved geographic scope.")
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
    brief_audiences = {value.strip().casefold() for value in request.planning.audiences if value.strip()}
    for audience in artifact.audiences:
        if audience.reference_observation_ids and audience.classification == EvidenceClassifications.FACT.value:
            raise ValueError("Aggregate reference observations support an inference, not a constructed audience fact.")
        is_client_requirement = audience.name.strip().casefold() in brief_audiences
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
    audiences = tuple(grounded_audience(request, _audience(request, name, is_target=True))
                      for name in candidate_audience_names(request))
    evidence_ids = tuple(dict.fromkeys(evidence_id for audience in audiences
                                      for evidence_id in audience.evidence_item_ids))
    names = ", ".join(item.name for item in audiences if item.is_target)
    markets = ", ".join(request.planning.geographies)
    artifact = AudienceDefinitionSetArtifact(
        audiences=audiences,
        targeting_rationale=(
            f"Prioritise {names} in {markets} because the approved Brief identifies "
            "those audiences and markets for the stated objective."
        ) if names else None,
        positioning_statement=None,
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
    return AudienceDefinition(
        name=name,
        description=f"An audience supplied in the approved Brief: {name}. Human validation is required before media planning.",
        need_state=None, buying_context=None, geographies=request.planning.geographies,
        language=None, life_stage=None, lsm_sem=None, lsm_sem_taxonomy=None,
        lsm_sem_taxonomy_version=None, lsm_sem_mandatory=False,
        classification=EvidenceClassifications.HYPOTHESIS.value,
        exclusions=("Do not infer sensitive individual attributes.",),
        evidence_item_ids=(), confidence=None, is_target=is_target,
    )


def _bindings(evidence_ids: tuple[UUID, ...], path: str) -> tuple[EvidenceBinding, ...]:
    return () if not evidence_ids else (EvidenceBinding(field_path=path, evidence_item_ids=evidence_ids),)


def _usage() -> ProviderUsage:
    return ProviderUsage(provider="deterministic", model="fixture-v1", units=0,
                         tool_calls=0, incremental_cost_minor=0, cache_status="FIXTURE")

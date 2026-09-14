"""Deterministic zero-cost supplied-Brief interpretation for local development fixtures."""

from decimal import Decimal

from contracts import AgentOutputEnvelope, OutputStatus, ProviderUsage, SuggestedNextAction
from master_data_codes import CampaignModes, EvidenceClassifications
from supplied_brief_contracts import (
    BriefEvidence,
    BriefQuestion,
    BriefUnknown,
    SuppliedBriefArtifact,
    SuppliedBriefRequest,
    SuppliedDraft,
)
from supplied_brief_grounding_utils import citation_key, exact_budget, supports_campaign_mode
from supplied_brief_model_input import PRIMARY_LOCATOR, source_segments

_LABELS = {
    "client": "clientName", "client name": "clientName", "brand": "clientName",
    "problem": "businessProblem", "business problem": "businessProblem",
    "objective": "objective", "audience": "audiences", "audiences": "audiences",
    "geography": "geographies", "geographies": "geographies", "location": "geographies",
    "locations": "geographies", "timing": "timing", "dates": "timing",
    "media": "mediaRequirements", "format": "mediaRequirements", "formats": "mediaRequirements",
    "constraints": "constraints", "requirements": "constraints", "exclusions": "constraints",
    "measurement": "measurement",
}
_MULTI_FIELDS = {"audiences", "geographies", "mediaRequirements", "constraints", "measurement"}
_BLOCKING_FIELDS = {
    "clientName": "Who is the client or brand?",
    "businessProblem": "What business problem should the campaign address?",
    "objective": "What outcome must the campaign achieve?",
    "audiences": "Which audience must the campaign reach?",
    "geographies": "Which geography must the campaign cover?",
    "timing": "What campaign timing or running period applies?",
}
_NON_OOH_MEDIA = (
    "radio", "television", " tv ", "print", "social", "influencer", "podcast",
    "experiential", "email", "mobile", "cinema",
)


def deterministic_supplied_brief(request: SuppliedBriefRequest) -> AgentOutputEnvelope[SuppliedBriefArtifact]:
    """Interpret only explicit labelled local fixture facts; leave everything else unknown."""
    values, evidence = _source_values(request)
    _apply_clarifications(request, values, evidence)
    campaign_mode = _campaign_mode(values)
    _append_mode_evidence(campaign_mode, evidence)
    questions = _questions(values, campaign_mode)
    artifact = _artifact(request, values, evidence, campaign_mode, questions)
    return _envelope(artifact)


def _artifact(request, values, evidence, campaign_mode, questions) -> SuppliedBriefArtifact:
    budget = exact_budget(request)
    currency, budget_minor = budget if budget is not None else (None, None)
    draft = _draft(values, currency, budget_minor, questions)
    return SuppliedBriefArtifact(
        source_hash=request.source.source_hash,
        client_name=_optional_text(values.get("clientName")),
        title=request.source.source_title,
        campaign_mode=campaign_mode,
        campaign_mode_confidence=Decimal("1") if campaign_mode else Decimal("0"),
        requires_human_clarification=any(item.is_blocking for item in questions),
        campaign_mode_rationale=_mode_rationale(campaign_mode),
        draft=draft,
        questions=questions,
        evidence=tuple(evidence),
    )


def _draft(values, currency, budget_minor, questions) -> SuppliedDraft:
    unknowns = tuple(BriefUnknown(
        field_path=item.field_path, question=item.question, is_blocking=item.is_blocking,
    ) for item in questions)
    return SuppliedDraft(
        business_problem=str(values.get("businessProblem", "")),
        objective=str(values.get("objective", "")),
        audiences=tuple(values.get("audiences", ())), geographies=tuple(values.get("geographies", ())),
        timing=str(values.get("timing", "")), budget_minor=budget_minor,
        budget_unknown=budget_minor is None, currency=currency, vat_status=None, fees_minor=None,
        media_requirements=tuple(values.get("mediaRequirements", ())),
        constraints=tuple(values.get("constraints", ())), measurement=tuple(values.get("measurement", ())),
        facts=(), unknowns=unknowns, assumptions=(), conflicts=(),
    )


def _envelope(artifact: SuppliedBriefArtifact) -> AgentOutputEnvelope[SuppliedBriefArtifact]:
    return AgentOutputEnvelope(
        schema_version="1.0.0", status=OutputStatus.REVIEW_REQUIRED, artifact=artifact,
        evidence_bindings=(), unknowns=(), assumptions=(), confidence=(), objections=(),
        rationale=("Deterministic local interpretation copied only explicit labelled source facts; "
                   "unsupported values remain unknown for human review."),
        suggested_next_action=SuggestedNextAction(command_code="ReviewSuppliedBrief", requires_human=True),
        usage=ProviderUsage(provider="deterministic", model="fixture-v1", units=0, tool_calls=0,
                            incremental_cost_minor=0, cache_status="FIXTURE"),
    )


def _source_values(request: SuppliedBriefRequest):
    primary = next(segment["content"] for segment in source_segments(request)
                   if segment["segment_id"] == PRIMARY_LOCATOR)
    values: dict[str, object] = {field: [] for field in _MULTI_FIELDS}
    evidence: list[BriefEvidence] = []
    for raw_line in primary.splitlines():
        _retain_labelled_line(raw_line, values, evidence)
    return values, evidence


def _retain_labelled_line(raw_line: str, values: dict[str, object], evidence: list[BriefEvidence]) -> None:
    line = raw_line.strip()
    label, separator, value = line.partition(":")
    field_path = _LABELS.get(citation_key(label)) if separator else None
    clean_value = value.strip()
    if field_path is None or not clean_value:
        return
    if field_path in _MULTI_FIELDS:
        values[field_path].append(clean_value)  # type: ignore[union-attr]
    else:
        values[field_path] = clean_value
    evidence.append(_evidence(field_path, line, PRIMARY_LOCATOR))


def _apply_clarifications(request, values, evidence) -> None:
    for item in request.source.clarifications:
        value = item.value.strip()
        if not value:
            continue
        if item.field_path in _MULTI_FIELDS:
            values[item.field_path] = [value]
        elif item.field_path in {"clientName", "businessProblem", "objective", "timing", "campaignMode"}:
            values[item.field_path] = value
        evidence.append(_evidence(item.field_path, item.value, f"clarification:{item.field_path}"))


def _evidence(field_path: str, excerpt: str, locator: str) -> BriefEvidence:
    return BriefEvidence(
        field_path=field_path, kind=EvidenceClassifications.FACT, excerpt=excerpt,
        confidence=Decimal("1"), source_locator=locator,
    )


def _campaign_mode(values):
    clarified = values.get("campaignMode")
    if clarified in {CampaignModes.OOH_ONLY, CampaignModes.FULL_CAMPAIGN}:
        return clarified
    normalized = f" {citation_key(' '.join(str(item) for item in values.get('mediaRequirements', ())))} "
    if any(term in normalized for term in (
        " full campaign ", " integrated campaign ", " multichannel ", " multi channel ", *_NON_OOH_MEDIA,
    )):
        return CampaignModes.FULL_CAMPAIGN
    words = set(normalized.split())
    has_ooh = bool(words.intersection({"ooh", "dooh", "billboard", "billboards"})) or (
        "out of home" in normalized or "outdoor" in words)
    return CampaignModes.OOH_ONLY if has_ooh and "only" in words else None


def _append_mode_evidence(campaign_mode, evidence: list[BriefEvidence]) -> None:
    if campaign_mode is None or any(item.field_path == "campaignMode" for item in evidence):
        return
    supporting = next((item for item in evidence
                       if item.field_path == "mediaRequirements" and supports_campaign_mode(campaign_mode, item.excerpt)), None)
    if supporting is not None:
        evidence.append(supporting.model_copy(update={"field_path": "campaignMode"}))


def _questions(values, campaign_mode) -> tuple[BriefQuestion, ...]:
    questions = [BriefQuestion(field_path=field_path, question=question, is_blocking=True, options=())
                 for field_path, question in _BLOCKING_FIELDS.items() if not values.get(field_path)]
    if campaign_mode is None:
        questions.append(BriefQuestion(
            field_path="campaignMode",
            question="Should this use only out-of-home media or a full campaign?",
            is_blocking=True,
            options=(CampaignModes.OOH_ONLY, CampaignModes.FULL_CAMPAIGN),
        ))
    return tuple(questions)


def _mode_rationale(campaign_mode) -> str:
    if campaign_mode:
        return "Campaign mode is determined only from explicit supplied media wording."
    return "The supplied media wording does not deterministically establish campaign mode."


def _optional_text(value) -> str | None:
    return value if isinstance(value, str) and value else None

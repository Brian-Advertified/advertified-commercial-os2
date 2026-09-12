"""Market Intelligence over the Brief and actual approved evidence snapshots."""

from __future__ import annotations

from contracts import AgentOutputEnvelope, OutputStatus, ProviderUsage, SuggestedNextAction, UnknownItem
from master_data_codes import EvidenceClassifications
from market_intelligence_contracts import MarketIntelligenceAgentRequest, MarketIntelligenceArtifact

INSTRUCTION = (
    "Analyse the commercial market context for the supplied business problem. Client requirements are "
    "facts about the Brief, not proof of market conditions. Use only the actual approved_evidence content "
    "supplied alongside the market context. Cite exact IDs from those evidence snapshots, not imagined "
    "sources or other identifiers. Do not invent category size, market share, competitor behaviour, pricing, "
    "demand, audience concentration, media effectiveness, consumer behaviour or trend data. A market FACT "
    "or INFERENCE requires supporting supplied evidence. With no supporting evidence, label a finding "
    "HYPOTHESIS, leave evidence_item_ids empty and confidence null, and make the wording explicitly "
    "conditional. Do not turn a client objective into evidence of demand. Disclose missing evidence and "
    "retain strict client constraints and unresolved conflicts. Empty findings and opportunities are valid "
    "when evidence does not justify a recommendation; do not pad the result. Market opportunities without "
    "evidence must identify the evidence needed to validate them. Return category situation, supported "
    "findings or hypotheses, provisional opportunities, strategic implications and evidence gaps."
)


def propose_market_intelligence(
    request: MarketIntelligenceAgentRequest,
) -> AgentOutputEnvelope[MarketIntelligenceArtifact]:
    """A zero-cost test mode retains the frame; it does not impersonate market analysis."""
    evidence_note = (
        f"{len(request.approved_evidence)} approved evidence snapshots are retained for analysis."
        if request.approved_evidence else "No verified market evidence is supplied."
    )
    artifact = MarketIntelligenceArtifact(
        category_situation=(
            f"The approved commercial problem for {request.market.client_name} and its audience, geography, "
            f"constraints and success measures remain bound to the Brief. {evidence_note} "
            "This deterministic run does not infer demand, competitor behaviour or a market opportunity."
        ),
        findings=(),
        opportunities=(),
        strategic_implications=(
            "Carry the approved client requirements and unresolved conflicts into subsequent analysis.",
            "Evaluate market claims against retained evidence before choosing a growth route.",
        ),
        evidence_gaps=(
            "Market interpretation has not been performed by this deterministic run.",
        ),
    )
    return AgentOutputEnvelope(
        schema_version="1.0.0",
        status=OutputStatus.COMPLETED,
        artifact=artifact,
        evidence_bindings=(),
        unknowns=(UnknownItem(
            field_path="artifact.findings",
            question="What evidence-backed market conclusions can an authorised analyst establish?",
            is_blocking=True,
        ),),
        assumptions=(),
        confidence=(),
        objections=(),
        rationale="The commercial frame is retained; no deterministic market finding or opportunity is invented.",
        suggested_next_action=SuggestedNextAction(
            command_code="ReviewGeneratedArtifact", requires_human=True,
        ),
        usage=ProviderUsage(
            provider="deterministic", model="fixture-v1", units=0, tool_calls=0,
            incremental_cost_minor=0, cache_status="FIXTURE",
        ),
    )


def canonicalize_market_intelligence(
    request: MarketIntelligenceAgentRequest,
    output: AgentOutputEnvelope[MarketIntelligenceArtifact],
) -> AgentOutputEnvelope[MarketIntelligenceArtifact]:
    artifact = output.artifact
    if artifact is None or request.approved_evidence:
        return output
    evidence_gaps = tuple(dict.fromkeys((
        "No verified market evidence is supplied.",
        *artifact.evidence_gaps,
    )))
    safe_situation = (
        f"The approved Brief for {request.market.client_name} defines the business problem, objective, "
        "audiences, geographies and client constraints. No verified market evidence is supplied. "
        "Market conditions, demand, competitor behaviour, category size, audience concentration and "
        "media effectiveness therefore remain unverified."
    )
    return output.model_copy(update={
        "artifact": artifact.model_copy(update={
            "category_situation": safe_situation,
            "strategic_implications": (
                "Carry approved client requirements and unresolved conflicts into subsequent analysis.",
                "Do not use unverified market conditions as planning facts until approved evidence supports them.",
            ),
            "evidence_gaps": evidence_gaps,
        }),
    })


def validate_market_grounding(
    request: MarketIntelligenceAgentRequest,
    output: AgentOutputEnvelope[MarketIntelligenceArtifact],
) -> None:
    artifact = output.artifact
    if artifact is None:
        raise ValueError("Market Intelligence returned no artifact.")
    # An identifier in the internal envelope is not evidence that the model received
    # the source. Only the exact supplied snapshots can support a finding.
    approved = {item.id for item in request.approved_evidence}
    if not artifact.findings and not artifact.opportunities and not artifact.evidence_gaps:
        raise ValueError("A market result without conclusions must explain its evidence gap.")
    if not approved and not any(marker in artifact.category_situation.casefold() for marker in (
        "no verified", "not supplied", "evidence is absent", "evidence is not supplied",
        "requires evidence", "evidence gap",
    )):
        raise ValueError("Market Intelligence must disclose the absence of verified market evidence.")
    for finding in artifact.findings:
        _validate_finding(finding, approved)
    if not approved and any(not item.required_evidence for item in artifact.opportunities):
        raise ValueError("Market opportunities without evidence must state the required evidence.")


def _validate_finding(finding, approved: set) -> None:
    evidence_ids = set(finding.evidence_item_ids)
    if not evidence_ids.issubset(approved):
        raise ValueError("Market Intelligence cited evidence whose approved content was not supplied.")
    if finding.classification not in {
        EvidenceClassifications.FACT, EvidenceClassifications.INFERENCE, EvidenceClassifications.HYPOTHESIS,
    }:
        raise ValueError("The market finding has an unsupported evidence classification.")
    if evidence_ids:
        return
    if finding.classification != EvidenceClassifications.HYPOTHESIS:
        raise ValueError("Market facts and inferences require supplied approved evidence.")
    if finding.confidence is not None:
        raise ValueError("A market hypothesis without evidence may not carry invented numeric confidence.")
    wording = f" {finding.finding.casefold()} "
    if not any(marker in wording for marker in (
        "hypothesis", " may ", " could ", " might ", "requires evidence", "requires validation",
        "not established", "unknown",
    )):
        raise ValueError("Unsupported market hypotheses must use explicitly conditional wording.")

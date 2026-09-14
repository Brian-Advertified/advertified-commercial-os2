"""First-class Media Strategy Intelligence reasoning and grounding."""

from __future__ import annotations

from decimal import Decimal
import logging
import re

logger = logging.getLogger(__name__)

from contracts import (
    AgentOutputEnvelope,
    OutputStatus,
    ProviderUsage,
    SuggestedNextAction,
    UnknownItem,
)
from media_strategy_contracts import (
    MediaChannelRecommendation,
    MediaStrategyArtifact,
    MediaStrategyRequest,
    UNSUPPORTED_CLAIM_MARKERS,
)

INSTRUCTION = (
    "Produce Media Strategy Intelligence upstream of inventory. Recommend only channels from available_channels. "
    "Explain the strategic role, objective contribution, geographic role and trade-offs for each recommended "
    "channel. Obey explicit media requirements and exclusions exactly; never resolve contradictory client "
    "instructions by guessing. Do not mention supplier inventory, availability, rates, sites, placements or "
    "specific buyable products. Target-audience hypotheses and Location hypotheses must remain hypotheses and may "
    "not be rewritten as facts. Do not invent reach, frequency, CPM, footfall, media consumption, performance or "
    "channel effectiveness evidence. If no budget is established, leave budget_guidance_percent null rather than "
    "inventing a split. If a budget exists, percentage guidance is still a strategic recommendation rather than an "
    "inventory-backed forecast. Unknown evidence should be stated as an evidence gap. "
    "Each role must describe only a communication task, such as presenting the supplied offer or inviting the "
    "stated action. Do not describe site traffic, audience presence, placement quality or promised effectiveness "
    "inside the role or objective contribution. Those details are not established by the supplied context. "
    "The following unsupported phrases are rejected by the output contract in every field: "
    + "; ".join(UNSUPPORTED_CLAIM_MARKERS) + "."
)


def unavailable_media_strategy(
    request: MediaStrategyRequest,
) -> AgentOutputEnvelope[MediaStrategyArtifact]:
    artifact = MediaStrategyArtifact(
        summary=(
            "No deterministic Media Strategy recommendation is fabricated. A live intelligence provider or human "
            "strategist must reason across the approved commercial problem and strategic context."
        ),
        channel_recommendations=(),
        strategic_principles=(
            "Media Strategy is evaluated before buyable supply.",
        ),
        excluded_channels=(),
        evidence_gaps=(
            "Strategic channel roles require governed reasoning across the approved Brief and audience context.",
        ),
    )
    return AgentOutputEnvelope(
        schema_version="1.0.0",
        status=OutputStatus.COMPLETED,
        artifact=artifact,
        evidence_bindings=(),
        unknowns=(UnknownItem(
            field_path="artifact.channel_recommendations",
            question="Which channel roles best serve the approved objective before inventory is considered?",
            is_blocking=True,
        ),),
        assumptions=(),
        confidence=(),
        objections=(),
        rationale="The deterministic path refuses to impersonate strategic media reasoning.",
        suggested_next_action=SuggestedNextAction(
            command_code="ReviewMediaStrategy",
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


def deterministic_media_strategy(
    request: MediaStrategyRequest,
) -> AgentOutputEnvelope[MediaStrategyArtifact]:
    """Build a bounded local-certification strategy from governed Brief inputs only.

    This fixture exists so deterministic connected-browser certification can exercise the
    real strategy approval flow. It never infers media performance, audience presence or
    buyable supply facts. Live/provider strategy remains a separate execution path.
    """
    context = request.media_strategy
    selected = _deterministic_channels(context.available_channels, context.media_requirements)
    percentages = _deterministic_budget_split(len(selected)) if context.budget_minor is not None else [None] * len(selected)
    geographies = ", ".join(context.geographies)
    recommendations = tuple(
        MediaChannelRecommendation(
            channel=channel,
            role=(
                f"Use {channel} to present the approved campaign message and invite the stated action for the "
                "approved objective."
            ),
            rationale=(
                f"{channel} is retained for deterministic local certification because it is inside the governed "
                "channel scope and is consistent with the approved media requirements. This is a planning "
                "hypothesis, not a performance claim."
            ),
            objective_contribution=(
                f"Proposed role: use {channel} as a communication channel for the approved objective. "
                "Actual delivery and effectiveness require later evidence."
            ),
            geography_role=(
                f"Evaluate {channel} only within the approved campaign geography: {geographies}. "
                "This does not establish audience presence or media performance."
            ),
            classification="HYPOTHESIS",
            budget_guidance_percent=percentages[index],
            trade_offs=(
                "Channel role remains provisional until execution constraints and media evidence are reviewed.",
            ),
            evidence_gaps=(
                "Verified channel reach, frequency, audience delivery and effectiveness evidence has not been supplied here.",
            ),
        )
        for index, channel in enumerate(selected)
    )
    artifact = MediaStrategyArtifact(
        summary=(
            "Deterministic local certification retained only governed channels that are compatible with the approved "
            "media requirements. The roles are hypotheses and do not assert measured delivery or effectiveness."
        ),
        channel_recommendations=recommendations,
        strategic_principles=tuple(dict.fromkeys((
            *context.media_requirements,
            "Treat approved audience and location hypotheses as hypotheses; do not promote them to media facts.",
            "Select and evaluate buyable inventory only after Media Strategy is approved.",
        ))),
        excluded_channels=tuple(channel for channel in context.available_channels if channel not in selected),
        evidence_gaps=(
            "Verified channel reach, frequency, audience delivery and effectiveness evidence has not been supplied here.",
        ),
    )
    return AgentOutputEnvelope(
        schema_version="1.0.0",
        status=OutputStatus.COMPLETED,
        artifact=artifact,
        evidence_bindings=(),
        unknowns=(),
        assumptions=(),
        confidence=(),
        objections=(),
        rationale=(
            "Deterministic local certification used only approved Brief requirements, campaign geography and the "
            "governed channel scope."
        ),
        suggested_next_action=SuggestedNextAction(
            command_code="ReviewMediaStrategy",
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


def _deterministic_channels(available_channels, media_requirements):
    available = tuple(dict.fromkeys(available_channels))
    if not media_requirements:
        return available
    tokens = {
        token.upper()
        for requirement in media_requirements
        for token in re.findall(r"[A-Za-z0-9]+", requirement)
    }
    explicit = tuple(channel for channel in available if channel.upper() in tokens)
    return explicit or available


def _deterministic_budget_split(count: int):
    if count <= 0:
        return []
    if count == 1:
        return [Decimal("100")]
    base = (Decimal("100") / Decimal(count)).quantize(Decimal("0.01"))
    result = [base] * (count - 1)
    result.append(Decimal("100") - sum(result, Decimal("0")))
    return result


def _geography_role(context, channel: str) -> str | None:
    if not context.location_opportunities:
        return None
    geographies = tuple(dict.fromkeys(
        opportunity.geography for opportunity in context.location_opportunities
    ))
    return (
        f"Evaluate {channel} against the approved Location Intelligence opportunity context in "
        f"{', '.join(geographies)}. Location opportunity classifications remain unchanged and do not prove "
        "audience presence, movement or media performance."
    )


def _contains_unsupported(value: str) -> bool:
    text = value.casefold()
    return any(marker in text for marker in UNSUPPORTED_CLAIM_MARKERS)


def _safe_role(item) -> str:
    role = item.role.strip()
    if role and not _contains_unsupported(role):
        return role
    return (
        f"{item.channel} communication role: present the approved campaign message and invite the stated action."
    )


def _canonical_channel(item, context):
    safe_role = _safe_role(item)
    evidence_gaps = tuple(dict.fromkeys((
        *(gap for gap in item.evidence_gaps if not _contains_unsupported(gap)),
        "Verified channel reach, frequency, audience delivery and effectiveness evidence has not been supplied here.",
    )))
    if not context.location_opportunities:
        evidence_gaps = tuple(dict.fromkeys((
            *evidence_gaps,
            "No approved Location Intelligence artifact was supplied for geographic channel reasoning.",
        )))
    return item.model_copy(update={
        "rationale": (
            f"{item.channel} is retained as a {item.classification.lower()} channel recommendation for the stated "
            "objective because the provider selected it from the governed channel scope. No supplied evidence in "
            "this step establishes reach, frequency, footfall, audience concentration or channel effectiveness."
        ),
        "role": safe_role,
        "objective_contribution": (
            f"Proposed role: {safe_role}. This describes the intended contribution to the client objective; "
            "it is not a measured performance claim."
        ),
        "geography_role": _geography_role(context, item.channel),
        "trade_offs": (
            "Channel role remains a strategic hypothesis until media evidence and execution constraints are evaluated.",
        ),
        "evidence_gaps": evidence_gaps,
    })


def _retained_channels(artifact, context):
    allowed = set(context.available_channels)
    retained = []
    seen = set()
    removed = 0
    for item in artifact.channel_recommendations:
        if item.channel not in allowed or item.channel in seen:
            removed += 1
            continue
        seen.add(item.channel)
        retained.append(_canonical_channel(item, context))
    return retained, seen, removed, allowed


def _valid_budget_guidance(context, retained) -> bool:
    percentages = [item.budget_guidance_percent for item in retained]
    return bool(
        context.budget_minor is not None
        and retained
        and all(value is not None for value in percentages)
        and sum((value for value in percentages if value is not None), Decimal("0")) == Decimal("100")
    )


def _strategy_gaps(artifact, context, removed: int, valid_budget_guidance: bool):
    gaps = tuple(dict.fromkeys((
        *(gap for gap in artifact.evidence_gaps if not _contains_unsupported(gap)),
        *context.audience_unknowns,
        *context.location_evidence_gaps,
        "Verified channel reach, frequency, audience delivery and effectiveness evidence has not been supplied here.",
    )))
    if removed:
        gaps = tuple(dict.fromkeys((
            *gaps,
            f"Advertified removed {removed} unsupported or duplicate channel recommendation"
            f"{'s' if removed != 1 else ''}.",
        )))
    if context.budget_minor is None:
        gaps = tuple(dict.fromkeys((*gaps, "No confirmed budget was supplied; no budget split is asserted.")))
    elif not valid_budget_guidance:
        gaps = tuple(dict.fromkeys((
            *gaps,
            "The provider did not return a complete 100% strategic budget guidance split; percentage guidance was omitted.",
        )))
    return gaps


def _strategy_summary(retained) -> str:
    if not retained:
        return "No provider channel recommendation survived the governed Media Strategy rules."
    channels = ", ".join(item.channel for item in retained)
    return (
        f"Media Strategy retained {channels} as governed strategic channel recommendation"
        f"{'s' if len(retained) != 1 else ''} for the stated objective. Channel roles remain recommendations, not "
        "claims of measured reach or effectiveness, and no buyable supply facts are used at this stage."
    )


def canonicalize_media_strategy(
    request: MediaStrategyRequest,
    output: AgentOutputEnvelope[MediaStrategyArtifact],
):
    """Retain channel decisions while rebuilding unsupported provider prose deterministically."""
    artifact = output.artifact
    if artifact is None:
        return output
    context = request.media_strategy
    retained, seen, removed, allowed = _retained_channels(artifact, context)
    valid_budget_guidance = _valid_budget_guidance(context, retained)
    if not valid_budget_guidance:
        retained = [item.model_copy(update={"budget_guidance_percent": None}) for item in retained]
    revised_artifact = artifact.model_copy(update={
        "summary": _strategy_summary(retained),
        "channel_recommendations": tuple(retained),
        "strategic_principles": tuple(dict.fromkeys((
            *context.media_requirements,
            "Treat approved audience and location hypotheses as hypotheses; do not promote them to media facts.",
            "Select and evaluate buyable inventory only after Media Strategy is approved.",
        ))),
        "excluded_channels": tuple(
            item for item in artifact.excluded_channels if item in allowed and item not in seen
        ),
        "evidence_gaps": _strategy_gaps(artifact, context, removed, valid_budget_guidance),
    })
    return output.model_copy(update={"artifact": revised_artifact})


def validate_media_strategy_grounding(
    request: MediaStrategyRequest,
    output: AgentOutputEnvelope[MediaStrategyArtifact],
) -> None:
    artifact = output.artifact
    if artifact is None:
        return
    allowed = set(request.media_strategy.available_channels)
    channels = [item.channel for item in artifact.channel_recommendations]
    if len(channels) != len(set(channels)):
        raise ValueError("Media Strategy returned duplicate channel recommendations.")
    if any(channel not in allowed for channel in channels):
        raise ValueError("Media Strategy returned a channel outside the governed channel scope.")
    if any(channel not in allowed for channel in artifact.excluded_channels):
        raise ValueError("Media Strategy excluded a channel outside the governed channel scope.")
    if request.media_strategy.budget_minor is None and any(
        item.budget_guidance_percent is not None for item in artifact.channel_recommendations
    ):
        raise ValueError("Budgetless Media Strategy may not invent a percentage allocation.")
    percentages = [item.budget_guidance_percent for item in artifact.channel_recommendations]
    if any(value is not None for value in percentages):
        if not percentages or any(value is None for value in percentages) or sum(
            (value for value in percentages if value is not None), Decimal("0")
        ) != Decimal("100"):
            raise ValueError("Media Strategy budget guidance must be absent or reconcile to 100 percent.")

    text = " ".join((
        artifact.summary,
        *artifact.strategic_principles,
        *artifact.evidence_gaps,
        *(item.role for item in artifact.channel_recommendations),
        *(item.rationale for item in artifact.channel_recommendations),
        *(item.objective_contribution for item in artifact.channel_recommendations),
        *(item.geography_role or "" for item in artifact.channel_recommendations),
        *(tradeoff for item in artifact.channel_recommendations for tradeoff in item.trade_offs),
    )).casefold()
    prohibited = UNSUPPORTED_CLAIM_MARKERS
    if any(marker in text for marker in prohibited):
        groups = {
            "summary": (artifact.summary,),
            "principles": artifact.strategic_principles,
            "evidence_gaps": artifact.evidence_gaps,
            "roles": tuple(item.role for item in artifact.channel_recommendations),
            "rationale": tuple(item.rationale for item in artifact.channel_recommendations),
            "objective": tuple(item.objective_contribution for item in artifact.channel_recommendations),
            "geography": tuple(item.geography_role or "" for item in artifact.channel_recommendations),
            "trade_offs": tuple(value for item in artifact.channel_recommendations for value in item.trade_offs),
        }
        # Log only fixed field names and policy markers, never provider or client text.
        hits = {name: [marker for marker in prohibited if any(marker in value.casefold() for value in values)]
                for name, values in groups.items()}
        logger.warning("Media Strategy grounding policy hits: %s", {name: markers for name, markers in hits.items() if markers})
        raise ValueError("Media Strategy introduced inventory, audience-presence or unsupported performance claims.")

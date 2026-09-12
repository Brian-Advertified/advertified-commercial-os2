"""Location Intelligence research planning, synthesis and grounding rules."""

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
from location_intelligence_contracts import (
    LocationIntelligenceArtifact,
    LocationOpportunityArea,
    LocationResearchPlanArtifact,
    LocationResearchPlanRequest,
    LocationSynthesisRequest,
)

RESEARCH_INSTRUCTION = (
    "Create a bounded geographic research plan for the supplied commercial problem and approved audience "
    "strategy. Produce at most eight POI research requests. Each poi_category must be one exact code from "
    "available_poi_categories and must be paired with an anchor_geography. The anchor must be either an exact "
    "Brief geography or a geography present in supplied reference context; an INFERENCE must cite an exact "
    "reference observation for that anchor. Advertified owns provider query syntax, so never emit OSM tags or "
    "Overpass/Nominatim syntax. Do not search for people or infer an individual's ethnicity, nationality, "
    "religion, health status or other sensitive trait. Aggregate SENSITIVE_CONTEXT_ONLY observations may guide "
    "contextual geography research but do not prove that the target audience lives at, visits or identifies "
    "with a place. Do not invent narrower suburbs, routes, towns, POIs or audience concentrations. If a "
    "requirement cannot be researched safely, leave it as an evidence gap instead of creating a request."
)

SYNTHESIS_INSTRUCTION = (
    "Synthesize Location Intelligence only from the supplied commercial problem, approved target segments, "
    "governed reference observations, research plan and resolved places. A resolved place proves only that the "
    "place exists at the returned location; it does not prove audience presence, visitation, affiliation or "
    "campaign effectiveness. Create opportunity areas only as INFERENCE or HYPOTHESIS, cite only supplied place "
    "IDs and reference observation IDs, preserve sensitive-context limitations, and state material evidence gaps. "
    "Return only summary, opportunities and evidence_gaps. Advertified attaches the exact request-owned "
    "research_queries and resolved_places and retains every deterministic research gap; do not include those "
    "immutable arrays in your proposal or recreate failed research requests. If no "
    "resolved places or reference observations are supplied, return opportunities as an empty array and "
    "describe the research limitation. Never reconstruct failed queries from gap wording. POI-only or "
    "context-only hypotheses must have confidence null; a failed lookup cannot establish an opportunity. "
    "Do not mention or rank media inventory; Location Intelligence runs before inventory discovery."
)


def propose_location_research(
    request: LocationResearchPlanRequest,
) -> AgentOutputEnvelope[LocationResearchPlanArtifact]:
    artifact = LocationResearchPlanArtifact(
        queries=(),
        rationale=(
            "No deterministic free-text POI inference is performed. Live intelligence may propose bounded "
            "research queries; otherwise explicit verified spatial requirements should be retained by the Brief."
        ),
        evidence_gaps=(
            "Location research queries require semantic interpretation of the approved Brief and audience context.",
        ),
    )
    return _envelope(
        artifact,
        "The deterministic Location Intelligence path does not guess place-search terms.",
        "RunLocationSynthesis",
    )


def synthesize_location(
    request: LocationSynthesisRequest,
) -> AgentOutputEnvelope[LocationIntelligenceArtifact]:
    context = request.location
    artifact = LocationIntelligenceArtifact(
        summary=(
            "No deterministic opportunity area is asserted. Resolved places, when present, are retained as "
            "factual place-discovery results but require an evidence-backed audience/location relationship."
        ),
        opportunities=(),
        research_queries=context.research_plan.queries,
        resolved_places=context.resolved_places,
        evidence_gaps=tuple(dict.fromkeys((
            *context.research_plan.evidence_gaps,
            "Verified audience presence or movement evidence is required before ranking opportunity areas.",
        ))),
    )
    return _envelope(
        artifact,
        "Location synthesis retained supplied places without inventing audience presence or inventory suitability.",
        "ReviewLocationIntelligence",
    )

def _envelope(artifact, rationale: str, command_code: str):
    return AgentOutputEnvelope(
        schema_version="1.0.0",
        status=OutputStatus.COMPLETED,
        artifact=artifact,
        evidence_bindings=(),
        unknowns=(UnknownItem(
            field_path="artifact.location_evidence",
            question="What verified audience presence, movement, catchment or visitation evidence supports these locations?",
            is_blocking=False,
        ),),
        assumptions=(),
        confidence=(ConfidenceAssessment(
            field_path="artifact",
            confidence=Decimal("0.35"),
        ),),
        objections=(),
        rationale=rationale,
        suggested_next_action=SuggestedNextAction(
            command_code=command_code,
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

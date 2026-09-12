"""Evidence-bound Location Intelligence synthesis; research validation is separate."""

from __future__ import annotations

from contracts import AgentOutputEnvelope
from location_intelligence_contracts import LocationIntelligenceArtifact, LocationSynthesisRequest

CONTEXT_ONLY_POLICIES = frozenset({"SENSITIVE_CONTEXT_ONLY", "AGGREGATE_PLANNING_ONLY"})
HYPOTHESIS_GAP = (
    "Supplied evidence does not establish target-audience presence, visitation, movement, footfall or media "
    "effectiveness for hypothesis-only location areas."
)


def canonicalize_location_synthesis(
    request: LocationSynthesisRequest,
    output: AgentOutputEnvelope[LocationIntelligenceArtifact],
):
    artifact = output.artifact
    if artifact is None:
        return output
    reference_by_id = {item.observation_id: item for item in request.location.reference_evidence}
    place_by_id = {item.place_id: item for item in request.location.resolved_places}
    revised_opportunities: list = []
    downgraded = 0
    removed_evidence_free = 0
    for area in artifact.opportunities:
        _validate_area_references(request, area, set(place_by_id), reference_by_id)
        revised, action = _classify_opportunity_area(area, reference_by_id, place_by_id)
        if action == "kept":
            revised_opportunities.append(revised)
        elif action == "downgraded":
            revised_opportunities.append(revised)
            downgraded += 1
        else:
            removed_evidence_free += 1
    if not downgraded and not removed_evidence_free:
        return output
    revised = artifact.model_copy(update={
        "summary": _synthesis_summary(request, revised_opportunities),
        "opportunities": tuple(revised_opportunities),
        "evidence_gaps": _synthesis_gaps(request, artifact, downgraded, removed_evidence_free),
    })
    return output.model_copy(update={"artifact": revised})


def _classify_opportunity_area(area, reference_by_id: dict, place_by_id: dict):
    cited_references = [reference_by_id[item] for item in area.reference_observation_ids]
    inference_evidence = [item for item in cited_references if item.activation_policy not in CONTEXT_ONLY_POLICIES]
    if inference_evidence:
        return area, "kept"
    if not area.place_ids and not cited_references:
        return area, "removed"
    safe_name, safe_rationale = _hypothesis_narrative(area, place_by_id)
    return area.model_copy(update={
        "name": safe_name,
        "rationale": safe_rationale,
        "classification": "HYPOTHESIS",
        "confidence": None,
        "evidence_gaps": tuple(dict.fromkeys((*area.evidence_gaps, HYPOTHESIS_GAP))),
    }), "downgraded"


def _hypothesis_narrative(area, place_by_id: dict) -> tuple[str, str]:
    if area.place_ids:
        categories = tuple(dict.fromkeys(
            place_by_id[item].query.split(" in ", 1)[0]
            for item in area.place_ids if " in " in place_by_id[item].query
        ))
        context_label = " / ".join(categories) if categories else "Contextual POI"
        return (
            f"{context_label} research hypothesis — {area.geography}",
            (
                f"Provider-resolved POIs establish contextual place existence in the bounded search for {area.geography}. "
                "Search bounding boxes do not establish administrative-area membership. "
                "Supplied evidence does not establish that the target audience is present at, visits, moves "
                "through, or is affiliated with these places, and it does not establish footfall or media "
                "effectiveness."
            ),
        )
    return (
        f"Context-supported location hypothesis — {area.geography}",
        (
            f"Governed reference observations provide contextual evidence for {area.geography}, but no "
            "provider-resolved POI fact establishes a specific place opportunity and no supplied evidence "
            "establishes target-audience presence, visitation, movement, footfall or media effectiveness."
        ),
    )


def _synthesis_summary(request: LocationSynthesisRequest, revised_opportunities: list) -> str:
    if request.location.resolved_places:
        return (
            f"Advertified retained {len(request.location.resolved_places)} provider-resolved contextual "
            f"POI facts across {len(request.location.research_plan.queries)} bounded research requests. "
            "POI existence is verified, but target-audience presence, visitation, movement, footfall and "
            "media effectiveness are not established by the supplied evidence; POI-only opportunity areas "
            "therefore remain hypotheses."
        )
    if revised_opportunities:
        return (
            "No provider-resolved POI facts were supplied. Advertified retained only location hypotheses "
            "supported by governed reference observations; target-audience presence, visitation, movement, "
            "footfall and media effectiveness remain unestablished."
        )
    return (
        "No provider-resolved POI facts or governed reference observations support a Location opportunity "
        "area. Advertified therefore returned no opportunity areas; further bounded location research is required."
    )


def _synthesis_gaps(request: LocationSynthesisRequest, artifact, downgraded: int, removed_evidence_free: int):
    evidence_gaps = tuple(dict.fromkeys((*request.location.research_plan.evidence_gaps, *artifact.evidence_gaps)))
    if downgraded:
        evidence_gaps = tuple(dict.fromkeys((*evidence_gaps, HYPOTHESIS_GAP)))
    if removed_evidence_free:
        evidence_gaps = tuple(dict.fromkeys((*evidence_gaps, (
            f"Advertified removed {removed_evidence_free} evidence-free model-suggested location "
            f"opportunity area{'s' if removed_evidence_free != 1 else ''} because no provider-resolved "
            "place or governed reference observation supported them."
        ))))
    return evidence_gaps


def validate_location_synthesis_grounding(
    request: LocationSynthesisRequest,
    output: AgentOutputEnvelope[LocationIntelligenceArtifact],
) -> None:
    artifact = output.artifact
    if artifact is None:
        return
    supplied_places = {item.place_id for item in request.location.resolved_places}
    reference_by_id = {item.observation_id: item for item in request.location.reference_evidence}
    if artifact.research_queries != request.location.research_plan.queries:
        raise ValueError("Location synthesis changed the approved research plan.")
    if artifact.resolved_places != request.location.resolved_places:
        raise ValueError("Location synthesis changed deterministic place-discovery facts.")
    if any(item not in artifact.evidence_gaps for item in request.location.research_plan.evidence_gaps):
        raise ValueError("Location synthesis omitted deterministic research evidence gaps.")
    for area in artifact.opportunities:
        _validate_area_references(request, area, supplied_places, reference_by_id)
        _validate_opportunity_area(area, reference_by_id)


def _validate_area_references(request, area, supplied_places: set, reference_by_id: dict) -> None:
    if any(item not in supplied_places for item in area.place_ids):
        raise ValueError("Location Intelligence cited a place that was not resolved by the provider.")
    if any(item not in reference_by_id for item in area.reference_observation_ids):
        raise ValueError("Location Intelligence cited a reference observation it was not supplied.")
    supported_geographies = {name.casefold() for name in supported_opportunity_geographies(request)}
    if area.geography.strip().casefold() not in supported_geographies:
        raise ValueError("Location synthesis introduced an unsupported opportunity geography.")


def supported_opportunity_geographies(request: LocationSynthesisRequest) -> tuple[str, ...]:
    names = (
        *request.location.geographies,
        *(item.anchor_geography for item in request.location.research_plan.queries),
        *(item.geography_name for item in request.location.reference_evidence),
    )
    return tuple(dict.fromkeys(name.strip() for name in names if name.strip()))


def _validate_opportunity_area(area, reference_by_id: dict) -> None:
    if not area.place_ids and not area.reference_observation_ids:
        raise ValueError("A Location opportunity area requires provider-resolved place or governed reference evidence.")
    inference_references = [reference_by_id[item] for item in area.reference_observation_ids
                            if reference_by_id[item].activation_policy not in CONTEXT_ONLY_POLICIES]
    if area.classification == "INFERENCE" and not inference_references:
        raise ValueError("An inferred opportunity area requires non-context-only reference evidence.")
    if not inference_references and area.confidence is not None:
        raise ValueError("POI-only or context-only hypotheses may not carry fabricated numeric confidence.")

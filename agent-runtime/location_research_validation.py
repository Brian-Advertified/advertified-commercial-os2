"""Validate and normalize bounded Location Intelligence research requests."""

from __future__ import annotations

from contracts import AgentOutputEnvelope
from location_intelligence_contracts import LocationResearchPlanArtifact, LocationResearchPlanRequest


def canonicalize_location_research(
    request: LocationResearchPlanRequest,
    output: AgentOutputEnvelope[LocationResearchPlanArtifact],
):
    artifact = output.artifact
    if artifact is None:
        return output
    reference_by_id = {item.observation_id: item for item in request.location.reference_evidence}
    category_labels = {item.code: item.label for item in request.location.available_poi_categories}
    retained, unsupported_removed, duplicate_removed, provenance_removed = _retain_research_queries(
        artifact.queries, reference_by_id, category_labels, _available_geographies(request)
    )
    revised = artifact.model_copy(update={
        "queries": tuple(retained),
        "rationale": _governed_research_rationale(retained),
        "evidence_gaps": _research_gaps(
            artifact.evidence_gaps, unsupported_removed, duplicate_removed, provenance_removed
        ),
    })
    return output.model_copy(update={"artifact": revised})


def _available_geographies(request: LocationResearchPlanRequest) -> set[str]:
    brief_geographies = {item.strip().casefold() for item in request.location.geographies}
    return brief_geographies | {
        item.geography_name.strip().casefold() for item in request.location.reference_evidence
    }


def _govern_research_query(query, reference_by_id: dict, category_labels: dict[str, str],
                          available_geographies: set[str]):
    if any(item not in reference_by_id for item in query.reference_observation_ids):
        raise ValueError("Location research cited a reference observation it was not supplied.")
    anchor_name = query.anchor_geography.strip()
    anchor = anchor_name.casefold()
    matching_reference_ids = tuple(
        item for item in query.reference_observation_ids
        if reference_by_id[item].geography_name.strip().casefold() == anchor
    )
    provenance_removed = len(query.reference_observation_ids) - len(matching_reference_ids)
    if query.poi_category not in category_labels or anchor not in available_geographies:
        return None, provenance_removed
    classification = (
        "INFERENCE"
        if query.classification == "INFERENCE" and matching_reference_ids
        else "HYPOTHESIS"
    )
    return query.model_copy(update={
        "anchor_geography": anchor_name,
        "classification": classification,
        "reference_observation_ids": matching_reference_ids,
        "purpose": (
            f"Verify {category_labels[query.poi_category]} POIs within {anchor_name} as contextual places "
            "requested or permitted by the research frame. POI existence does not establish target-audience "
            "presence or visitation."
        ),
    }), provenance_removed


def _retain_research_queries(queries, reference_by_id: dict, category_labels: dict[str, str],
                             available_geographies: set[str]):
    retained: list = []
    retained_by_key: dict = {}
    unsupported_removed = 0
    duplicate_removed = 0
    provenance_removed = 0
    for query in queries:
        governed, removed = _govern_research_query(
            query, reference_by_id, category_labels, available_geographies
        )
        provenance_removed += removed
        if governed is None:
            unsupported_removed += 1
            continue
        key = (governed.poi_category, governed.anchor_geography.casefold())
        if key in retained_by_key:
            duplicate_removed += 1
            index = retained_by_key[key]
            retained[index] = _merge_duplicate_query(retained[index], governed)
            continue
        retained_by_key[key] = len(retained)
        retained.append(governed)
    return retained, unsupported_removed, duplicate_removed, provenance_removed


def _merge_duplicate_query(existing, governed):
    merged_refs = tuple(dict.fromkeys((*existing.reference_observation_ids, *governed.reference_observation_ids)))
    return existing.model_copy(update={
        "priority": "REQUIRED" if "REQUIRED" in {existing.priority, governed.priority} else "PREFERRED",
        "classification": "INFERENCE" if merged_refs and "INFERENCE" in {
            existing.classification, governed.classification} else "HYPOTHESIS",
        "reference_observation_ids": merged_refs,
    })


def _governed_research_rationale(retained) -> str:
    if not retained:
        return "No provider-suggested place/POI research request passed the governed geography and provenance rules."
    return ("Research only the governed place/POI requests: " +
            "; ".join(f"{item.poi_category} in {item.anchor_geography}" for item in retained) + ".")


def _research_gaps(
    gaps,
    unsupported_removed: int,
    duplicate_removed: int,
    provenance_removed: int,
):
    if unsupported_removed:
        gaps = tuple(dict.fromkeys((*gaps, (
            f"Advertified omitted {unsupported_removed} provider-suggested location research request"
            f"{'s' if unsupported_removed != 1 else ''} because the geographic anchor or category was unsupported."
        ))))
    if provenance_removed:
        gaps = tuple(dict.fromkeys((*gaps, (
            f"Advertified removed {provenance_removed} supplied reference observation citation"
            f"{'s' if provenance_removed != 1 else ''} because the observation geography did not match "
            "the provider-suggested research anchor; requests without matching evidence remain hypotheses."
        ))))
    if duplicate_removed:
        gaps = tuple(dict.fromkeys((*gaps, (
            f"Advertified merged {duplicate_removed} duplicate provider-suggested location research request"
            f"{'s' if duplicate_removed != 1 else ''} by governed POI category and geographic anchor."
        ))))
    return gaps


def validate_location_research_grounding(
    request: LocationResearchPlanRequest,
    output: AgentOutputEnvelope[LocationResearchPlanArtifact],
) -> None:
    artifact = output.artifact
    if artifact is None:
        return
    reference_by_id = {item.observation_id: item for item in request.location.reference_evidence}
    available_geographies = _available_geographies(request)
    available_categories = {item.code for item in request.location.available_poi_categories}
    seen = set()
    for query in artifact.queries:
        key = (query.poi_category, query.anchor_geography.casefold().strip())
        if key in seen:
            raise ValueError("Location Intelligence returned duplicate place research requests.")
        seen.add(key)
        _validate_research_query(query, reference_by_id, available_geographies, available_categories)


def _validate_research_query(query, reference_by_id: dict, available_geographies: set[str],
                             available_categories: set[str]) -> None:
    if query.poi_category not in available_categories:
        raise ValueError("Location Intelligence requested an unsupported POI category.")
    if any(item not in reference_by_id for item in query.reference_observation_ids):
        raise ValueError("Location research cited a reference observation it was not supplied.")
    references = [reference_by_id[item] for item in query.reference_observation_ids]
    anchor = query.anchor_geography.strip().casefold()
    if anchor not in available_geographies:
        raise ValueError("Location research introduced an unsupported anchor geography.")
    if any(item.geography_name.strip().casefold() != anchor for item in references):
        raise ValueError("Location research retained provenance from a different geography.")
    if query.classification == "INFERENCE" and not references:
        raise ValueError("An inferred location research request requires cited evidence for its anchor geography.")

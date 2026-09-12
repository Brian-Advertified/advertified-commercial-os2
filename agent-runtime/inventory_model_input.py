"""Compact inventory provider projection retaining canonical selection facts."""
from planning_contracts import InventoryIntelligenceAgentRequest


def _inventory_model_input(request: InventoryIntelligenceAgentRequest) -> dict:
    return {
        "inventory": {
            "brief_version_id": str(request.inventory.brief_version_id),
            "shortlist_version_id": str(request.inventory.shortlist_version_id),
            "strategy": request.inventory.strategy.model_dump(mode="json")
            if request.inventory.strategy else None,
            "candidates": [
                _compact_inventory_candidate(candidate)
                for candidate in request.inventory.candidates
            ],
        },
    }


def _compact_inventory_candidate(candidate) -> dict:
    raw = candidate.model_dump(mode="json")
    benchmark = raw["benchmark"]
    return {
        "candidate_id": raw["candidate_id"],
        "name": raw["name"],
        "channel": raw["channel"],
        "geography": raw["geography"],
        "rate_amount_minor": raw["rate_amount_minor"],
        "currency": raw["currency"],
        "is_eligible": raw["is_eligible"],
        "rejection_reason": raw["rejection_reason"],
        "rejection_detail": raw["rejection_detail"],
        "score": raw["score"],
        "suitability_total": raw["suitability"]["total"],
        "suitability": raw["suitability"],
        "audience_fit": raw["audience_fit"],
        "benchmark": None if benchmark is None else {
            key: benchmark[key]
            for key in (
                "geography_basis",
                "cohort_size",
                "median_minor",
                "percentile",
                "position",
                "confidence",
            )
        },
    }



"""Project governed audience research to the provider while preserving citation boundaries."""

from collections import OrderedDict
from decimal import Decimal

from planning_contracts import AudienceAgentRequest
from planning_service import segment_support_reference_ids


_GROUP_FIELDS = (
    "source_title",
    "measurement_period",
    "geography_level",
    "geography_name",
    "stability_code",
    "sensitivity_code",
)
_METRIC_FIELDS = (
    "dimensions",
    "metric_code",
    "metric_value",
    "metric_unit",
)


def audience_model_input(request: AudienceAgentRequest) -> dict:
    """Expose compact market context without making it citable segment evidence.

    Segment-support observations retain their exact IDs in ``reference_evidence``.
    Aggregate/context-only observations are grouped by source/period/geography and moved
    to ``context_research`` without IDs. Grouping removes repeated provenance strings but
    preserves every supplied metric, dimensions, stability and sensitivity classification.
    Runtime provenance validation still evaluates the complete request-owned observation
    set after provider output.
    """
    payload = request.model_dump(mode="json", exclude={"invocation"})
    planning = payload["planning"]
    _project_budget_for_provider(planning)
    references = planning.get("reference_evidence", [])
    allowed = {str(value) for value in segment_support_reference_ids(request)}
    segment_support = [item for item in references if item["observation_id"] in allowed]
    context_only = [item for item in references if item["observation_id"] not in allowed]

    planning["reference_evidence"] = segment_support
    if context_only:
        planning["context_research"] = _group_context_research(context_only)
        payload["reference_policy_note"] = (
            f"{len(context_only)} supplied reference observation"
            f"{'s are' if len(context_only) != 1 else ' is'} contextual market research only. "
            "context_research groups those observations by source, period and geography to reduce repetition; "
            "each metrics entry is one retained observation. Use it for market context and bounded reasoning, "
            "but never cite it as segment proof or convert it into individual audience traits. Only "
            "reference_evidence observations may appear in reference_observation_ids."
        )
    return payload


def _project_budget_for_provider(planning: dict) -> None:
    minor = planning.pop("budget_minor", None)
    currency = planning.pop("currency", None)
    if minor is None:
        planning["budget"] = {"status": "UNKNOWN"}
        return
    major = Decimal(minor) / Decimal(100)
    amount = format(major, ",.2f").rstrip("0").rstrip(".")
    planning["budget"] = {
        "status": "SUPPLIED",
        "currency": currency,
        "amount_major": str(major),
        "display": f"{currency} {amount}" if currency else amount,
        "note": "amount_major is expressed in major currency units; do not interpret it as minor units.",
    }


def _group_context_research(items: list[dict]) -> list[dict]:
    groups: OrderedDict[tuple, dict] = OrderedDict()
    for item in items:
        key = tuple(_freeze(item.get(field)) for field in _GROUP_FIELDS)
        group = groups.get(key)
        if group is None:
            group = {field: item[field] for field in _GROUP_FIELDS if field in item}
            group["metrics"] = []
            groups[key] = group
        group["metrics"].append({field: item[field] for field in _METRIC_FIELDS if field in item})
    return list(groups.values())


def _freeze(value):
    if isinstance(value, dict):
        return tuple(sorted((key, _freeze(item)) for key, item in value.items()))
    if isinstance(value, list):
        return tuple(_freeze(item) for item in value)
    return value

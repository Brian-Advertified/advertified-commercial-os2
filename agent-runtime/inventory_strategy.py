"""Evidence requirements for relating inventory to approved strategy."""

from contracts import UnknownItem


INTERPRETATION_INSTRUCTION = (
    "Explain supplied deterministic inventory eligibility and benchmark facts without changing them. "
    "Connect each placement to the approved strategy: who it serves, the consumer need, "
    "message, moment, channel role and planned period. Use audience classifications and "
    "exclusions; approval is not proof of a hypothesis. Explain missing target reach, "
    "frequency, daypart, creative/exposure and route/POI evidence explicitly. Never infer "
    "habits or affluence from names or locations, claim incremental reach without measurements, "
    "or change scores or selection."
)


def strategy_unknowns(strategy) -> tuple[UnknownItem, ...]:
    if strategy is not None:
        return ()
    return (UnknownItem(
        field_path="inventory.strategy",
        question="Which approved audience strategy and channel roles justify these placements?",
        is_blocking=False,
    ),)

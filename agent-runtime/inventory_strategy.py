"""Evidence requirements for relating inventory to approved strategy."""

from contracts import UnknownItem


INTERPRETATION_INSTRUCTION = (
    "Explain supplied deterministic inventory eligibility, suitability and benchmark facts without changing them. "
    "Connect each placement only to approved strategy fields that are actually established: target audience, "
    "channel role, planned period and any retained need, buying or positioning context. If need state, buying "
    "context, positioning, target reach, frequency, daypart, creative/exposure or route/POI evidence is absent, "
    "state the gap instead of inventing it. Use audience classifications and exclusions; approval is not proof of "
    "a hypothesis. Never infer habits, affluence, identity or visitation from names or locations, claim incremental "
    "reach without measurements, or change deterministic scores, eligibility or human selection."
)


def strategy_unknowns(strategy) -> tuple[UnknownItem, ...]:
    unknowns: list[UnknownItem] = []
    if strategy.targeting_rationale is None:
        unknowns.append(UnknownItem(
            field_path="inventory.strategy.targeting_rationale",
            question="What human-approved targeting rationale should guide the inventory trade-off?",
            is_blocking=False,
        ))
    if strategy.positioning_statement is None:
        unknowns.append(UnknownItem(
            field_path="inventory.strategy.positioning_statement",
            question="What evidence-backed positioning direction, if any, should guide placement interpretation?",
            is_blocking=False,
        ))
    if any(item.need_state is None for item in strategy.audiences):
        unknowns.append(UnknownItem(
            field_path="inventory.strategy.audiences.need_state",
            question="Which verified consumer need states, if any, support the target audiences?",
            is_blocking=False,
        ))
    if any(item.buying_context is None for item in strategy.audiences):
        unknowns.append(UnknownItem(
            field_path="inventory.strategy.audiences.buying_context",
            question="Which verified buying contexts, if any, support the target audiences?",
            is_blocking=False,
        ))
    return tuple(unknowns)

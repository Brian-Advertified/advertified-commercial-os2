"""Approved human-facing Proposal facts and numeric grounding."""
import re
from decimal import Decimal

from master_data_codes import CURRENCY_MINOR_UNIT_DIGITS
from media_presentation import channel_labels

NUMBER = re.compile(r"(?<!\w)[+-]?\d[\d,]*(?:\.\d+)?%?")
MONEY = re.compile(r"\b([A-Z]{3})\s*([+-]?\d[\d,]*(?:\.\d+)?)|"
                   r"([+-]?\d[\d,]*(?:\.\d+)?)\s*([A-Z]{3})\b")


def format_money(amount_minor: int, currency: str) -> str:
    digits = CURRENCY_MINOR_UNIT_DIGITS[currency]
    major, minor = divmod(amount_minor, 10 ** digits)
    return (f"{currency} {major:,}" if minor == 0 or digits == 0
            else f"{currency} {major:,}.{minor:0{digits}d}")


def proposal_model_input(request) -> dict:
    proposal = request.proposal
    return {"proposal": {
        "brief_business_problem": proposal.brief_business_problem,
        "brief_objective": proposal.brief_objective,
        "success_measures": list(proposal.success_measures),
        "options": [{
            "label": option.label, "outcome": option.outcome,
            "approved_budget": format_money(option.budget_minor, option.currency),
            "channels": channel_labels(option.channels),
        } for option in proposal.options],
    }}


def approved_fact_lines(request) -> list[str]:
    proposal = request.proposal
    return [
        f"Approved business problem: {proposal.brief_business_problem}",
        f"Approved objective: {proposal.brief_objective}",
        *(["Approved success measures:", *proposal.success_measures] if proposal.success_measures else []),
        "Approved options:",
        *(f"{option.label}: {option.outcome} | {format_money(option.budget_minor, option.currency)} | "
          f"channels: {channel_labels(option.channels)}" for option in proposal.options),
    ]


def validate_proposal_numeric_claims(request, output) -> None:
    if output.artifact is None:
        return
    facts = "\n".join(approved_fact_lines(request))
    approved = _numbers(facts)
    proposed = _numbers(output.artifact.executive_summary)
    if (not proposed.issubset(approved)
            or not _money_claims(output.artifact.executive_summary).issubset(_money_claims(facts))):
        raise ValueError("Proposal narrative introduced a numeric claim absent from approved facts.")


def _numbers(text: str) -> set[tuple[Decimal, bool]]:
    return {(Decimal(value.rstrip("%").replace(",", "")), value.endswith("%"))
            for value in NUMBER.findall(text)}


def _money_claims(text: str) -> set[tuple[str, Decimal]]:
    return {(prefix or suffix, Decimal((before or after).replace(",", "")))
            for prefix, before, after, suffix in MONEY.findall(text)}

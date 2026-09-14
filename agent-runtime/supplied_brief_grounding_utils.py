"""Small shared helpers for supplied-Brief grounding and deterministic interpretation."""

import re
import unicodedata
from decimal import Decimal

from master_data_codes import CampaignModes, Currencies
from supplied_brief_contracts import SuppliedBriefRequest
from supplied_brief_model_input import PRIMARY_LOCATOR, source_segments


def citation_key(value: str) -> str:
    normalized = unicodedata.normalize("NFKC", value).casefold()
    return re.sub(r"[\W_]+", " ", normalized, flags=re.UNICODE).strip()


def primary_message_content(request: SuppliedBriefRequest) -> tuple[str, ...]:
    return tuple(
        segment["content"] for segment in source_segments(request)
        if segment["segment_id"] == PRIMARY_LOCATOR
    )


def exact_budget(request: SuppliedBriefRequest):
    candidates = [
        item.value
        for item in request.source.clarifications
        if item.field_path == "budget"
    ]
    if not candidates:
        candidates = [
            line
            for content in primary_message_content(request)
            for line in content.splitlines()
            if line.strip().casefold().startswith("budget:")
        ]
    matches = []
    operative_matches = []
    currency_pattern = "|".join(re.escape(item.value) for item in Currencies)
    money_pattern = rf"\b({currency_pattern})\s+([0-9][0-9, ]*(?:\.[0-9]{{1,2}})?)\b"
    for candidate in candidates:
        candidate_matches = re.findall(money_pattern, candidate)
        matches.extend(candidate_matches)
        for clause in re.split(r"[;\n]", candidate):
            if "operative" not in clause.casefold():
                continue
            clause_matches = re.findall(money_pattern, clause)
            if len(clause_matches) == 1:
                operative_matches.extend(clause_matches)
    selected = operative_matches if len(operative_matches) == 1 else matches
    if len(selected) != 1:
        selected = _explicit_base_budget(candidates, matches)
    if len(selected) != 1:
        return None
    currency, amount_text = selected[0]
    amount = Decimal(amount_text.replace(",", "").replace(" ", ""))
    return currency, int(amount * 100)


def _explicit_base_budget(candidates: list[str], matches: list[tuple[str, str]]):
    if len(matches) != 2 or len(candidates) != 1:
        return matches
    text = candidates[0].casefold()
    if not any(marker in text for marker in (
        "flexibility up to", "flexible up to", "with flexibility", "maximum", "max budget",
    )):
        return matches
    first = re.search(r"budget\s*:\s*", text)
    return [matches[0]] if first is not None else matches


def supports_campaign_mode(mode: str, excerpt: str) -> bool:
    key = citation_key(excerpt)
    if mode == CampaignModes.OOH_ONLY:
        has_ooh = any(term in key.split() for term in ("ooh", "dooh"))
        has_ooh = has_ooh or "out of home" in key or "outdoor" in key
        return has_ooh and "only" in key.split()
    if mode == CampaignModes.FULL_CAMPAIGN:
        return any(term in key for term in (
            "full campaign",
            "integrated campaign",
            "multichannel",
            "multi channel",
        ))
    return False

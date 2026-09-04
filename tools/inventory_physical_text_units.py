"""Independent sellable-unit derivation from physical document text."""

from __future__ import annotations

import re
from typing import Any

from inventory_physical_model import (
    CODE,
    DIMENSION,
    MONEY,
    NON_SELLABLE,
    PhysicalUnit,
    group_by_scope,
    locator_scope,
    meaningful_identity,
    normalize,
)


def extract_page_card_units(
    fragments: list[dict[str, Any]],
) -> list[PhysicalUnit]:
    result: list[PhysicalUnit] = []
    for scope, items in group_by_scope(fragments).items():
        combined = "\n".join(
            str(item.get("text") or "") for item in items
        )
        if not re.search(
            r"\bsite\s+(?:number|no|code)\b",
            combined,
            re.IGNORECASE,
        ):
            continue
        code_match = re.search(
            r"site\s+(?:number|no|code)\s*[:\n ]+([A-Z0-9 -]{3,20})",
            combined,
            re.IGNORECASE,
        )
        identity = code_match.group(1).strip() if code_match else ""
        title = next(
            (
                str(item.get("text") or "").strip()
                for item in items
                if meaningful_identity(str(item.get("text") or ""))
                and not re.search(
                    r"site\s+(?:number|no|code)",
                    str(item.get("text") or ""),
                    re.IGNORECASE,
                )
            ),
            "",
        )
        identity = " | ".join(
            value for value in (identity, title) if value
        )
        if not identity:
            continue
        raw_rate = preferred_page_rate(combined)
        result.append(PhysicalUnit(
            key=f"{scope}:page-card",
            locator=scope,
            scope=scope,
            kind="PAGE_CARD_SITE",
            identity=identity,
            raw_rate=raw_rate,
            evidence=tuple(
                str(item.get("text") or "")
                for item in items
                if item.get("text")
            ),
        ))
    return result


def preferred_page_rate(combined: str) -> str | None:
    for label in ("discounted rate", "rate card", "package cost", "cost"):
        pattern = re.compile(
            rf"{re.escape(label)}\s*[:\n ]+(?P<money>(?:ZAR|R)\s*\d[\d\s.,\u00a0]*)",
            re.IGNORECASE,
        )
        match = pattern.search(combined)
        if match:
            return match.group("money").strip().rstrip(".,")
    rates = list(MONEY.finditer(combined))
    return rates[-1].group(0).strip().rstrip(".,") if rates else None


def extract_presentation_site_units(
    fragments: list[dict[str, Any]],
    document_format: Any,
) -> list[PhysicalUnit]:
    if str(document_format).upper() != "PPTX":
        return []
    result: list[PhysicalUnit] = []
    for scope, items in group_by_scope(fragments).items():
        texts = [
            str(item.get("text") or "").strip() for item in items
        ]
        combined = "\n".join(texts)
        discovered_codes = [
            match.group(0) for match in CODE.finditer(combined)
        ]
        route_codes = {
            normalize(value)
            for value in discovered_codes
            if route_reference(value, combined)
        }
        codes = [
            value for value in discovered_codes
            if normalize(value) not in route_codes
        ]
        dimensions = [
            match.group(0) for match in DIMENSION.finditer(combined)
        ]
        rates = [
            match.group(0).strip() for match in MONEY.finditer(combined)
            if normalize(match.group(0)) not in route_codes
        ]
        location_blocks = [
            value for value in texts
            if presentation_location_block(value)
        ]
        if codes:
            code = codes[0]
            title = next(
                (
                    value
                    for value in texts
                    if value != code
                    and meaningful_identity(value)
                    and not DIMENSION.fullmatch(value)
                ),
                "",
            )
            result.append(PhysicalUnit(
                key=f"{scope}:pptx-site:{normalize(code)}",
                locator=scope,
                scope=scope,
                kind="PRESENTATION_SITE",
                identity=" | ".join(
                    value
                    for value in (
                        code,
                        title,
                        dimensions[0] if dimensions else "",
                    )
                    if value
                ),
                raw_rate=rates[-1] if rates else None,
                evidence=tuple(texts),
            ))
            continue
        for index, location in enumerate(location_blocks, start=1):
            result.append(PhysicalUnit(
                key=f"{scope}:pptx-location:{index}",
                locator=scope,
                scope=scope,
                kind="PRESENTATION_LOCATION",
                identity=location.replace("\n", " | "),
                raw_rate=rates[-1] if len(location_blocks) == 1 and rates else None,
                evidence=(location,),
            ))
    return result


def route_reference(value: str, combined: str) -> bool:
    del combined
    return bool(re.fullmatch(
        r"[RNM]\s*\d{1,3}",
        value,
        re.IGNORECASE,
    ))


def presentation_location_block(value: str) -> bool:
    normalized = normalize(value)
    if "\n" not in value or len(value) > 180:
        return False
    excluded = {
        "thank you",
        "digital network",
        "static ooh snapshot",
        "media deck 2026",
        "introduction",
        "contact us",
    }
    if normalized in excluded or any(
        phrase in normalized
        for phrase in (
            "where architecture meets advertising",
            "be remembered our media spectrum",
            "market digital network",
            "publisher media kit",
        )
    ):
        return False
    lines = [line.strip() for line in value.splitlines() if line.strip()]
    return 1 < len(lines) <= 4 and all(
        meaningful_identity(line) for line in lines
    )


def extract_catalogue_fallback_units(
    fragments: list[dict[str, Any]],
) -> list[PhysicalUnit]:
    product_terms = re.compile(
        r"\b(?:package|sponsorship|screen|billboard|site|placement|banner|"
        r"pre[- ]?roll|video|audio|radio spot|tv spot|social|advertorial|"
        r"takeover|roadblock|activation|event|podcast|influencer)\b",
        re.IGNORECASE,
    )
    result: list[PhysicalUnit] = []
    for scope, items in group_by_scope(fragments).items():
        candidates = []
        for item in items:
            value = str(item.get("text") or "").strip()
            normalized = normalize(value)
            if (
                not value
                or len(value) > 300
                or not product_terms.search(value)
                or normalized in {
                    "thank you", "contact us", "terms and conditions",
                    "media kit", "rate card", "introduction",
                }
            ):
                continue
            candidates.append((item, value))
        for index, (item, value) in enumerate(candidates[:8], start=1):
            locator = str(item.get("locator") or scope)
            result.append(PhysicalUnit(
                key=f"{scope}:catalogue:{index}",
                locator=locator,
                scope=scope,
                kind="CATALOGUE_ITEM",
                identity=" | ".join(value.splitlines())[:500],
                raw_rate=None,
                evidence=(value,),
            ))
    return result


def extract_priced_fragment_units(
    fragments: list[dict[str, Any]],
) -> list[PhysicalUnit]:
    result: list[PhysicalUnit] = []
    for item in fragments:
        text = str(item.get("text") or "").strip()
        if not text or NON_SELLABLE.search(text):
            continue
        locator = str(item.get("locator") or "")
        for index, match in enumerate(MONEY.finditer(text), start=1):
            prefix = text[: match.start()].strip(" \t\n:-|•")
            identity = prefix.splitlines()[-1].strip() if prefix else ""
            if not meaningful_identity(identity):
                continue
            result.append(PhysicalUnit(
                key=f"{locator}:price:{index}",
                locator=locator,
                scope=locator_scope(locator),
                kind="PRICED_TEXT",
                identity=identity,
                raw_rate=match.group(0).strip(),
                evidence=(text,),
            ))
    return result

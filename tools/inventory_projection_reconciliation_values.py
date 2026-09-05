"""Value normalization and commercial-unit views for projection reconciliation."""

from __future__ import annotations

import hashlib
import re
from pathlib import Path
from typing import Any


STOP = {
    "advertising", "and", "digital", "for", "from", "media", "not", "page",
    "per", "rate", "rates", "source", "stated", "the", "with",
}
MONEY_FIELD = re.compile(r"rate|price|cost|value|investment|invoice|amount|fee", re.I)
PAGE = re.compile(r"(?:page|slide|worksheet)=(\d+)")
SITE_REFERENCE = re.compile(r"(?:^|;)site=(\d+)(?:;|$)")
SITE_CANDIDATE = re.compile(r"(?:^|;)site-card=(\d+)(?:;|$)")


def file_sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def normalized(value: Any) -> str:
    return re.sub(r"[^a-z0-9]+", " ", str(value or "").lower()).strip()


def tokens(value: Any) -> set[str]:
    return {
        token for token in normalized(value).split()
        if len(token) > 1 and token not in STOP
    }


def scalar_values(value: Any) -> list[str]:
    if isinstance(value, dict):
        return [item for child in value.values() for item in scalar_values(child)]
    if isinstance(value, list):
        return [item for child in value for item in scalar_values(child)]
    return [] if value is None else [str(value)]


def source_page(locator: str) -> int | None:
    match = PAGE.search(locator or "")
    return int(match.group(1)) if match else None


def source_site(locator: str, pattern: re.Pattern[str]) -> int | None:
    match = pattern.search(locator or "")
    return int(match.group(1)) if match else None


def money_values(value: Any) -> set[str]:
    result = set()
    for item in scalar_values(value):
        for raw in re.findall(r"(?:R\s*)?\d[\d ,.]*", item, re.I):
            compact = re.sub(r"[^0-9,.]", "", raw)
            if not compact:
                continue
            if "," in compact and "." not in compact and len(compact.rsplit(",", 1)[-1]) == 2:
                compact = compact.replace(".", "").replace(",", ".")
            else:
                compact = compact.replace(",", "")
            try:
                result.add(f"{float(compact):.2f}")
            except ValueError:
                continue
    return result


def rate_variant_candidates(candidate: dict[str, Any]) -> list[dict[str, Any]]:
    values = candidate.get("values", {})
    variants = values.get("rateVariants") or []
    if not variants:
        return [candidate] if values.get("rateAmountMinor") is not None else []
    result = []
    for index, variant in enumerate(variants):
        prefix = f"rateVariant[{index:04d}]"
        identity_evidence = [
            item for item in candidate.get("evidence", [])
            if not str(item.get("fieldName") or "").startswith("rateVariant[")
            or str(item.get("fieldName") or "").startswith(prefix)
        ]
        item_values = {
            key: value for key, value in values.items()
            if key not in {"rateVariants", "rateAmountMinor", "rateType", "currency"}
        }
        item_values.update({
            "rateAmountMinor": variant.get("amountMinor"),
            "rateType": variant.get("rateType"),
            "currency": variant.get("currency"),
            "rateVariant": variant,
        })
        result.append({**candidate,
            "sourceLocator": variant.get("sourceLocator") or candidate.get("sourceLocator"),
            "values": item_values, "evidence": identity_evidence})
    return result


def reference_unit(reference: dict[str, Any]) -> str:
    fields = reference.get("expected_fields", {})
    keys = " ".join(str(key).lower() for key in fields)
    identity = normalized(reference.get("identity"))
    if "package" in keys or "package" in identity or "relationship" in fields:
        return "package"
    if "component" in keys or "component" in identity:
        return "component"
    if any(MONEY_FIELD.search(str(key)) for key in fields):
        return "rate_variant"
    return "product"


def candidate_units(candidate: dict[str, Any], unit: str) -> list[dict[str, Any]]:
    values = candidate.get("values", {})
    if unit == "rate_variant":
        return rate_variant_candidates(candidate)
    if unit == "package":
        return [candidate] if values.get("package") else []
    if unit == "component":
        return [candidate] if values.get("packageComponents") else []
    return [candidate]

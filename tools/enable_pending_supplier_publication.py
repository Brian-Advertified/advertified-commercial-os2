"""Ensure the prepared publication contract supports pending supplier prices."""

from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
INVENTORY_ROOT = (
    ROOT / "api" / "src" / "Advertified.Commercial.Infrastructure"
    / "Inventory"
)


def main() -> int:
    matches = []
    for path in INVENTORY_ROOT.glob("*.cs"):
        source = path.read_text(encoding="utf-8")
        if "record PreparedInventoryPublication" not in source:
            continue
        updated = source
        replacements = (
            (r"(?<!\?)\bstring\s+RateType\b", "string? RateType"),
            (r"(?<!\?)\bstring\s+Currency\b", "string? Currency"),
            (
                r"(?<!\?)(\blong|\bint|\bdecimal)\s+RateAmountMinor\b",
                r"\1? RateAmountMinor",
            ),
        )
        for pattern, replacement in replacements:
            updated = re.sub(pattern, replacement, updated)
        if not all(value in updated for value in (
            "string? RateType",
            "string? Currency",
            "RateAmountMinor",
        )):
            raise RuntimeError(
                "Prepared inventory publication pricing fields were not found."
            )
        path.write_text(updated, encoding="utf-8")
        matches.append(str(path.relative_to(ROOT)))
    if len(matches) != 1:
        raise RuntimeError(
            f"Expected one PreparedInventoryPublication declaration; found {matches}."
        )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

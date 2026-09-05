"""Synthetic source-unit comparison checks; no private inventory files are read."""

from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TOOLS = ROOT / "tools"
if str(TOOLS) not in sys.path:
    sys.path.insert(0, str(TOOLS))


def test_horizontal_rate_tables_handle_shifted_prices_and_continuations() -> None:
    from inventory_physical_table_units import extract_horizontal_units

    units = extract_horizontal_units([
        ["Platform", "Element", "Specifications", "CPM", ""],
        ["Display", "Leaderboard", "728 x 90 max 39k", "R365", ""],
        ["", "Medium Rectangle", "300 x 250 max 39k", "", "R365"],
        ["", "Half Page Advertisement", "300 x 600 max 39k", "", "R415"],
    ], "pdf:page=9;table=1", "ordinal:9")

    assert [unit.identity for unit in units] == [
        "Display | Leaderboard",
        "Display | Medium Rectangle",
        "Display | Half Page Advertisement",
    ]
    assert [unit.raw_rate for unit in units] == ["R365", "R365", "R415"]

    multiplier = extract_horizontal_units([
        ["Duration", "5s", "10s"],
        ["Multiply By", "0.5", "0.6"],
    ], "pdf:page=10;table=1", "ordinal:10")
    assert multiplier == []


def test_presentation_physical_units_ignore_route_numbers_and_taglines() -> None:
    from inventory_physical_text_units import extract_presentation_site_units

    route_units = extract_presentation_site_units([
        {"locator": "pptx:slide=10;shape=1", "text": "R24 BEDFORDVIEW"},
        {"locator": "pptx:slide=10;shape=2", "text": "TOGD007"},
    ], "PPTX")
    assert len(route_units) == 1
    assert "TOGD007" in route_units[0].identity
    assert route_units[0].raw_rate is None

    tagline_units = extract_presentation_site_units([
        {
            "locator": "pptx:slide=2;shape=1",
            "text": "where architecture\nmeets advertising.",
        },
        {
            "locator": "pptx:slide=3;shape=1",
            "text": "be remembered\nOur media spectrum ignites your brand's presence",
        },
    ], "PPTX")
    assert tagline_units == []

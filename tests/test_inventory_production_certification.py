"""Focused checks for the production corpus certification commands."""

from __future__ import annotations

import importlib.util
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TOOLS = ROOT / "tools"
if str(TOOLS) not in sys.path:
    sys.path.insert(0, str(TOOLS))


def load(name: str):
    path = TOOLS / f"{name}.py"
    spec = importlib.util.spec_from_file_location(name, path)
    assert spec and spec.loader
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


def test_physical_certifier_recognizes_dms_rows_and_money() -> None:
    from inventory_physical_evidence import read_physical_document
    from inventory_physical_model import money_fingerprint

    source_map = (
        ROOT / "artifacts" / "inventory-corpus" / "semantic-v1" /
        "2e2bb6e6a70bbd8c54b6d03deb3643d72126e1040752d7ef0e05c9ec456a25b5.json"
    )
    physical = read_physical_document(source_map)

    assert len(physical.units) == 4
    assert physical.units[1].raw_rate == "R1,10"
    assert money_fingerprint("ZAR 90 000.00") == "R90000.00"


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


def test_bedrock_evaluator_rejects_invented_price_and_date() -> None:
    module = load("evaluate_bedrock_inventory_corpus")
    values = {
        "rateAmountMinor": None,
        "commercialTerms": None,
    }
    assert module.validate_description(
        "This placement costs R90 000 and reaches a national audience.",
        values,
        {"placement", "national", "audience"},
    ) == "DESCRIPTION_INVENTED_PRICE"
    assert module.validate_description(
        "This placement is valid from 2026-01-01 for the campaign.",
        values,
        {"placement", "valid", "campaign"},
    ) == "DESCRIPTION_INVENTED_DATE"


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


def test_bedrock_evaluator_accepts_grounded_description() -> None:
    module = load("evaluate_bedrock_inventory_corpus")
    values = {
        "rateAmountMinor": 9_000_000,
        "commercialTerms": None,
    }
    assert module.validate_description(
        "Digital billboard on the N1 freeway near Centurion with a 15 second slot.",
        values,
        {"digital", "billboard", "freeway", "centurion", "second", "slot"},
    ) is None

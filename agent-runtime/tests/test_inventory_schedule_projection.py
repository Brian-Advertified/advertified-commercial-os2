"""Generic schedule-matrix projection regressions."""

from inventory_docling_rates import is_rate_header
from inventory_extraction_contracts import InventoryProjectionRequest
from inventory_extraction_service import project_inventory


def _cell(text: str, row: int, column: int, header: bool = False) -> dict:
    return {
        "text": text,
        "start_row_offset_idx": row,
        "start_col_offset_idx": column,
        "column_header": header,
    }


def test_money_in_timeslot_column_is_retained_in_dated_schedule_matrix():
    document = {
        "texts": [],
        "pictures": [],
        "tables": [{
            "prov": [{"page_no": 1}],
            "data": {"table_cells": [
                _cell("Time Slot", 0, 0, True),
                _cell("2026/05/12 Tuesday", 0, 1, True),
                _cell("2026/05/13 Wednesday", 0, 2, True),
                _cell("R16 500 21:30 Documentary", 1, 0),
                _cell("21:30 News R20 000", 1, 1),
            ]},
        }],
    }

    rows = project_inventory(InventoryProjectionRequest(
        provider_document=document
    )).rows
    variants = [
        variant
        for row in rows
        for variant in (row.rate_variants or ())
    ]

    assert sorted(variant.raw_value for variant in variants) == [
        "R16 500", "R20 000",
    ]
    assert len({variant.source_locator for variant in variants}) == 2


def test_explicit_spot_count_schedule_is_not_a_bare_number_rate_matrix():
    assert is_rate_header("No. of Spots") is False
    document = {
        "texts": [{
            "text": "Rate Card", "label": "section_header",
            "prov": [{"page_no": 1}],
        }],
        "pictures": [],
        "tables": [{
            "prov": [{"page_no": 1}],
            "data": {"table_cells": [
                _cell("No. of Spots", 0, 0, True),
                _cell("Schedule", 0, 1, True),
                _cell("A", 1, 0),
                _cell("06:00-09:00", 1, 1),
                _cell("6", 1, 2),
                _cell("1", 1, 3),
            ]},
        }],
    }

    assert project_inventory(InventoryProjectionRequest(
        provider_document=document
    )).rows == ()

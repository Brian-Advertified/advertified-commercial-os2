"""Generic text, product, and mixed-content projection regressions."""

from inventory_extraction_contracts import InventoryProjectionRequest
from inventory_extraction_service import (
    project_inventory,
    project_inventory_with_embedded_images,
)


def _cell(text: str, row: int, column: int) -> dict:
    return {
        "text": text,
        "start_row_offset_idx": row,
        "start_col_offset_idx": column,
    }


def test_unpriced_product_code_may_contain_internal_space():
    document = {
        "texts": [
            {"text": "CENTRAL PLAZA", "label": "list_item",
             "prov": [{"page_no": 1}]},
            {"text": "DSP 104A", "label": "list_item",
             "prov": [{"page_no": 1}]},
            {"text": "Site beside route", "label": "list_item",
             "prov": [{"page_no": 2}]},
            {"text": "R40", "label": "list_item",
             "prov": [{"page_no": 2}]},
            {"text": "Audience band", "label": "list_item",
             "prov": [{"page_no": 3}]},
            {"text": "SEM 5-7", "label": "list_item",
             "prov": [{"page_no": 3}]},
        ], "pictures": [], "tables": [],
    }

    rows = project_inventory(InventoryProjectionRequest(
        provider_document=document
    )).rows

    assert len(rows) == 1
    assert rows[0].values["productcode"] == "DSP 104A"


def test_combined_name_and_code_line_becomes_unpriced_inventory():
    document = {
        "texts": [{"text": "FOURWAYS | TOGD001", "label": "list_item",
                   "prov": [{"page_no": 1}]}],
        "pictures": [], "tables": [],
    }

    row = project_inventory(InventoryProjectionRequest(
        provider_document=document
    )).rows[0]

    assert row.values == {"name": "FOURWAYS", "productcode": "TOGD001"}


def test_sparse_picture_led_document_keeps_parent_text_and_ocr_results():
    picture = {"image": {
        "mimetype": "image/png",
        "uri": "data:image/png;base64,aW1hZ2U=",
    }}
    parent = {
        "texts": [{"text": "PARENT", "label": "text",
                   "prov": [{"page_no": 1}]}],
        "tables": [], "pictures": [picture, picture],
    }
    child = {
        "texts": [{"text": "CHILD", "label": "text",
                   "prov": [{"page_no": 1}]}],
        "tables": [], "pictures": [],
    }
    calls = []

    response = project_inventory_with_embedded_images(
        InventoryProjectionRequest(provider_document=parent),
        lambda *args: calls.append(args) or child,
    )

    assert len(calls) == 2
    assert {item.raw_value for item in response.source_elements} == {
        "PARENT", "CHILD",
    }


def test_money_followed_by_a_buying_unit_is_a_rate_without_memory():
    def text(value: str, label: str, top: int) -> dict:
        return {
            "text": value, "label": label,
            "prov": [{"page_no": 1, "bbox": {
                "l": 10, "t": top, "r": 200, "b": top - 20,
            }}],
        }
    document = {
        "texts": [
            text("PRODUCT A", "section_header", 400),
            text("location", "section_header", 300),
            text("R80 000,00", "section_header", 200),
            text("Format", "text", 180),
            text("Availability", "text", 160),
            text("Audience", "text", 140),
            text("Per month", "text", 100),
        ],
        "pictures": [], "tables": [],
    }

    row = project_inventory(InventoryProjectionRequest(
        provider_document=document
    )).rows[0]

    assert row.values["name"] == "PRODUCT A"
    assert row.values["rate"] == "R80 000,00"
    assert row.rate_variants[0].buying_unit == "per month"


def test_short_section_can_label_money_on_an_already_priced_page():
    cells = [
        _cell("Product", 0, 0), _cell("Rate", 0, 1),
        _cell("Morning", 1, 0), _cell("R1 000", 1, 1),
    ]
    cells[0]["column_header"] = cells[1]["column_header"] = True
    document = {
        "texts": [
            {"text": "REGIONAL", "label": "section_header",
             "prov": [{"page_no": 1, "bbox": {
                 "l": 10, "t": 300, "r": 100, "b": 280,
             }}]},
            {"text": "R1 575", "label": "text",
             "prov": [{"page_no": 1, "bbox": {
                 "l": 10, "t": 200, "r": 100, "b": 180,
             }}]},
        ],
        "pictures": [],
        "tables": [{"prov": [{"page_no": 1}],
                    "data": {"table_cells": cells}}],
    }

    rows = project_inventory(InventoryProjectionRequest(
        provider_document=document
    )).rows

    assert len(rows) == 2
    assert rows[1].extraction_method == "TEXT_RATE"
    assert rows[1].values["name"] == "REGIONAL"
    assert rows[1].values["rate"] == "R1 575"


def test_single_money_schedule_cell_under_day_header_is_a_rate():
    cells = [
        _cell("Day", 0, 0), _cell("Monday", 0, 1),
        _cell("Schedule", 1, 0), _cell("R4 500 07:00 Programme", 1, 1),
    ]
    cells[0]["column_header"] = cells[1]["column_header"] = True
    document = {
        "texts": [], "pictures": [],
        "tables": [{"prov": [{"page_no": 1}],
                    "data": {"table_cells": cells}}],
    }

    row = project_inventory(InventoryProjectionRequest(
        provider_document=document
    )).rows[0]

    assert row.values["rate"] == "R4 500"


def test_dense_schedule_cell_is_split_into_programme_rate_rows():
    cells = [
        _cell("Day", 0, 0), _cell("MONDAY", 0, 1),
        _cell("Schedule", 1, 0),
        _cell(
            "07:00 Show A R4 500 08:00 Show B R25 000",
            1, 1,
        ),
    ]
    cells[0]["column_header"] = cells[1]["column_header"] = True
    document = {
        "texts": [], "pictures": [],
        "tables": [{"prov": [{"page_no": 1}],
                    "data": {"table_cells": cells}}],
    }

    rows = project_inventory(InventoryProjectionRequest(
        provider_document=document
    )).rows

    assert [row.extraction_method for row in rows] == [
        "MIXED_CELL", "MIXED_CELL",
    ]
    assert [row.values["name"] for row in rows] == [
        "07:00 Show A", "08:00 Show B",
    ]
    assert [row.values["rate"] for row in rows] == [
        "R4 500", "R25 000",
    ]
    assert len({row.locator for row in rows}) == 1


def test_multiple_prices_in_one_generic_table_cell_are_separate_variants():
    cells = [
        _cell("Package", 0, 0), _cell("Options", 0, 1),
        _cell("Site takeover", 1, 0),
        _cell("R25 000 (x1) R50 000 (x2)", 1, 1),
    ]
    cells[0]["column_header"] = cells[1]["column_header"] = True
    document = {
        "texts": [], "pictures": [],
        "tables": [{"prov": [{"page_no": 1}],
                    "data": {"table_cells": cells}}],
    }

    row = project_inventory(InventoryProjectionRequest(
        provider_document=document
    )).rows[0]

    assert [item.raw_value for item in row.rate_variants] == [
        "R25 000", "R50 000",
    ]
    assert len({item.source_locator for item in row.rate_variants}) == 1


def test_multiple_prices_in_one_text_source_are_all_retained():
    document = {
        "texts": [{
            "text": "Campaign package VALUE: R120 875 INVESTMENT: R22 382",
            "label": "text", "prov": [{"page_no": 1, "bbox": {
                "l": 10, "t": 100, "r": 300, "b": 80,
            }}],
        }], "pictures": [], "tables": [],
    }

    rows = project_inventory(InventoryProjectionRequest(
        provider_document=document
    )).rows
    variants = [item for row in rows for item in row.rate_variants]

    assert [item.raw_value for item in variants] == ["R120 875", "R22 382"]
    assert len({item.source_locator for item in variants}) == 1


def test_commercial_description_links_adjacent_money_on_priced_page():
    def text(value: str, left: int) -> dict:
        return {
            "text": value, "label": "paragraph",
            "prov": [{"page_no": 1, "bbox": {
                "l": left, "t": 100, "r": left + 140, "b": 80,
            }}],
        }
    cells = [
        _cell("Product", 0, 0), _cell("Rate", 0, 1),
        _cell("Basic", 1, 0), _cell("R1 000", 1, 1),
    ]
    cells[0]["column_header"] = cells[1]["column_header"] = True
    document = {
        "texts": [text("Newsletter post", 10), text("R30 000", 200)],
        "pictures": [], "tables": [{"prov": [{"page_no": 1}],
                                     "data": {"table_cells": cells}}],
    }

    rows = project_inventory(InventoryProjectionRequest(
        provider_document=document
    )).rows
    rates = [item.raw_value for row in rows for item in row.rate_variants]

    assert rates == ["R1 000", "R30 000"]


def test_short_inline_product_price_on_a_rate_page_is_retained():
    def text(value: str, top: int) -> dict:
        return {
            "text": value, "label": "text",
            "prov": [{"page_no": 1, "bbox": {
                "l": 10, "t": top, "r": 300, "b": top - 20,
            }}],
        }
    document = {
        "texts": [
            text("Rate Card", 300),
            text("GQ / Glamour R26 000", 200),
            text("FULL CHICKEN AND LARGE CHIPS R14990 NOW ONLY", 100),
        ],
        "pictures": [], "tables": [],
    }

    rows = project_inventory(InventoryProjectionRequest(
        provider_document=document
    )).rows

    assert [row.values["rate"] for row in rows] == ["R26 000"]
    assert rows[0].values["name"] == "GQ / Glamour"


def test_self_contained_advertising_price_is_a_rate():
    document = {
        "texts": [{
            "text": "Indoor advertising package at R20 000 excluding production",
            "label": "text", "prov": [{"page_no": 1, "bbox": {
                "l": 10, "t": 100, "r": 300, "b": 80,
            }}],
        }], "pictures": [], "tables": [],
    }

    row = project_inventory(InventoryProjectionRequest(
        provider_document=document
    )).rows[0]

    assert row.values["rate"] == "R20 000"


def test_inline_spot_price_is_a_commercial_rate_not_unlinked_money():
    document = {
        "texts": [{
            "text": "The average spot on a channel is R3 750",
            "label": "text",
            "prov": [{"page_no": 1, "bbox": {
                "l": 10, "t": 100, "r": 300, "b": 80,
            }}],
        }],
        "pictures": [], "tables": [],
    }

    rows = project_inventory(InventoryProjectionRequest(
        provider_document=document
    )).rows

    assert len(rows) == 1
    assert rows[0].values["rate"] == "R3 750"
    assert rows[0].rate_variants[0].source_locator == (
        "docling:page=1;text=1"
    )


def test_inline_rate_and_adjacent_rate_are_both_preserved():
    def item(value: str, left: int) -> dict:
        return {
            "text": value, "label": "text",
            "prov": [{"page_no": 1, "bbox": {
                "l": left, "t": 100, "r": left + 140, "b": 80,
            }}],
        }
    document = {
        "texts": [
            item("Discounted Rate R 30 000", 10),
            item("R24 000", 200),
        ],
        "pictures": [], "tables": [],
    }

    rows = project_inventory(InventoryProjectionRequest(
        provider_document=document
    )).rows

    assert [row.values["rate"] for row in rows] == [
        "R 30 000", "R24 000",
    ]
    assert all(row.values["name"] == "Discounted Rate" for row in rows)


def test_concatenated_rate_label_links_a_separate_suffixed_value():
    def text(value: str, top: int) -> dict:
        return {
            "text": value, "label": "section_header" if top == 300 else "text",
            "prov": [{"page_no": 1, "bbox": {
                "l": 10, "t": top, "r": 200, "b": top - 20,
            }}],
        }

    request = InventoryProjectionRequest(provider_document={
        "texts": [text("MEDIARATE", 300), text("R65 000 Per Month", 200)],
        "pictures": [], "tables": [],
    })

    row = project_inventory(request).rows[0]

    assert row.values["name"] == "MEDIARATE"
    assert row.values["rate"] == "R65 000 Per Month"
    assert row.rate_variants[0].buying_unit == "per month"
    assert row.field_locators["rate"] == "docling:page=1;text=2"


def test_separate_slot_monthly_unit_makes_money_a_rate():
    def text(value: str, label: str) -> dict:
        return {
            "text": value, "label": label,
            "prov": [{"page_no": 1, "bbox": {
                "l": 10, "t": 100, "r": 200, "b": 80,
            }}],
        }

    response = project_inventory(InventoryProjectionRequest(provider_document={
        "texts": [text("R80 000,00", "section_header"),
                  text("Per Slot Monthly", "text")],
        "pictures": [], "tables": [],
    }))

    assert len(response.rows) == 1
    assert response.rows[0].values["rate"] == "R80 000,00"
    assert response.rows[0].rate_variants[0].buying_unit == "per slot monthly"

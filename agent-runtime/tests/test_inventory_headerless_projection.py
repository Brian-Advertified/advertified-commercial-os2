"""Generic headerless table projection regressions."""

from inventory_docling_rates import MONEY_PATTERN, is_table_rate
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


def test_numeric_metric_under_rate_group_is_not_a_rate():
    assert is_table_rate("INVESTMENT SUMMARY / EXPOSURE", "180") is False
    assert is_table_rate("INVESTMENT SUMMARY / VALUE", "180") is True


def test_money_accepts_common_thousands_and_decimal_conventions():
    assert MONEY_PATTERN.search("R 5,090.00").group(0) == "R 5,090.00"
    assert MONEY_PATTERN.search("R 5.090,00").group(0) == "R 5.090,00"
    assert MONEY_PATTERN.search("R 5 090,00").group(0) == "R 5 090,00"
    assert MONEY_PATTERN.search("R 250 000, 00").group(0) == "R 250 000, 00"


def test_grouped_money_stops_before_an_adjacent_percentage():
    match = MONEY_PATTERN.search("MONTHLY RATE: R27 495 10% Discount")

    assert match is not None
    assert match.group(0) == "R27 495"


def test_inline_table_rate_keeps_commercial_units_but_not_production_costs():
    assert is_table_rate("description", "1 X SOCIAL POST @ R500 EACH") is True
    assert is_table_rate("description", "R640 000 PACKAGE") is True
    assert is_table_rate("description", "Printing cost R500") is False
    assert is_table_rate("description", "Site beside route R44") is False


def test_marked_top_header_drives_numeric_price_rows_and_ignores_route_ids():
    cells = [
        _cell("name", 0, 0),
        _cell("base_price", 0, 1),
        _cell("currency", 0, 2),
        _cell("WC15 - R44 & R304", 1, 0),
        _cell("50000", 1, 1),
        _cell("ZAR", 1, 2),
        _cell("WC16 - Somerset", 2, 0),
        _cell("19200", 2, 1),
        _cell("ZAR", 2, 2),
    ]
    for cell in cells[:3]:
        cell["column_header"] = True
    cells[6]["column_header"] = True
    document = {
        "texts": [], "pictures": [],
        "tables": [{"prov": [{"page_no": 1}],
                    "data": {"table_cells": cells}}],
    }

    rows = project_inventory(InventoryProjectionRequest(
        provider_document=document
    )).rows

    assert [row.values["name"] for row in rows] == [
        "WC15 - R44 & R304", "WC16 - Somerset",
    ]
    assert [row.values["rate"] for row in rows] == ["50000", "19200"]
    assert [row.rate_variants[0].currency for row in rows] == ["ZAR", "ZAR"]
    assert all(
        variant.raw_value not in {"R44", "R304"}
        for row in rows for variant in row.rate_variants
    )


def test_marked_first_row_with_a_money_value_is_data_not_a_header():
    cells = [
        _cell("Publisher A", 0, 0), _cell("Site takeover", 0, 1),
        _cell("R30 000", 0, 2),
        _cell("Publisher B", 1, 0), _cell("Site takeover", 1, 1),
        _cell("R20 000", 1, 2),
    ]
    for cell in cells[:3]:
        cell["column_header"] = True
    document = {
        "texts": [], "pictures": [],
        "tables": [{"prov": [{"page_no": 1}],
                    "data": {"table_cells": cells}}],
    }

    rows = project_inventory(InventoryProjectionRequest(
        provider_document=document
    )).rows

    assert [row.values["rate"] for row in rows] == ["R30 000", "R20 000"]


def test_fact_card_may_contain_multiple_key_value_pairs_per_row():
    document = {
        "texts": [], "pictures": [],
        "tables": [{"prov": [{"page_no": 1}], "data": {"table_cells": [
            _cell("Product Code:", 0, 0), _cell("DSP104", 0, 1),
            _cell("Rate Card:", 0, 2), _cell("100000", 0, 3),
            _cell("City:", 1, 0), _cell("Metro", 1, 1),
            _cell("Discounted Rate Card:", 1, 2), _cell("80000", 1, 3),
        ]}}],
    }

    document["tables"][0]["data"]["table_cells"][0]["column_header"] = True
    row = project_inventory(InventoryProjectionRequest(
        provider_document=document
    )).rows[0]

    assert row.extraction_method == "KEY_VALUE"
    assert row.values["productcode"] == "DSP104"
    assert [item.raw_value for item in row.rate_variants] == ["100000", "80000"]


def test_key_value_table_may_reverse_label_and_value_per_row():
    document = {
        "texts": [], "pictures": [],
        "tables": [{"prov": [{"page_no": 1}], "data": {"table_cells": [
            _cell("Location", 0, 0), _cell("Main entrance", 0, 1),
            _cell("Digital", 1, 0), _cell("Platform", 1, 1),
            _cell("R 250 000, 00", 2, 0), _cell("Rate Card", 2, 1),
        ]}}],
    }

    rows = project_inventory(InventoryProjectionRequest(
        provider_document=document
    )).rows

    assert len(rows) == 1
    assert rows[0].extraction_method == "KEY_VALUE"
    assert rows[0].values["rate"] == "R 250 000, 00"


def test_single_descriptor_cell_with_inline_rate_is_retained():
    header = _cell("Details", 0, 0)
    header["column_header"] = True
    document = {
        "texts": [], "pictures": [],
        "tables": [{"prov": [{"page_no": 1}], "data": {"table_cells": [
            header, _cell("Average spot rate is R5 000", 1, 0),
        ]}}],
    }

    rows = project_inventory(InventoryProjectionRequest(
        provider_document=document
    )).rows

    assert len(rows) == 1
    assert rows[0].rate_variants[0].raw_value == "R5 000"


def test_unmarked_two_column_rate_card_is_one_key_value_product():
    cells = [
        _cell("Site No.", 0, 0), _cell("UNIT-1", 0, 1),
        _cell("Rate Card", 1, 0), _cell("R90 000", 1, 1),
        _cell("Discounted Rate", 2, 0), _cell("R52 500", 2, 1),
        _cell("Printing", 3, 0), _cell("R0", 3, 1),
    ]
    document = {
        "texts": [], "pictures": [],
        "tables": [{"prov": [{"page_no": 1}],
                    "data": {"table_cells": cells}}],
    }

    row = project_inventory(InventoryProjectionRequest(
        provider_document=document
    )).rows[0]

    assert row.extraction_method == "KEY_VALUE"
    assert row.values["productcode"] == "UNIT-1"
    assert row.values["printing"] == "R0"
    assert [item.raw_value for item in row.rate_variants] == [
        "R90 000", "R52 500",
    ]


def test_sparse_rate_fact_card_is_not_flattened_into_table_rows():
    labels_and_values = [
        ("Product Code", "UNIT-42"), ("Area", "Central"),
        ("Traffic Count", "12 500 per day"),
        ("Impacts", "450 000 per month"), ("Format", "Large display"),
        ("Coordinates", "-26.1, 28.0"), ("Rate Card", "R32 000"),
    ]
    cells = [
        _cell(text, row, column)
        for row, pair in enumerate(labels_and_values)
        for column, text in enumerate(pair)
    ]
    document = {
        "texts": [], "pictures": [],
        "tables": [{"prov": [{"page_no": 1}],
                    "data": {"table_cells": cells}}],
    }

    rows = project_inventory(InventoryProjectionRequest(
        provider_document=document
    )).rows

    assert len(rows) == 1
    assert rows[0].extraction_method == "KEY_VALUE"
    assert rows[0].values["productcode"] == "UNIT-42"
    assert rows[0].rate_variants[0].raw_value == "R32 000"


def test_spanned_physical_rate_cell_is_projected_once():
    header = _cell("2026/05/12 Tuesday", 0, 0)
    header["column_header"] = True
    rate = _cell("R76,000", 1, 0)
    rate["end_col_offset_idx"] = 3
    document = {
        "texts": [], "pictures": [],
        "tables": [{"prov": [{"page_no": 1}],
                    "data": {"table_cells": [header, rate]}}],
    }

    rows = project_inventory(InventoryProjectionRequest(
        provider_document=document
    )).rows

    variants = [item for row in rows for item in (row.rate_variants or ())]
    assert [item.raw_value for item in variants] == ["R76,000"]


def test_overlapping_physical_rate_cell_is_not_hidden_by_dense_grid():
    header = _cell("2026/05/12 Tuesday", 0, 0)
    header["column_header"] = True
    document = {
        "texts": [], "pictures": [],
        "tables": [{"prov": [{"page_no": 1}], "data": {"table_cells": [
            header, _cell("Morning programme", 1, 0),
            _cell("R90 000 19:30 Programme", 1, 0),
        ]}}],
    }

    rows = project_inventory(InventoryProjectionRequest(
        provider_document=document
    )).rows

    variants = [item for row in rows for item in (row.rate_variants or ())]
    assert [item.raw_value for item in variants] == ["R90 000"]


def test_image_code_pair_requires_inventory_context():
    def rows(context: str):
        texts = [{
            "text": value, "label": "text", "_advertified_virtual_page": 10001,
            "_advertified_locator_prefix": "docling:picture=1;child-page=1",
        } for value in (context, "DISPLAY", "DSP 104A")]
        return project_inventory(InventoryProjectionRequest(provider_document={
            "texts": texts, "pictures": [], "tables": [],
        })).rows

    assert rows("CAMPAIGN CREATIVE") == ()
    accepted = rows("SITE LOCATION")
    assert len(accepted) == 1
    assert accepted[0].values["productcode"] == "DSP 104A"


def test_first_priced_row_is_not_consumed_as_a_header():
    document = {
        "texts": [],
        "pictures": [],
        "tables": [{
            "prov": [{"page_no": 1}],
            "data": {"table_cells": [
                _cell("PREMIUM PACKAGE", 0, 0),
                _cell("R 67 000.00", 0, 1),
                _cell("1x Article", 1, 0),
            ]},
        }],
    }

    rows = project_inventory(InventoryProjectionRequest(
        provider_document=document
    )).rows

    assert [row.values["name"] for row in rows] == [
        "PREMIUM PACKAGE", "1x Article",
    ]
    assert rows[0].rate_variants[0].raw_value == "R 67 000.00"
    assert rows[0].rate_variants[0].header_hierarchy == "column_2"


def test_only_nearest_structural_row_precedes_unmarked_rate_data():
    document = {
        "texts": [],
        "pictures": [],
        "tables": [{
            "prov": [{"page_no": 1}],
            "data": {"table_cells": [
                _cell("Contact", 0, 0),
                _cell("012 345 6789", 0, 2),
                _cell("AUCTIONS", 1, 0),
                _cell("Full Colour", 2, 0),
                _cell("R 206.00", 2, 2),
            ]},
        }],
    }

    rows = project_inventory(InventoryProjectionRequest(
        provider_document=document
    )).rows

    assert len(rows) == 1
    assert rows[0].values["name"] == "Full Colour"
    assert rows[0].rate_variants[0].header_hierarchy == "column_3"
    assert "0123456789" not in rows[0].values


def test_bare_numbers_are_rates_only_in_a_current_source_daypart_rate_matrix():
    table = {
        "prov": [{"page_no": 1}],
        "data": {"table_cells": [
            _cell("MON-FRI", 0, 0),
            _cell("00:00-04:00", 1, 0),
            _cell("495", 1, 1),
            _cell("1 500", 1, 2),
        ]},
    }
    rate_text = {
        "text": "Rate Card 2026", "label": "section_header",
        "prov": [{"page_no": 1}],
    }
    request = InventoryProjectionRequest(provider_document={
        "texts": [rate_text], "pictures": [], "tables": [table],
    })

    row = project_inventory(request).rows[0]

    assert row.values["name"] == "00:00-04:00"
    assert [item.raw_value for item in row.rate_variants] == ["495", "1 500"]
    assert all(item.currency is None for item in row.rate_variants)

    without_rate_context = InventoryProjectionRequest(provider_document={
        "texts": [], "pictures": [], "tables": [table],
    })
    assert project_inventory(without_rate_context).rows == ()


def test_noncommercial_row_headers_do_not_become_inventory_products():
    row_header = _cell("Audience segment", 1, 0)
    row_header["row_header"] = True
    table = {
        "prov": [{"page_no": 1}],
        "data": {"table_cells": [
            _cell("Demographics", 0, 0), row_header,
            _cell("52%", 1, 1),
        ]},
    }
    request = InventoryProjectionRequest(provider_document={
        "texts": [], "pictures": [], "tables": [table],
    })

    assert project_inventory(request).rows == ()


def test_unpriced_name_and_product_code_pair_becomes_inventory():
    document = {
        "texts": [
            {"text": "CLAREMONT", "label": "list_item",
             "prov": [{"page_no": 1}]},
            {"text": "WCD002", "label": "list_item",
             "prov": [{"page_no": 1}]},
            {"text": "CLAREMONT", "label": "list_item",
             "prov": [{"page_no": 2}]},
            {"text": "55%", "label": "list_item",
             "prov": [{"page_no": 2}]},
        ],
        "pictures": [], "tables": [],
    }

    rows = project_inventory(InventoryProjectionRequest(
        provider_document=document
    )).rows

    assert len(rows) == 1
    assert rows[0].extraction_method == "TEXT_PRODUCT"
    assert rows[0].values == {
        "name": "CLAREMONT", "productcode": "WCD002",
    }




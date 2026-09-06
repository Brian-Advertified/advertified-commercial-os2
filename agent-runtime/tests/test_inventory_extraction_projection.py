"""Generic inventory projection regressions against Docling-native structure."""

import pytest
from pydantic import ValidationError

from inventory_docling_rates import is_rate_header
from inventory_extraction_contracts import InventoryProjectionRequest
from inventory_extraction_service import (
    project_inventory,
    project_inventory_with_embedded_images,
)


def _request(document):
    return InventoryProjectionRequest(provider_document=document)


def _provenance(page: int, left: int, top: int, right: int, bottom: int):
    return [{"page_no": page, "bbox": {
        "l": left, "t": top, "r": right, "b": bottom,
    }}]


def _text(text: str, page: int, label: str, box=(10, 100, 100, 90)):
    return {"text": text, "label": label, "prov": _provenance(page, *box)}


def _cell(
    text: str,
    row: int,
    column: int,
    header: bool = False,
    row_header: bool = False,
):
    return {
        "text": text,
        "start_row_offset_idx": row,
        "start_col_offset_idx": column,
        "column_header": header,
        "row_header": row_header,
    }


def _table(page: int, code: str, latitude: str):
    pairs = [
        ("Site No.", code),
        ("Dimensions", "3m x 12m"),
        ("GPS Coordinates", f"{latitude}, 28.1000"),
        ("Production", "R 7 380 (R 4 680 + R 2 700)"),
        ("Availability", "Immediately"),
    ]
    return {
        "prov": _provenance(page, 30, 300, 360, 60),
        "data": {"table_cells": [
            _cell(value, row, column)
            for row, pair in enumerate(pairs)
            for column, value in enumerate(pair)
        ]},
    }


def test_projection_request_accepts_only_the_current_docling_document():
    document = {"texts": [], "tables": [], "pictures": []}

    assert _request(document).model_dump() == {"provider_document": document}
    with pytest.raises(ValidationError):
        InventoryProjectionRequest(
            provider_document=document,
            source_hash="a" * 64,
            document_class="PDF",
        )


def test_key_value_card_uses_source_structure_not_filename_or_supplier_branch():
    document = {
        "texts": [
            _text("NORTH MEDIA INVENTORY", 1, "section_header"),
            _text("Northern approach", 3, "section_header",
                  (400, 505, 680, 489)),
            _text("SITE INFO: Faces commuter traffic", 3, "caption"),
            _text("Rate Card", 3, "text", (41, 430, 90, 420)),
            _text("R 45 000", 3, "text", (148, 431, 200, 421)),
        ],
        "tables": [_table(3, "UNIT-1", "-26.1000")],
    }
    response = project_inventory(_request(document))

    row = response.rows[0]
    assert "suppliername" not in row.values
    assert row.values["sectioncontext"] == "NORTH MEDIA INVENTORY"
    assert row.values["productcode"] == "UNIT-1"
    assert row.values["name"] == "Northern approach"
    assert row.values["latitude"] == "-26.1000"
    assert row.values["productioncost"].startswith("R 7 380")
    assert row.rate_variants[0].raw_value == "R 45 000"
    assert row.rate_variants[0].header_hierarchy == "Rate Card"


def test_repeated_codes_on_different_pages_remain_separate_products():
    document = {
        "texts": [
            _text("GENERIC INVENTORY", 1, "section_header"),
            _text("Eastern face", 3, "section_header"),
            _text("Western face", 4, "section_header"),
        ],
        "tables": [
            _table(3, "UNIT-1", "-26.1000"),
            _table(4, "UNIT-1", "-26.2000"),
        ],
    }
    response = project_inventory(_request(document))

    assert len(response.rows) == 2
    assert {row.values["latitude"] for row in response.rows} == {
        "-26.1000", "-26.2000",
    }


@pytest.mark.parametrize(
    "rate_header", ["Rate", "Rates", "Price", "Value"]
)
def test_two_column_grid_is_not_mistaken_for_one_key_value_card(rate_header):
    document = {
        "texts": [],
        "tables": [{
            "prov": _provenance(1, 30, 300, 360, 60),
            "data": {"table_cells": [
                _cell("Product Code", 0, 0, header=True),
                _cell(rate_header, 0, 1, header=True),
                _cell("UNIT-1", 1, 0),
                _cell("R 10 000", 1, 1),
                _cell("UNIT-2", 2, 0),
                _cell("R 20 000", 2, 1),
            ]},
        }],
    }
    response = project_inventory(_request(document))

    assert [row.values["productcode"] for row in response.rows] == [
        "UNIT-1", "UNIT-2",
    ]
    assert [
        row.rate_variants[0].raw_value for row in response.rows
    ] == ["R 10 000", "R 20 000"]


def test_first_generic_descriptor_column_becomes_the_row_name():
    document = {
        "texts": [],
        "tables": [{
            "prov": _provenance(1, 30, 300, 360, 60),
            "data": {"table_cells": [
                _cell("Main body advertising", 0, 0, header=True),
                _cell("Rate", 0, 1, header=True),
                _cell("Colour", 1, 0),
                _cell("R 355.00", 1, 1),
            ]},
        }],
    }

    row = project_inventory(_request(document)).rows[0]

    assert row.values["name"] == "Colour"
    assert row.field_locators["name"] == "docling:page=1;table=1;row=2;cell=1"


def test_rate_vocabulary_does_not_match_substrings_inside_other_words():
    assert is_rate_header("Corporate package") is False


def test_unlabelled_money_matrix_is_retained_but_production_cost_is_not_a_rate():
    document = {
        "texts": [],
        "tables": [{
            "prov": _provenance(1, 30, 300, 360, 60),
            "data": {"table_cells": [
                _cell("Product", 0, 0, header=True),
                _cell("Paging / 4 to 8", 0, 1, header=True),
                _cell("Production", 0, 2, header=True),
                _cell("Inserts", 1, 0),
                _cell("R 951.00", 1, 1),
                _cell("R 100.00", 1, 2),
                _cell("1x Article", 2, 0),
            ]},
        }],
    }

    rows = project_inventory(_request(document)).rows
    row = rows[0]

    assert row.values["name"] == "Inserts"
    assert row.values["productioncost"] == "R 100.00"
    assert [item.raw_value for item in row.rate_variants] == ["R 951.00"]
    assert row.rate_variants[0].header_hierarchy == "Paging / 4 to 8"
    assert rows[1].values["name"] == "1x Article"


def test_unpriced_form_table_does_not_become_inventory_rows():
    document = {
        "texts": [],
        "tables": [{
            "prov": _provenance(1, 30, 300, 360, 60),
            "data": {"table_cells": [
                _cell("Field", 0, 0, header=True),
                _cell("Value", 0, 1, header=True),
                _cell("DATE", 1, 0),
                _cell("AGENCY", 2, 0),
            ]},
        }],
    }

    assert project_inventory(_request(document)).rows == ()


def test_compact_inline_price_is_retained_with_value_removed_from_header():
    document = {
        "texts": [_text(
            "Hosting fee per month Price available upon request R 55 000 PM",
            1, "text",
        )],
        "tables": [],
        "pictures": [],
    }

    row = project_inventory(_request(document)).rows[0]

    assert row.values["rate"] == "R 55 000"
    assert row.values["name"] == (
        "Hosting fee per month Price available upon request PM"
    )


@pytest.mark.parametrize("label", [
    "PLEASE NOTE RATES ARE NET AND EXCLUDE VAT.",
    "Rates: 01 November 2024",
])
def test_commercial_prose_is_not_used_as_a_text_rate_label(label):
    document = {
        "texts": [
            _text(label, 1, "text", (10, 300, 300, 280)),
            _text("R 13 500", 1, "text", (10, 200, 90, 180)),
        ],
        "tables": [],
        "pictures": [],
    }

    assert project_inventory(_request(document)).rows == ()


def test_text_rate_uses_bounded_reading_order_when_geometry_is_separated():
    document = {
        "texts": [
            _text("GENERIC OFFERS", 1, "section_header",
                  (10, 400, 180, 380)),
            _text("Cost Per Month", 1, "text",
                  (10, 300, 120, 280)),
            _text("24", 1, "text", (10, 210, 30, 190)),
            _text("R 10 000", 1, "text", (10, 100, 90, 80)),
        ],
        "tables": [],
        "pictures": [],
    }

    response = project_inventory(_request(document))

    assert len(response.rows) == 1
    row = response.rows[0]
    assert row.extraction_method == "TEXT_RATE"
    assert row.values == {
        "name": "Cost Per Month",
        "rate": "R 10 000",
        "sectioncontext": "GENERIC OFFERS",
    }
    assert row.field_locators == {
        "name": "docling:page=1;text=2",
        "rate": "docling:page=1;text=4",
        "sectioncontext": "docling:page=1;text=1",
    }
    assert row.rate_variants[0].source_locator == "docling:page=1;text=4"
    assert row.rate_variants[0].header_locators == (
        "docling:page=1;text=2",
    )


def _image_only_parent():
    return {
        "groups": [{
            "name": "Offers",
            "children": [{"$ref": "#/pictures/0"}],
        }],
        "texts": [],
        "tables": [],
        "pictures": [{
            "parent": {"$ref": "#/groups/0"},
            "image": {
                "mimetype": "image/png",
                "uri": "data:image/png;base64,c291cmNlLWltYWdl",
            },
        }],
    }


def _image_child():
    cells = [
        _cell("Platform", 0, 0),
        _cell("Width", 0, 2, header=True),
        _cell("Height", 0, 3, header=True),
        _cell("Format", 0, 4, header=True),
        _cell("Rate", 0, 5, header=True),
        _cell("Streaming", 1, 0, row_header=True),
        _cell("Ad Unit", 1, 1),
        _cell("Width", 1, 2),
        _cell("Height", 1, 3),
        _cell("Format", 1, 4),
        _cell("Rate", 1, 5),
        _cell("On-demand video", 2, 0, row_header=True),
        _cell("Pre-roll", 2, 1),
        _cell("16 9", 2, 3),
        _cell("MP4", 2, 4),
        _cell("R2,50", 2, 5),
    ]
    return {
        "texts": [_text(
            "DIGITAL INVENTORY RATE CARD", 1, "section_header"
        )],
        "tables": [{
            "prov": _provenance(1, 10, 200, 500, 20),
            "data": {"table_cells": cells},
        }],
        "pictures": [],
    }


def test_image_only_wrapper_uses_docling_again_and_preserves_parent_lineage():
    parent = _image_only_parent()
    child = _image_child()
    calls = []

    def convert(filename: str, media_type: str, content: bytes):
        calls.append((filename, media_type, content))
        return child

    response = project_inventory_with_embedded_images(
        _request(parent),
        convert,
    )

    assert calls == [("embedded-picture-1.png", "image/png", b"source-image")]
    assert len(response.rows) == 1
    row = response.rows[0]
    assert row.values["name"] == "On-demand video"
    assert row.values["placement"] == "Pre-roll"
    assert row.values["height"] == "16 9"
    assert row.values["sectioncontext"] == "DIGITAL INVENTORY RATE CARD"
    assert row.rate_variants[0].raw_value == "R2,50"
    assert row.locator.startswith(
        "docling:group=Offers;picture=1;child-page=1;table=1"
    )
    assert any(
        item.locator.startswith(
            "docling:group=Offers;picture=1;child-page=1;table=1"
        )
        for item in response.source_elements
    )
    assert response.warnings[0] == (
        "1 embedded picture blocks were interpreted through Docling OCR."
    )


def test_structured_document_does_not_repeat_docling_for_retained_pictures():
    document = {
        "texts": [],
        "tables": [{
            "prov": _provenance(1, 10, 200, 500, 20),
            "data": {"table_cells": [
                _cell("Product Code", 0, 0, header=True),
                _cell("Rate", 0, 1, header=True),
                _cell("UNIT-1", 1, 0),
                _cell("R 10 000", 1, 1),
            ]},
        }],
        "pictures": [{"image": {"uri": "external.png"}}],
    }

    def unexpected(*_):
        raise AssertionError("converter must not be called")

    response = project_inventory_with_embedded_images(
        _request(document),
        unexpected,
    )

    assert len(response.rows) == 1
    assert response.rows[0].values["productcode"] == "UNIT-1"

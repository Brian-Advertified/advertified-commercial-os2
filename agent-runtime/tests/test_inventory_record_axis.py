"""Physical evidence remains immutable when records traverse columns."""
import pytest
from pydantic import ValidationError

from inventory_schema_contracts import RecordSchema, SourceCell
from inventory_schema_service import validate_schema_grounding
from test_inventory_schema_agent import envelope, mapping, proposal, request


@pytest.mark.parametrize("axis", ["DIAGONAL", ""])
def test_unknown_axis_is_not_a_schema(axis):
    record = proposal().records[0].model_dump()
    record["record_axis"] = axis
    with pytest.raises(ValidationError):
        RecordSchema.model_validate(record)


@pytest.mark.parametrize("axis,accepted", [("ROW", False), ("COLUMN", True)])
def test_binding_bound_matches_physical_record_axis(axis, accepted):
    source = request()
    cells = [
        SourceCell(locator="h-product", row=700, column=1, raw_text="Product"),
        SourceCell(locator="r2-product", row=700, column=2, raw_text="UNSEEN-835"),
    ]
    structure = source.document.representative_structures[0].model_copy(update={"cells": cells})
    source = source.model_copy(update={"document": source.document.model_copy(
        update={"representative_structures": [structure]})})
    artifact = proposal(mapping(column=700))
    artifact = artifact.model_copy(update={"records": [
        artifact.records[0].model_copy(update={"record_axis": axis})]})
    before = source.model_dump_json()
    if accepted:
        validate_schema_grounding(source, envelope(artifact))
        assert source.model_dump_json() == before
    else:
        with pytest.raises(ValueError, match="source binding"):
            validate_schema_grounding(source, envelope(artifact))


def test_legacy_schema_defaults_to_row_traversal():
    record = proposal().records[0].model_dump()
    del record["record_axis"]
    assert RecordSchema.model_validate(record).record_axis == "ROW"

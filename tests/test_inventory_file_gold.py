"""Comparator regressions use explicit synthetic expectations, never private files."""

from copy import deepcopy

import pytest

from tools.evaluate_inventory_file_gold import evaluate


def evidence_pair():
    gold = {
        "datasetVersion": "synthetic-comparator/1",
        "documentId": "a" * 64,
        "relativePath": "unfamiliar-source.pdf",
        "goldCells": [{"recordKey": "row-1", "field": "price", "value": "R1,10"}],
        "safetyExpectations": {
            "requiredCandidateCount": 1,
            "expectedNullNormalizedRateRecordKeys": ["row-1"],
            "expectedRateAmountMinor": {"row-1": None},
            "expectedUnknownFields": ["rate_type", "rate_valid_from"],
            "publicationAllowed": False,
        },
    }
    observed = {
        "sourceHash": "a" * 64,
        "status": "REVIEW_REQUIRED",
        "candidates": [{
            "rowNumber": 1,
            "status": "REVIEW_REQUIRED",
            "values": {"name": "Unfamiliar placement", "rateAmountMinor": None},
            "evidence": [{
                "fieldName": "rate", "rawValue": "R1,10",
                "normalizedValue": None, "sourceLocator": "pdf:page=1;cell=2",
            }],
        }],
    }
    return gold, observed


def test_comparison_does_not_require_known_supplier_or_private_inventory():
    gold, observed = evidence_pair()
    assert evaluate(observed, gold)["passed"]
    renamed = deepcopy(gold)
    renamed["relativePath"] = "entirely-different-name.pdf"
    assert evaluate(observed, renamed)["passed"]


@pytest.mark.parametrize("change, failure", [
    ("hash", "source_hash_mismatch"),
    ("rate", "ambiguous_rate_was_normalized:row-1"),
    ("basis", "expected_unknown_populated:row-1:rate_type"),
    ("date", "expected_unknown_populated:row-1:rate_valid_from"),
    ("publication", "publication_occurred"),
])
def test_comparison_rejects_changed_source_invented_facts_or_publication(change, failure):
    gold, observed = evidence_pair()
    candidate = observed["candidates"][0]
    if change == "hash":
        observed["sourceHash"] = "b" * 64
    elif change == "rate":
        candidate["values"]["rateAmountMinor"] = 110
        candidate["evidence"][0]["normalizedValue"] = "110"
    elif change == "basis":
        candidate["values"]["rateType"] = "FLAT_RATE"
    elif change == "date":
        candidate["values"]["commercialTerms"] = {"rateValidFrom": "2026-01-01"}
    else:
        candidate["status"] = "PUBLISHED"
    result = evaluate(observed, gold)
    assert not result["passed"]
    assert failure in result["failures"]

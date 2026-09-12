"""Reject catalogue drift or contradictory test evidence before reconciling a run."""
import json
import xml.etree.ElementTree as ET

import pytest

from business_scenarios.api_scenario_receipts import load_receipt, NS
from business_scenarios.backend_acceptance_catalog import BACKEND_ACCEPTANCE_SCENARIOS
from business_scenarios.backend_scenario_executor import unavailable

SCENARIO = next(case for case in BACKEND_ACCEPTANCE_SCENARIOS if case.scenario_id == "OPP-005")


def write_fixture(directory, change):
    # Synthetic receipt fixture tests the reconciliation boundary, not the business scenario.
    record = unavailable(SCENARIO, "fixture", 0).model_dump(mode="json")
    record.update(
        source_inputs={"definition": SCENARIO.source_inputs, "observed": {"fixture": True}},
        invariant_results={item.code: True for item in SCENARIO.expected_invariants},
        execution_status="EXECUTED", passed=True, failure_reasons=[],
    )
    outcome = "Passed"
    if change == "identity":
        record["scenario_id"] = "OPP-004"
    elif change == "inputs":
        record["source_inputs"]["definition"] = {}
    elif change == "invariants":
        record["invariant_results"] = {}
    elif change == "failed-test":
        outcome = "Failed"
    elif change == "failed-invariant":
        record["invariant_results"][next(iter(record["invariant_results"]))] = False
    receipts = directory / "scenarios"
    receipts.mkdir(parents=True)
    (receipts / "OPP-005.json").write_text(json.dumps(record), encoding="utf-8")
    root = ET.Element(f"{{{NS['t']}}}TestRun")
    ET.SubElement(root, f"{{{NS['t']}}}UnitTestResult", {
        "testName": 'Fixture.CanonicalOpportunityScenario(scenarioId: "OPP-005")',
        "outcome": outcome,
    })
    ET.ElementTree(root).write(directory / "api.trx", encoding="utf-8")


@pytest.mark.parametrize("change", ["identity", "inputs", "invariants", "failed-test", "failed-invariant"])
def test_contradictory_receipt_is_rejected(tmp_path, change):
    write_fixture(tmp_path, change)
    with pytest.raises(ValueError):
        load_receipt(SCENARIO, [tmp_path])


def test_matching_execution_is_linked_and_duplicate_evidence_rejected(tmp_path):
    write_fixture(tmp_path, None)
    receipt = load_receipt(SCENARIO, [tmp_path])
    assert receipt.passed
    provenance = receipt.actual_result["api_execution_evidence"]
    assert provenance["test_outcome"] == "Passed"
    assert len(provenance["receipt_sha256"]) == 64
    assert provenance["verification_scope"] == "RETAINED_EXECUTION_NOT_A_NEW_API_RUN"
    with pytest.raises(ValueError, match="Multiple"):
        load_receipt(SCENARIO, [tmp_path, tmp_path])


def test_partition_receipt_is_linked_but_duplicate_execution_is_rejected(tmp_path):
    write_fixture(tmp_path, None)
    original = tmp_path / "api.trx"
    partition = tmp_path / "scenario-opportunity.trx"
    original.rename(partition)
    receipt = load_receipt(SCENARIO, [tmp_path])
    assert receipt.passed
    assert receipt.actual_result["api_execution_evidence"]["trx_path"] == str(partition)
    original.write_bytes(partition.read_bytes())
    with pytest.raises(ValueError, match="Exactly one"):
        load_receipt(SCENARIO, [tmp_path])

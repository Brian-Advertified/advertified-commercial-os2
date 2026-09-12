"""Reconcile actual C# scenario receipts against their pinned verifier TRX results."""
import hashlib
from pathlib import Path
import xml.etree.ElementTree as ET

from business_scenarios.backend_acceptance_catalog import BackendScenarioRunRecord

NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}


def load_receipt(scenario, evidence_directories):
    candidates = [Path(directory) / "scenarios" / (scenario.scenario_id + ".json")
                  for directory in evidence_directories]
    candidates = [path for path in candidates if path.is_file()]
    if not candidates:
        return None
    if len(candidates) != 1:
        raise ValueError(f"Multiple API receipts exist for {scenario.scenario_id}.")
    path = candidates[0]
    encoded = path.read_bytes()
    receipt = BackendScenarioRunRecord.model_validate_json(encoded)
    if receipt.scenario_id != scenario.scenario_id or receipt.category != scenario.category:
        raise ValueError("API receipt identity does not match the selected scenario.")
    if receipt.source_inputs.get("definition") != scenario.source_inputs:
        raise ValueError("API receipt inputs do not match the current catalogue.")
    expected = {item.code for item in scenario.expected_invariants}
    if set(receipt.invariant_results) != expected:
        raise ValueError("API receipt invariant keys do not match the current catalogue.")
    marker = f'(scenarioId: "{scenario.scenario_id}")'
    matches = []
    for trx_path in sorted(path.parent.parent.glob("*.trx")):
        trx = trx_path.read_bytes()
        document = ET.fromstring(trx)
        matches.extend((trx_path, trx, item)
                       for item in document.findall(".//t:UnitTestResult", NS)
                       if item.attrib.get("testName", "").endswith(marker))
    if len(matches) != 1:
        raise ValueError("Exactly one matching API test execution is required.")
    trx_path, trx, result = matches[0]
    results = [result]
    outcome = results[0].attrib.get("outcome")
    if receipt.passed and (outcome != "Passed" or not all(receipt.invariant_results.values())
                           or receipt.execution_status != "EXECUTED" or receipt.failure_reasons):
        raise ValueError("A passing API receipt disagrees with its actual test evidence.")
    if not receipt.passed and outcome == "Passed":
        raise ValueError("A failed API receipt disagrees with its test result.")
    provenance = dict(
        receipt_path=str(path.resolve()), receipt_sha256=hashlib.sha256(encoded).hexdigest(),
        trx_path=str(trx_path.resolve()), trx_sha256=hashlib.sha256(trx).hexdigest(),
        test_name=results[0].attrib["testName"], test_outcome=outcome,
        verification_scope="RETAINED_EXECUTION_NOT_A_NEW_API_RUN",
    )
    return receipt.model_copy(update={"actual_result": {
        **receipt.actual_result, "api_execution_evidence": provenance,
    }})

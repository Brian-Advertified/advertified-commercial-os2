"""Retained API discovery must reconcile exactly across bounded test hosts."""
import importlib.util
from pathlib import Path
import xml.etree.ElementTree as ET

import pytest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("partition_reconciliation",
                                            ROOT / "tools/reconcile-api-partitions.py")
assert SPEC and SPEC.loader
reconciliation = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(reconciliation)
NS = reconciliation.NAMESPACE["t"]
TEST = "Advertified.Fixture.Acceptance"


def write_partitions(directory, defect):
    (directory / "discovered-tests.txt").write_text("    " + TEST + "\n", encoding="utf-8")
    exits = []
    for index, name in enumerate(reconciliation.PARTITIONS):
        tests = [TEST] if index == 0 else []
        if defect == "missing":
            tests = []
        if defect == "duplicate" and index == 1:
            tests = [TEST]
        if defect == "unexpected" and index == 1:
            tests = ["Advertified.Fixture.Unexpected"]
        passed = defect != "failed" or not tests
        root = ET.Element(f"{{{NS}}}TestRun")
        results = ET.SubElement(root, f"{{{NS}}}Results")
        for test in tests:
            ET.SubElement(results, f"{{{NS}}}UnitTestResult", {
                "testName": test, "outcome": "Passed" if passed else "Failed",
            })
        summary = ET.SubElement(root, f"{{{NS}}}ResultSummary", {"outcome": "Completed"})
        ET.SubElement(summary, f"{{{NS}}}Counters", {
            "total": str(len(tests)), "passed": str(len(tests) if passed else 0),
        })
        ET.ElementTree(root).write(directory / f"{name}.trx", encoding="utf-8")
        exits.append(f"{name}\t{1 if defect == 'exit' and index == 0 else 0}")
    (directory / "partition-exit-codes.tsv").write_text("\n".join(exits), encoding="utf-8")


@pytest.mark.parametrize("defect", [None, "missing", "duplicate", "unexpected", "failed", "exit"])
def test_partition_gate_requires_exact_complete_passing_execution(tmp_path, defect):
    write_partitions(tmp_path, defect)
    result = reconciliation.reconcile(tmp_path)
    assert result["complete"] is (defect is None)
    assert result["discovered"] == 1

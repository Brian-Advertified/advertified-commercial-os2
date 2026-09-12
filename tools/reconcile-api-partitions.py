"""Reconcile retained test discovery with disjoint pinned-API test receipts."""
from __future__ import annotations

import json
import sys
from collections import Counter
from pathlib import Path
import xml.etree.ElementTree as ET

PARTITIONS = ("unit", "database", "migration", "recovery", "scenario-opportunity",
              "scenario-inventory", "scenario-planning", "scenario-transaction",
              "scenario-funding", "scenario-measurement")
NAMESPACE = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}


def reconcile(directory: Path) -> dict:
    discovered = Counter(
        line.strip()
        for line in (directory / "discovered-tests.txt").read_text(encoding="utf-8").splitlines()
        if line.startswith("    Advertified.")
    )
    actual: Counter[str] = Counter()
    partitions = []
    for name in PARTITIONS:
        root = ET.parse(directory / f"{name}.trx").getroot()
        summary = root.find("t:ResultSummary", NAMESPACE)
        if summary is None:
            raise ValueError(f"{name}: missing test result summary")
        counters_element = summary.find("t:Counters", NAMESPACE)
        if counters_element is None:
            raise ValueError(f"{name}: missing test counters")
        counters = counters_element.attrib
        results = root.findall("t:Results/t:UnitTestResult", NAMESPACE)
        actual.update(result.attrib["testName"] for result in results)
        partitions.append({
            "name": name, "outcome": summary.attrib["outcome"], "counters": counters,
            "passed": summary.attrib["outcome"] == "Completed"
            and len(results) == int(counters["total"]) == int(counters["passed"])
            and all(result.attrib["outcome"] == "Passed" for result in results),
        })
    missing = list((discovered - actual).elements())
    unexpected = list((actual - discovered).elements())
    duplicates = [name for name, count in actual.items() if count != 1]
    exits = (directory / "partition-exit-codes.tsv").read_text(encoding="utf-8").splitlines()
    return {
        "schemaVersion": "advertified.api-partition-reconciliation.v1",
        "discovered": sum(discovered.values()), "executed": sum(actual.values()),
        "missing": missing, "unexpected": unexpected, "duplicates": duplicates,
        "partitions": partitions,
        "complete": bool(discovered) and not missing and not unexpected and not duplicates
        and all(partition["passed"] for partition in partitions)
        and exits == [f"{name}\t0" for name in PARTITIONS],
    }


def main() -> int:
    directory = Path(sys.argv[1]).resolve(strict=True)
    record = reconcile(directory)
    (directory / "reconciliation.json").write_text(
        json.dumps(record, indent=2) + "\n", encoding="utf-8",
    )
    print(f"API discovery: {record['discovered']}; executed: {record['executed']}; "
          f"complete: {record['complete']}")
    return 0 if record["complete"] else 1


if __name__ == "__main__":
    raise SystemExit(main())

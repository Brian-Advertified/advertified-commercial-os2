"""Certify all Bedrock enrichment against physical inventory baselines."""

from __future__ import annotations

import argparse
import json
from datetime import UTC, datetime
from pathlib import Path
from typing import Any

import requests

from inventory_ai_cost_ledger import (
    load_policy,
    read_inventory_semantic_runs,
    validate_budget,
)
from inventory_bedrock_certification_policy import (
    certify_document,
    validate_ledger_content,
)
from inventory_corpus_api import InventoryApi
from inventory_physical_facts import load_source

REPO_ROOT = Path(__file__).resolve().parents[1]
CORPUS_ROOT = REPO_ROOT / "artifacts" / "inventory-corpus"
TENANT_ID = "10000000-0000-0000-0000-000000000020"


def main() -> int:
    args = parse_args()
    root = args.evidence.resolve(strict=True)
    require_physical_certification(root)

    client = InventoryApi(
        args.api_base_url, args.origin, args.tenant_id
    )
    client.start_session()
    sources = client.request(
        "GET", client.tenant_path("/inventory-semantic-preflight")
    ).get("sources") or []
    if len(sources) != 43:
        raise RuntimeError(
            "The live semantic source set is not the 43-file corpus."
        )

    records = certify_sources(root, client, sources)
    policy = load_policy()
    ledger_entries = read_inventory_semantic_runs()
    cost = validate_budget(ledger_entries, policy)
    certified_hashes = {
        str(item["sourceHash"]) for item in sources
    }
    failures = document_failures(records)
    failures.extend(cost["failures"])
    failures.extend(validate_ledger_content(
        ledger_entries, policy, certified_hashes
    ))
    report = build_report(records, cost, failures)

    output = root / "bedrock-certification"
    output.mkdir(parents=True, exist_ok=True)
    write_json(
        output / "corpus-bedrock-certification.json",
        report,
    )
    (output / "CORPUS_BEDROCK_CERTIFICATION.md").write_text(
        render_markdown(report), encoding="utf-8"
    )
    print_summary(report)
    return 0 if report["verdict"] == "PASS" else 2


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--evidence", type=Path, default=CORPUS_ROOT)
    parser.add_argument(
        "--api-base-url", default="http://127.0.0.1:5197"
    )
    parser.add_argument("--origin", default="http://localhost:3017")
    parser.add_argument("--tenant-id", default=TENANT_ID)
    return parser.parse_args()


def require_physical_certification(root: Path) -> None:
    physical = read_json(
        root / "physical-certification"
        / "corpus-physical-certification.json"
    )
    if (
        physical.get("verdict") != "PASS"
        or physical.get("passedSourceCount") != 43
    ):
        raise RuntimeError(
            "All 43 physical files must pass before Bedrock certification."
        )


def certify_sources(
    root: Path,
    client: InventoryApi,
    sources: list[dict[str, Any]],
) -> list[dict[str, Any]]:
    records = []
    for source in sorted(
        sources,
        key=lambda item: item["fileName"].lower(),
    ):
        source_hash = str(source["sourceHash"])
        baseline = read_json(
            root / "physical-certification" / "baseline"
            / f"{source_hash}.json"
        )
        physical = load_source(
            root / "semantic-v1" / f"{source_hash}.json"
        )
        current = client.read_complete_import(
            str(source["importId"])
        )
        records.append(certify_document(
            source,
            baseline,
            current,
            physical.searchable_text,
        ))
    return records


def document_failures(
    records: list[dict[str, Any]],
) -> list[str]:
    return [
        f"{item['fileName']}:{failure}"
        for item in records
        for failure in item["failures"]
    ]


def build_report(
    records: list[dict[str, Any]],
    cost: dict[str, Any],
    failures: list[str],
) -> dict[str, Any]:
    return {
        "schemaVersion": (
            "advertified.inventory-bedrock-certification.v1"
        ),
        "generatedAtUtc": datetime.now(UTC).isoformat(),
        "verdict": "PASS" if not failures else "FAIL",
        "sourceCount": len(records),
        "passedSourceCount": sum(item["passed"] for item in records),
        "failedSourceCount": sum(
            not item["passed"] for item in records
        ),
        "candidateCount": sum(
            item["candidateCount"] for item in records
        ),
        "changedPhysicalFieldCount": sum(
            item["changedPhysicalFieldCount"] for item in records
        ),
        "missingClassificationCount": sum(
            item["missingClassificationCount"] for item in records
        ),
        "missingDescriptionCount": sum(
            item["missingDescriptionCount"] for item in records
        ),
        "cost": cost,
        "failures": failures,
        "documents": records,
    }


def print_summary(report: dict[str, Any]) -> None:
    cost = report["cost"]
    print(json.dumps({
        "verdict": report["verdict"],
        "sourceCount": report["sourceCount"],
        "passedSourceCount": report["passedSourceCount"],
        "candidateCount": report["candidateCount"],
        "changedPhysicalFieldCount": report[
            "changedPhysicalFieldCount"
        ],
        "missingClassificationCount": report[
            "missingClassificationCount"
        ],
        "missingDescriptionCount": report[
            "missingDescriptionCount"
        ],
        "actualKnownTotalUsd": (
            cost["actualKnownTotalUsdMicros"] / 1_000_000
        ),
        "budgetAccountedTotalUsd": (
            cost["budgetAccountedTotalUsdMicros"] / 1_000_000
        ),
        "remainingBudgetUsd": (
            cost["remainingBudgetUsdMicros"] / 1_000_000
        ),
        "failureCount": len(report["failures"]),
    }, indent=2))


def render_markdown(report: dict[str, Any]) -> str:
    cost = report["cost"]
    rows = [
        "# Corpus Bedrock certification",
        "",
        f"Verdict: **{report['verdict']}**",
        "",
        "| Measure | Result |",
        "|---|---:|",
        f"| Sources | {report['sourceCount']} |",
        f"| Passed sources | {report['passedSourceCount']} |",
        f"| Candidates | {report['candidateCount']} |",
        f"| Changed physical fields | {report['changedPhysicalFieldCount']} |",
        f"| Missing classifications | {report['missingClassificationCount']} |",
        f"| Missing descriptions | {report['missingDescriptionCount']} |",
        f"| Budget-accounted total | ${cost['budgetAccountedTotalUsdMicros'] / 1_000_000:.6f} |",
        f"| Remaining budget | ${cost['remainingBudgetUsdMicros'] / 1_000_000:.6f} |",
        "",
        "| File | Candidates | Physical changes | Missing class | Missing description | Verdict |",
        "|---|---:|---:|---:|---:|---|",
    ]
    for item in report["documents"]:
        rows.append(
            f"| {escape(item['fileName'])} | {item['candidateCount']}"
            f" | {item['changedPhysicalFieldCount']}"
            f" | {item['missingClassificationCount']}"
            f" | {item['missingDescriptionCount']}"
            f" | {'PASS' if item['passed'] else 'FAIL'} |"
        )
    rows.append("")
    return "\n".join(rows)


def escape(value: str) -> str:
    return value.replace("|", "\\|").replace("\n", " ")


def read_json(path: Path) -> dict[str, Any]:
    value = json.loads(
        path.resolve(strict=True).read_text(encoding="utf-8")
    )
    if not isinstance(value, dict):
        raise RuntimeError(f"Expected an object in {path}.")
    return value


def write_json(path: Path, value: dict[str, Any]) -> None:
    path.write_text(
        json.dumps(value, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (
        OSError,
        KeyError,
        ValueError,
        RuntimeError,
        json.JSONDecodeError,
        requests.RequestException,
    ) as error:
        print(json.dumps({"verdict": "ERROR", "error": str(error)}))
        raise SystemExit(3)

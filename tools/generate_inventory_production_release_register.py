"""Generate the local production-release register for corpus inventory.

This command is read-only: it never calls Bedrock and never mutates imports.
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any

import requests

from inventory_corpus_api import InventoryApi
from inventory_production_release_policy import build_record, build_register

TENANT_ID = "10000000-0000-0000-0000-000000000020"
REPO_ROOT = Path(__file__).resolve().parents[1]
MANIFEST_PATH = REPO_ROOT / "artifacts" / "inventory-corpus" / "source-manifest.json"
GOLD_ROOT = REPO_ROOT / "artifacts" / "inventory-corpus" / "gold"
CERTIFICATION_ROOT = REPO_ROOT / "artifacts" / "inventory-corpus" / "certification"
OUTPUT_ROOT = REPO_ROOT / "artifacts" / "inventory-corpus" / "production-release"


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--api", default="http://127.0.0.1:5197")
    parser.add_argument("--runtime", default="http://127.0.0.1:5198")
    parser.add_argument("--origin", default="http://localhost:3017")
    parser.add_argument("--tenant", default=TENANT_ID)
    parser.add_argument("--output-root", type=Path, default=OUTPUT_ROOT)
    args = parser.parse_args()

    runtime = read_runtime(args.runtime)
    api = InventoryApi(args.api, args.origin, args.tenant)
    api.start_session()
    preflight = api.request(
        "GET", api.tenant_path("/inventory-semantic-preflight")
    )
    manifest = read_json(MANIFEST_PATH)
    manifest_documents = {
        item["sha256"]: item for item in manifest.get("documents", [])
    }

    records = []
    for source in sorted(
        preflight.get("sources") or [],
        key=lambda value: str(value.get("fileName", "")).lower(),
    ):
        source_hash = str(source["sourceHash"])
        gold_present, gold_passed = file_gold_status(source_hash)
        records.append(build_record(
            source,
            api.read_complete_import(str(source["importId"])),
            manifest_documents.get(source_hash),
            gold_present=gold_present,
            gold_passed=gold_passed,
        ))

    register = build_register(runtime, preflight, manifest, records)
    args.output_root.mkdir(parents=True, exist_ok=True)
    json_path = args.output_root / "corpus-release-register.json"
    markdown_path = args.output_root / "PRODUCTION_RELEASE_GATE.md"
    write_json(json_path, register)
    markdown_path.write_text(render_markdown(register), encoding="utf-8")
    print(json.dumps(console_summary(register, json_path, markdown_path), indent=2))
    return 0 if register["softwareLaunchGate"] == "GO" else 1


def read_runtime(base_url: str) -> dict[str, Any]:
    if not base_url.startswith(("http://127.0.0.1", "http://localhost")):
        raise ValueError("Runtime verification is restricted to localhost.")
    response = requests.get(base_url.rstrip("/") + "/health/ready", timeout=10)
    response.raise_for_status()
    payload = response.json()
    checks = payload.get("checks") or []
    return {
        "status": payload.get("status"),
        "checks": checks,
        "deterministicZeroCost": "deterministic-zero-cost" in checks,
    }


def file_gold_status(source_hash: str) -> tuple[bool, bool]:
    if not (GOLD_ROOT / f"{source_hash}.json").exists():
        return False, False
    for path in (
        CERTIFICATION_ROOT / f"{source_hash}.local-evaluation.json",
        CERTIFICATION_ROOT / f"{source_hash}.evaluation.json",
    ):
        if path.exists():
            value = read_json(path)
            return (
                True,
                bool(value.get("passed"))
                and value.get("documentId") == source_hash,
            )
    return True, False


def console_summary(
    register: dict[str, Any],
    json_path: Path,
    markdown_path: Path,
) -> dict[str, Any]:
    summary = register["summary"]
    return {
        "softwareLaunchGate": register["softwareLaunchGate"],
        "corpusPublicationGate": register["corpusPublicationGate"],
        "sourceCount": summary["sourceCount"],
        "certifiedSourceCount": summary["certifiedSourceCount"],
        "quarantinedSourceCount": summary["quarantinedSourceCount"],
        "publishedCandidateCount": summary["publishedCandidateCount"],
        "bedrockLiveExecutionEnabled": register["bedrock"][
            "liveExecutionEnabled"
        ],
        "bedrockCommittedCostUsdMicros": register["bedrock"][
            "committedCostUsdMicros"
        ],
        "jsonRegister": str(json_path.relative_to(REPO_ROOT)),
        "markdownGate": str(markdown_path.relative_to(REPO_ROOT)),
    }


def render_markdown(register: dict[str, Any]) -> str:
    summary = register["summary"]
    rows = [
        "# Advertified inventory production release gate",
        "",
        f"Generated: `{register['generatedAtUtc']}`",
        "",
        "## Release decision",
        "",
        f"- Software launch: **{register['softwareLaunchGate']}**",
        f"- Corpus publication: **{register['corpusPublicationGate']}**",
        "- Uncertified corpus inventory remains quarantined.",
        "- Bedrock remains disabled for this release.",
        "",
        "## Corpus status",
        "",
        "| Measure | Result |",
        "|---|---:|",
        f"| Source files | {summary['sourceCount']} |",
        f"| Physically certified files | {summary['certifiedSourceCount']} |",
        f"| Quarantined files | {summary['quarantinedSourceCount']} |",
        f"| Active candidates | {summary['activeCandidateCount']} |",
        f"| Core-empty candidates | {summary['coreEmptyCandidateCount']} |",
        f"| Minimum-complete candidates | {summary['minimumCompleteCandidateCount']} |",
        f"| Blocking candidate reviews | {summary['blockingCandidateCount']} |",
        f"| Published candidates | {summary['publishedCandidateCount']} |",
        "",
        "## File register",
        "",
        "| File | Format | Candidates | Core-empty | Certification | Publication |",
        "|---|---|---:|---:|---|---|",
    ]
    for item in register["files"]:
        rows.append(
            "| " + escape_markdown(item["file_name"])
            + " | " + item["document_class"]
            + f" | {item['candidate_count']}"
            + f" | {item['candidates_with_no_core_fields']}"
            + " | " + item["extraction_certification"]
            + " | " + item["publication_disposition"] + " |"
        )
    rows.extend([
        "",
        "## Boundary",
        "",
        "This gate permits deployment of the software while prohibiting publication "
        "of uncertified corpus inventory. A file moves out of quarantine only after "
        "its physical-file gold evaluation passes and human review is complete.",
        "",
    ])
    return "\n".join(rows)


def escape_markdown(value: str) -> str:
    return value.replace("|", "\\|").replace("\n", " ")


def read_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.resolve(strict=True).read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError(f"Expected an object in {path}.")
    return value


def write_json(path: Path, value: dict[str, Any]) -> None:
    path.write_text(
        json.dumps(value, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )


if __name__ == "__main__":
    raise SystemExit(main())

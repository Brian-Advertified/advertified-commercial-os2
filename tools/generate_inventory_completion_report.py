"""Generate the final production-style inventory and journey completion report."""

from __future__ import annotations

import argparse
import json
import subprocess
from datetime import UTC, datetime
from pathlib import Path
from typing import Any

import requests

ROOT = Path(__file__).resolve().parents[1]
CORPUS = ROOT / "artifacts" / "inventory-corpus"


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--validation",
        type=Path,
        default=CORPUS / "completion" / "validation-steps.json",
    )
    args = parser.parse_args()

    physical = read_json(
        CORPUS / "physical-certification"
        / "corpus-physical-certification.json"
    )
    bedrock = read_json(
        CORPUS / "bedrock-certification"
        / "corpus-bedrock-certification.json"
    )
    cost = read_json(
        CORPUS / "ai-cost" / "inventory-ai-cost-report.json"
    )
    publication = read_json(
        CORPUS / "publication" / "corpus-publication.json"
    )
    canary_cost = read_json(
        CORPUS / "ai-cost" / "brief-proposal-canary-cost.json"
    )
    validation = read_json(args.validation)
    containers = docker_state()
    health = health_state()

    failures = []
    require(
        physical.get("verdict") == "PASS"
        and int(physical.get("passedSourceCount") or 0) == 43,
        failures,
        "PHYSICAL_CERTIFICATION_NOT_43_OF_43",
    )
    require(
        bedrock.get("verdict") == "PASS"
        and int(bedrock.get("passedSourceCount") or 0) == 43,
        failures,
        "BEDROCK_CERTIFICATION_NOT_43_OF_43",
    )
    require(
        int(bedrock.get("changedPhysicalFieldCount") or 0) == 0,
        failures,
        "BEDROCK_CHANGED_PHYSICAL_FACTS",
    )
    require(
        bool(cost.get("passed"))
        and int(cost.get("budgetAccountedTotalUsdMicros") or 0)
        <= 5_000_000,
        failures,
        "US5_BUDGET_NOT_SATISFIED",
    )
    require(
        publication.get("verdict") == "PASS"
        and int(publication.get("sourceCount") or 0) == 43,
        failures,
        "CORPUS_PUBLICATION_NOT_43_OF_43",
    )
    require(
        int(publication.get("publishedProductCount") or 0)
        == int(publication.get("approvedCandidateCount") or -1),
        failures,
        "PUBLISHED_PRODUCT_COUNT_MISMATCH",
    )
    require(
        int(publication.get("lineageSourceCount") or 0) == 43,
        failures,
        "PUBLISHED_LINEAGE_NOT_43_OF_43",
    )
    require(
        bool(canary_cost.get("passed")),
        failures,
        "BRIEF_PROPOSAL_CANARY_COST_FAILED",
    )
    require(
        validation.get("verdict") == "PASS",
        failures,
        "FINAL_VALIDATION_BATCH_FAILED",
    )
    require(
        containers["foreignAdvertifiedProjectCount"] == 0,
        failures,
        "NON_OS2_ADVERTIFIED_PROJECT_PRESENT",
    )
    require(
        containers["requiredHealthyServiceCount"] == 9,
        failures,
        "OS2_REQUIRED_SERVICES_NOT_HEALTHY",
    )
    require(all(health.values()), failures, "LOCAL_SERVICE_HEALTH_FAILED")

    report = {
        "schemaVersion": "advertified.inventory-production-completion.v1",
        "generatedAtUtc": datetime.now(UTC).isoformat(),
        "verdict": "PASS" if not failures else "FAIL",
        "failures": failures,
        "physicalCertification": {
            "sourceCount": physical.get("sourceCount"),
            "passedSourceCount": physical.get("passedSourceCount"),
            "candidateCount": physical.get("candidateCount"),
            "expectedAnchorCount": physical.get("expectedAnchorCount"),
            "matchedAnchorCount": physical.get("matchedAnchorCount"),
        },
        "bedrockCertification": {
            "sourceCount": bedrock.get("sourceCount"),
            "passedSourceCount": bedrock.get("passedSourceCount"),
            "candidateCount": bedrock.get("candidateCount"),
            "changedPhysicalFieldCount": bedrock.get(
                "changedPhysicalFieldCount"
            ),
            "missingClassificationCount": bedrock.get(
                "missingClassificationCount"
            ),
            "missingDescriptionCount": bedrock.get(
                "missingDescriptionCount"
            ),
        },
        "publication": {
            "sourceCount": publication.get("sourceCount"),
            "approvedCandidateCount": publication.get(
                "approvedCandidateCount"
            ),
            "publishedProductCount": publication.get(
                "publishedProductCount"
            ),
            "publishedRateCount": publication.get(
                "publishedRateCount"
            ),
            "publishedAvailabilityCount": publication.get(
                "publishedAvailabilityCount"
            ),
            "lineageSourceCount": publication.get(
                "lineageSourceCount"
            ),
        },
        "cost": {
            "hardCeilingUsdMicros": cost.get(
                "totalBudgetUsdMicros"
            ),
            "confirmedHistoricalUsageUsdMicros": cost.get(
                "confirmedHistoricalUsageUsdMicros"
            ),
            "historicalReserveUsdMicros": cost.get(
                "uncertainHistoricalUsageReserveUsdMicros"
            ),
            "inventoryActualUsageUsdMicros": cost.get(
                "newActualUsageUsdMicros"
            ),
            "inventoryCommittedUsageUsdMicros": cost.get(
                "newCommittedUsageUsdMicros"
            ),
            "canaryActualUsageUsdMicros": canary_cost.get(
                "canaryActualUsdMicros"
            ),
            "canaryCommittedUsageUsdMicros": canary_cost.get(
                "canaryCommittedUsdMicros"
            ),
            "totalAccountedUsdMicros": canary_cost.get(
                "totalAccountedUsdMicros"
            ),
            "remainingUsdMicros": canary_cost.get(
                "remainingUsdMicros"
            ),
        },
        "briefToProposalCanary": {
            "passed": canary_cost.get("passed"),
            "recordedAiCostEntries": len(
                canary_cost.get("changes") or []
            ),
        },
        "validation": validation,
        "docker": containers,
        "health": health,
    }
    output = CORPUS / "completion"
    output.mkdir(parents=True, exist_ok=True)
    json_path = output / "inventory-production-completion.json"
    markdown_path = output / "INVENTORY_PRODUCTION_COMPLETION.md"
    write_json(json_path, report)
    markdown_path.write_text(
        render_markdown(report), encoding="utf-8"
    )
    print(json.dumps(report, indent=2))
    return 0 if report["verdict"] == "PASS" else 2


def docker_state() -> dict[str, Any]:
    process = subprocess.run(
        [
            "docker", "ps", "-a", "--format",
            "{{.Names}}|{{.Status}}|{{.Label \"com.docker.compose.project\"}}|{{.Label \"com.docker.compose.service\"}}",
        ],
        check=True,
        capture_output=True,
        text=True,
        timeout=60,
    )
    rows = []
    for line in process.stdout.splitlines():
        name, status, project, service = (line.split("|", 3) + [""] * 4)[:4]
        if not name.startswith("advertified-"):
            continue
        rows.append({
            "name": name,
            "status": status,
            "project": project,
            "service": service,
        })
    required = {
        "web", "api", "agent-runtime", "docling", "redis",
        "clamav", "mailhog", "postgres", "minio",
    }
    healthy = {
        item["service"]
        for item in rows
        if item["project"] == "advertified-os2-dev"
        and item["service"] in required
        and "healthy" in item["status"].lower()
    }
    return {
        "advertifiedContainerCount": len(rows),
        "foreignAdvertifiedProjectCount": sum(
            item["project"] != "advertified-os2-dev" for item in rows
        ),
        "requiredHealthyServiceCount": len(healthy),
        "containers": rows,
    }


def health_state() -> dict[str, bool]:
    return {
        "web": healthy("http://127.0.0.1:3017/"),
        "api": healthy("http://127.0.0.1:5197/health/ready"),
        "agentRuntime": healthy(
            "http://127.0.0.1:5198/health/ready"
        ),
    }


def healthy(url: str) -> bool:
    try:
        return requests.get(url, timeout=10).status_code == 200
    except requests.RequestException:
        return False


def require(value: bool, failures: list[str], code: str) -> None:
    if not value:
        failures.append(code)


def render_markdown(report: dict[str, Any]) -> str:
    physical = report["physicalCertification"]
    bedrock = report["bedrockCertification"]
    publication = report["publication"]
    cost = report["cost"]
    return "\n".join([
        "# Inventory production completion",
        "",
        f"Verdict: **{report['verdict']}**",
        "",
        "| Stage | Result |",
        "|---|---:|",
        f"| Physical files certified | {physical['passedSourceCount']}/{physical['sourceCount']} |",
        f"| Bedrock files certified | {bedrock['passedSourceCount']}/{bedrock['sourceCount']} |",
        f"| Bedrock physical fact changes | {bedrock['changedPhysicalFieldCount']} |",
        f"| Published source lineages | {publication['lineageSourceCount']}/{publication['sourceCount']} |",
        f"| Published products | {publication['publishedProductCount']} |",
        f"| Published rates | {publication['publishedRateCount']} |",
        f"| Accounted AI spend | ${int(cost['totalAccountedUsdMicros'] or 0) / 1_000_000:.6f} |",
        f"| Remaining from US$5 | ${int(cost['remainingUsdMicros'] or 0) / 1_000_000:.6f} |",
        f"| Brief-to-proposal canary | {'PASS' if report['briefToProposalCanary']['passed'] else 'FAIL'} |",
        f"| Only advertified-os2-dev | {'YES' if report['docker']['foreignAdvertifiedProjectCount'] == 0 else 'NO'} |",
        "",
    ])


def read_json(path: Path) -> dict[str, Any]:
    value = json.loads(
        path.resolve(strict=True).read_text(encoding="utf-8")
    )
    if not isinstance(value, dict):
        raise RuntimeError(f"Expected JSON object in {path}.")
    return value


def write_json(path: Path, value: dict[str, Any]) -> None:
    path.write_text(
        json.dumps(value, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )


if __name__ == "__main__":
    raise SystemExit(main())

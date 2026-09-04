"""Write one authoritative status for the full inventory-to-proposal programme."""

from __future__ import annotations

import json
from datetime import UTC, datetime
from pathlib import Path
from typing import Any

from inventory_corpus_api import InventoryApi

ROOT = Path(__file__).resolve().parents[1]
CORPUS = ROOT / "artifacts" / "inventory-corpus"
CERT = CORPUS / "certification"
TENANT = "10000000-0000-0000-0000-000000000020"
PROGRAMME_LIMIT = 5_000_000
HISTORICAL_RESERVE = 250_000


def main() -> int:
    physical = optional_json(CERT / "physical-corpus-certification.json")
    bedrock_preflight = optional_json(CERT / "bedrock-preflight.json")
    bedrock_progress = optional_json(CERT / "bedrock-corpus-progress.json")
    bedrock_evaluation = optional_json(CERT / "bedrock-corpus-evaluation.json")
    upload = optional_json(CERT / "inventory-upload-verification.json")
    proposal = optional_json(CERT / "brief-to-proposal-verification.json")

    live = live_status()
    committed = int(live.get("bedrockCommittedCostUsdMicros") or 0)
    known_total = HISTORICAL_RESERVE + committed
    stages = [
        stage(
            "PHYSICAL_CERTIFICATION",
            physical.get("passed") is True
            and physical.get("summary", {}).get("passedSourceCount") == 43,
            physical.get("summary") or {},
        ),
        stage(
            "BEDROCK_PREFLIGHT",
            bedrock_preflight.get("passed") is True,
            without_large_arrays(bedrock_preflight),
        ),
        stage(
            "BEDROCK_CORPUS_RUN",
            bedrock_progress.get("status") == "COMPLETED"
            and bedrock_progress.get("completedSourceCount") == 43,
            without_large_arrays(bedrock_progress),
        ),
        stage(
            "BEDROCK_RESPONSE_EVALUATION",
            bedrock_evaluation.get("passed") is True
            and bedrock_evaluation.get("passedSourceCount") == 43,
            without_large_arrays(bedrock_evaluation),
        ),
        stage(
            "INVENTORY_UPLOAD_AND_PUBLICATION",
            upload.get("passed") is True,
            without_large_arrays(upload),
        ),
        stage(
            "BRIEF_TO_PROPOSAL",
            proposal.get("passed") is True,
            without_large_arrays(proposal),
        ),
    ]
    next_stage = next((item["name"] for item in stages if not item["passed"]), None)
    result = {
        "schemaVersion": "advertified.inventory-program-status.v1",
        "generatedAtUtc": datetime.now(UTC).isoformat(),
        "programmeLimitUsdMicros": PROGRAMME_LIMIT,
        "historicalAndUncertainReserveUsdMicros": HISTORICAL_RESERVE,
        "activeCorpusCommittedCostUsdMicros": committed,
        "conservativeKnownTotalUsdMicros": known_total,
        "remainingProgrammeBudgetUsdMicros": max(0, PROGRAMME_LIMIT - known_total),
        "bedrockLiveExecutionEnabled": live.get("bedrockLiveExecutionEnabled"),
        "sourceCount": live.get("sourceCount"),
        "candidateCount": live.get("candidateCount"),
        "publishedCandidateCount": live.get("publishedCandidateCount"),
        "stages": stages,
        "nextIncompleteStage": next_stage,
        "passed": next_stage is None and known_total <= PROGRAMME_LIMIT,
    }
    output = CERT / "INVENTORY_PROGRAM_STATUS.json"
    output.write_text(json.dumps(result, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    (CERT / "INVENTORY_PROGRAM_STATUS.md").write_text(render_markdown(result), encoding="utf-8")
    print(json.dumps(result, indent=2))
    return 0


def live_status() -> dict[str, Any]:
    api = InventoryApi("http://127.0.0.1:5197", "http://localhost:3017", TENANT)
    api.start_session()
    preflight = api.request("GET", api.tenant_path("/inventory-semantic-preflight"))
    sources = preflight.get("sources") or []
    candidate_count = 0
    published_count = 0
    for source in sources:
        view = api.read_complete_import(str(source["importId"]))
        candidates = view.get("candidates") or []
        candidate_count += len(candidates)
        published_count += sum(
            str(item.get("status") or "") == "PUBLISHED"
            for item in candidates
        )
    return {
        "bedrockLiveExecutionEnabled": preflight.get("liveExecutionEnabled"),
        "bedrockCommittedCostUsdMicros": int(
            preflight.get("existingCommittedCostUsdMicros") or 0
        ),
        "sourceCount": len(sources),
        "candidateCount": candidate_count,
        "publishedCandidateCount": published_count,
    }


def stage(name: str, passed: bool, evidence: dict[str, Any]) -> dict[str, Any]:
    return {"name": name, "passed": passed, "evidence": evidence}


def optional_json(path: Path) -> dict[str, Any]:
    if not path.exists():
        return {}
    value = json.loads(path.read_text(encoding="utf-8"))
    return value if isinstance(value, dict) else {}


def without_large_arrays(value: dict[str, Any]) -> dict[str, Any]:
    return {
        key: item for key, item in value.items()
        if key not in {"files", "sources", "blockedSources", "failures"}
    }


def render_markdown(result: dict[str, Any]) -> str:
    lines = [
        "# Inventory production programme status",
        "",
        f"Overall: **{'PASS' if result['passed'] else 'IN PROGRESS'}**",
        "",
        "| Stage | Result |",
        "|---|---|",
    ]
    for item in result["stages"]:
        lines.append(
            f"| {item['name']} | {'PASS' if item['passed'] else 'OUTSTANDING'} |"
        )
    lines.extend([
        "",
        "## Cost",
        "",
        f"- Programme limit: US${result['programmeLimitUsdMicros'] / 1_000_000:.6f}",
        f"- Conservative historical reserve: US${result['historicalAndUncertainReserveUsdMicros'] / 1_000_000:.6f}",
        f"- Active corpus committed cost: US${result['activeCorpusCommittedCostUsdMicros'] / 1_000_000:.6f}",
        f"- Remaining programme budget: US${result['remainingProgrammeBudgetUsdMicros'] / 1_000_000:.6f}",
        "",
        f"Next incomplete stage: `{result['nextIncompleteStage']}`",
        "",
    ])
    return "\n".join(lines)


if __name__ == "__main__":
    raise SystemExit(main())

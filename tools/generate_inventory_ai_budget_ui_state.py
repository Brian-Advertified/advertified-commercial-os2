"""Generate the UI's inventory-AI budget state from audited cost artifacts."""

from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CORPUS = ROOT / "artifacts" / "inventory-corpus"
POLICY = CORPUS / "ai-cost" / "budget-policy.json"
INVENTORY = CORPUS / "ai-cost" / "inventory-ai-cost-report.json"
CANARY = CORPUS / "ai-cost" / "brief-proposal-canary-cost.json"
OUTPUT = (
    ROOT / "web" / "src" / "generated"
    / "inventory-ai-budget-state.ts"
)


def main() -> int:
    policy = read(POLICY)
    inventory = read(INVENTORY, optional=True)
    canary = read(CANARY, optional=True)
    state = {
        "totalUsdMicros": int(policy["totalBudgetUsdMicros"]),
        "confirmedHistoricalUsdMicros": int(
            policy["confirmedHistoricalUsageUsdMicros"]
        ),
        "uncertainReserveUsdMicros": int(
            policy["uncertainHistoricalUsageReserveUsdMicros"]
        ),
        "inventoryActualUsdMicros": int(
            inventory.get("newActualUsageUsdMicros") or 0
        ),
        "inventoryCommittedUsdMicros": int(
            inventory.get("newCommittedUsageUsdMicros") or 0
        ),
        "canaryActualUsdMicros": int(
            canary.get("canaryActualUsdMicros") or 0
        ),
        "canaryCommittedUsdMicros": int(
            canary.get("canaryCommittedUsdMicros") or 0
        ),
        "purposes": purpose_rows(policy, inventory, canary),
    }
    accounted = (
        state["confirmedHistoricalUsdMicros"]
        + state["uncertainReserveUsdMicros"]
        + state["inventoryCommittedUsdMicros"]
        + state["canaryCommittedUsdMicros"]
    )
    state["accountedUsedUsdMicros"] = min(
        state["totalUsdMicros"], accounted
    )
    state["remainingUsdMicros"] = max(
        0, state["totalUsdMicros"] - accounted
    )
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(
        "// Generated from audited inventory AI cost artifacts.\n"
        "// Do not edit manually.\n"
        "export const inventoryAiBudgetState = "
        + json.dumps(state, indent=2)
        + " as const\n",
        encoding="utf-8",
    )
    print(json.dumps(state, indent=2))
    return 0


def purpose_rows(policy: dict, inventory: dict, canary: dict) -> list[dict]:
    return [
        {
            "code": "HISTORICAL_CERTIFICATION_USAGE",
            "label": "Earlier extraction certification",
            "amountUsdMicros": int(
                policy["confirmedHistoricalUsageUsdMicros"]
            ),
            "kind": "actual",
            "explanation": (
                "Confirmed provider usage retained from the workbook and "
                "earlier extraction certification."
            ),
        },
        {
            "code": "FAILED_CALL_USAGE_RESERVE",
            "label": "Conservative failed-call reserve",
            "amountUsdMicros": int(
                policy["uncertainHistoricalUsageReserveUsdMicros"]
            ),
            "kind": "reserve",
            "explanation": (
                "Reserved because failed calls did not retain enough final "
                "usage evidence to prove a lower amount."
            ),
        },
        {
            "code": "CORPUS_SEMANTIC_CLASSIFICATION",
            "label": "Corpus classification and descriptions",
            "amountUsdMicros": int(
                inventory.get("newActualUsageUsdMicros") or 0
            ),
            "committedUsdMicros": int(
                inventory.get("newCommittedUsageUsdMicros") or 0
            ),
            "kind": "actual",
            "explanation": (
                "Bedrock classification and grounded searchable descriptions "
                "for physically certified inventory only."
            ),
        },
        {
            "code": "BRIEF_TO_PROPOSAL_PRODUCTION_CANARY",
            "label": "Live brief-to-proposal canary",
            "amountUsdMicros": int(
                canary.get("canaryActualUsdMicros") or 0
            ),
            "committedUsdMicros": int(
                canary.get("canaryCommittedUsdMicros") or 0
            ),
            "kind": "actual",
            "explanation": (
                "One governed live brief, planning and proposal verification "
                "after corpus publication."
            ),
        },
    ]


def read(path: Path, *, optional: bool = False) -> dict:
    if optional and not path.exists():
        return {}
    value = json.loads(path.resolve(strict=True).read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise RuntimeError(f"Expected JSON object in {path}.")
    return value


if __name__ == "__main__":
    raise SystemExit(main())

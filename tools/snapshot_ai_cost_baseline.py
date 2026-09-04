"""Snapshot all current AI/agent cost rows before a governed canary."""

from __future__ import annotations

import json
from datetime import UTC, datetime
from pathlib import Path

from ai_cost_catalog import baseline_payload, read_all_ai_cost_rows
from inventory_ai_cost_ledger import REPO_ROOT

OUTPUT = (
    REPO_ROOT / "artifacts" / "inventory-corpus" / "ai-cost"
    / "brief-proposal-canary-baseline.json"
)


def main() -> int:
    payload = baseline_payload(read_all_ai_cost_rows())
    payload["capturedAtUtc"] = datetime.now(UTC).isoformat()
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(
        json.dumps(payload, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    print(json.dumps({
        "capturedAtUtc": payload["capturedAtUtc"],
        "rowCount": payload["rowCount"],
        "path": str(OUTPUT.relative_to(REPO_ROOT)),
    }, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

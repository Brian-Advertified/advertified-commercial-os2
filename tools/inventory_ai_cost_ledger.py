"""Read and validate the persisted inventory-AI cost ledger from OS2 PostgreSQL."""

from __future__ import annotations

import json
import shlex
import subprocess
from dataclasses import dataclass
from pathlib import Path
from typing import Any

REPO_ROOT = Path(__file__).resolve().parents[1]
POLICY_PATH = (
    REPO_ROOT / "artifacts" / "inventory-corpus" / "ai-cost"
    / "budget-policy.json"
)
POSTGRES_CONTAINER = "advertified-os2-dev-postgres-1"


@dataclass(frozen=True)
class LedgerEntry:
    entry_id: str
    source_hash: str | None
    import_id: str | None
    status: str | None
    model_id: str | None
    budget_scope: str | None
    purpose: str
    actual_cost_usd_micros: int
    committed_cost_usd_micros: int
    request: Any
    response: Any
    raw: dict[str, Any]


def load_policy(path: Path = POLICY_PATH) -> dict[str, Any]:
    value = json.loads(path.resolve(strict=True).read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise RuntimeError("Inventory AI budget policy must be a JSON object.")
    return value


def read_inventory_semantic_runs() -> tuple[LedgerEntry, ...]:
    rows = query_json("""
        SELECT COALESCE(jsonb_agg(to_jsonb(run)), '[]'::jsonb)
        FROM commercial.inventory_semantic_runs AS run
        """)
    if not isinstance(rows, list):
        raise RuntimeError("Inventory semantic run ledger did not return a list.")
    return tuple(entry(row) for row in rows if isinstance(row, dict))


def query_json(sql: str) -> Any:
    command = (
        "psql -v ON_ERROR_STOP=1 -U \"$POSTGRES_USER\" "
        "-d \"$POSTGRES_DB\" -At -c " + shlex.quote(sql.strip())
    )
    process = subprocess.run(
        ["docker", "exec", POSTGRES_CONTAINER, "sh", "-lc", command],
        check=False,
        capture_output=True,
        text=True,
        timeout=120,
    )
    if process.returncode != 0:
        raise RuntimeError(
            "Unable to read the OS2 inventory AI ledger: "
            + process.stderr.strip()
        )
    raw = process.stdout.strip()
    return json.loads(raw or "null")


def entry(row: dict[str, Any]) -> LedgerEntry:
    normalized = {normalize_key(key): value for key, value in row.items()}
    request = first_value(
        normalized,
        "requestjson", "requestpayload", "inputjson", "packetjson",
    )
    response = first_value(
        normalized,
        "responsejson", "providerresponsejson", "outputjson",
        "resultjson",
    )
    return LedgerEntry(
        entry_id=str(first_value(normalized, "id", "runid") or ""),
        source_hash=text(first_value(
            normalized, "sourcehash", "documentsourcehash"
        )),
        import_id=text(first_value(normalized, "importid")),
        status=text(first_value(normalized, "statuscode", "status")),
        model_id=text(first_value(normalized, "modelid", "providermodel")),
        budget_scope=text(first_value(
            normalized, "budgetscope", "budgetscopeid", "scope"
        )),
        purpose=purpose_for(normalized),
        actual_cost_usd_micros=integer(first_value(
            normalized,
            "actualcostusdmicros",
            "costusdmicros",
            "actualcostmicros",
        )),
        committed_cost_usd_micros=integer(first_value(
            normalized,
            "committedcostusdmicros",
            "maximumcostusdmicros",
            "reservedcostusdmicros",
        )),
        request=parse_json_value(request),
        response=parse_json_value(response),
        raw=row,
    )


def validate_budget(
    entries: tuple[LedgerEntry, ...],
    policy: dict[str, Any],
) -> dict[str, Any]:
    active_scope = str(policy.get("activeBudgetScope") or "")
    scoped_entries = tuple(
        item for item in entries
        if not active_scope or item.budget_scope == active_scope
    )
    confirmed_historical = int(
        policy["confirmedHistoricalUsageUsdMicros"]
    )
    uncertain_reserve = int(
        policy["uncertainHistoricalUsageReserveUsdMicros"]
    )
    total_budget = int(policy["totalBudgetUsdMicros"])
    per_call = int(policy["perCallMaximumUsdMicros"])
    models = set(policy.get("modelAllowlist") or [])
    actual_new = sum(
        item.actual_cost_usd_micros for item in scoped_entries
    )
    committed_new = sum(
        max(item.actual_cost_usd_micros, item.committed_cost_usd_micros)
        for item in scoped_entries
    )
    failures: list[str] = []
    for item in scoped_entries:
        if item.actual_cost_usd_micros > per_call:
            failures.append(f"PER_CALL_ACTUAL_CAP_EXCEEDED:{item.entry_id}")
        if item.committed_cost_usd_micros > per_call:
            failures.append(f"PER_CALL_COMMITTED_CAP_EXCEEDED:{item.entry_id}")
        if item.model_id and models and item.model_id not in models:
            failures.append(f"MODEL_NOT_ALLOWLISTED:{item.entry_id}")
        if not item.purpose:
            failures.append(f"PURPOSE_MISSING:{item.entry_id}")
    actual_total = confirmed_historical + actual_new
    budget_accounted_total = (
        confirmed_historical + uncertain_reserve + committed_new
    )
    if budget_accounted_total > total_budget:
        failures.append("TOTAL_BUDGET_EXCEEDED")
    return {
        "totalBudgetUsdMicros": total_budget,
        "confirmedHistoricalUsageUsdMicros": confirmed_historical,
        "uncertainHistoricalUsageReserveUsdMicros": uncertain_reserve,
        "newActualUsageUsdMicros": actual_new,
        "newCommittedUsageUsdMicros": committed_new,
        "actualKnownTotalUsdMicros": actual_total,
        "budgetAccountedTotalUsdMicros": budget_accounted_total,
        "remainingBudgetUsdMicros": max(
            0, total_budget - budget_accounted_total
        ),
        "activeBudgetScope": active_scope,
        "entryCount": len(scoped_entries),
        "allLedgerEntryCount": len(entries),
        "failures": failures,
        "passed": not failures,
        "entries": [entry_view(item) for item in scoped_entries],
    }


def entry_view(item: LedgerEntry) -> dict[str, Any]:
    return {
        "id": item.entry_id,
        "sourceHash": item.source_hash,
        "importId": item.import_id,
        "status": item.status,
        "modelId": item.model_id,
        "budgetScope": item.budget_scope,
        "purpose": item.purpose,
        "actualCostUsdMicros": item.actual_cost_usd_micros,
        "committedCostUsdMicros": item.committed_cost_usd_micros,
    }


def purpose_for(values: dict[str, Any]) -> str:
    explicit = first_value(
        values, "purpose", "operationcode", "agentcode", "toolcode",
    )
    if explicit:
        return str(explicit)
    request = parse_json_value(first_value(
        values, "requestjson", "requestpayload", "inputjson"
    ))
    if isinstance(request, dict):
        for key in ("purpose", "operation", "agent", "tool"):
            if request.get(key):
                return str(request[key])
    return "INVENTORY_SEMANTIC_CLASSIFICATION"


def normalize_key(value: str) -> str:
    return "".join(character.lower() for character in value if character.isalnum())


def first_value(values: dict[str, Any], *keys: str) -> Any:
    for key in keys:
        if key in values and values[key] is not None:
            return values[key]
    return None


def parse_json_value(value: Any) -> Any:
    if not isinstance(value, str):
        return value
    try:
        return json.loads(value)
    except json.JSONDecodeError:
        return value


def integer(value: Any) -> int:
    try:
        return int(value or 0)
    except (TypeError, ValueError):
        return 0


def text(value: Any) -> str | None:
    return str(value) if value is not None and str(value).strip() else None

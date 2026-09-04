"""Discover and account for AI/agent cost rows in the OS2 commercial schema."""

from __future__ import annotations

import hashlib
import json
from dataclasses import dataclass, asdict
from typing import Any

from inventory_ai_cost_ledger import query_json

AI_TABLE_TOKENS = (
    "agent", "semantic", "inference", "ai_run", "model_run",
    "runtime", "execution",
)
ACTUAL_COST_KEYS = (
    "actualcostusdmicros", "costusdmicros", "actualcostmicros",
    "actualcostminor", "costminor",
)
COMMITTED_COST_KEYS = (
    "committedcostusdmicros", "maximumcostusdmicros",
    "reservedcostusdmicros", "costcapusdmicros", "costcapminor",
)


@dataclass(frozen=True)
class AiCostRow:
    key: str
    table: str
    row_id: str
    created_at: str | None
    purpose: str
    model_id: str | None
    actual_usd_micros: int
    committed_usd_micros: int
    row_hash: str


def read_all_ai_cost_rows() -> tuple[AiCostRow, ...]:
    catalog = query_json("""
        SELECT COALESCE(jsonb_agg(to_jsonb(catalog_row)), '[]'::jsonb)
        FROM (
            SELECT table_name,
                   jsonb_agg(column_name ORDER BY ordinal_position) AS columns
            FROM information_schema.columns
            WHERE table_schema = 'commercial'
            GROUP BY table_name
            HAVING bool_or(lower(column_name) LIKE '%cost%')
            ORDER BY table_name
        ) AS catalog_row
        """)
    result: list[AiCostRow] = []
    for item in catalog if isinstance(catalog, list) else []:
        table = str(item.get("table_name") or "")
        normalized_table = normalize(table)
        if not any(token.replace("_", "") in normalized_table for token in AI_TABLE_TOKENS):
            continue
        columns = [str(value) for value in item.get("columns") or []]
        normalized_columns = {normalize(value): value for value in columns}
        actual_column = first_column(normalized_columns, ACTUAL_COST_KEYS)
        committed_column = first_column(normalized_columns, COMMITTED_COST_KEYS)
        if not actual_column and not committed_column:
            continue
        rows = query_json(f"""
            SELECT COALESCE(jsonb_agg(to_jsonb(source_row)), '[]'::jsonb)
            FROM commercial.{quote_identifier(table)} AS source_row
            """)
        for row in rows if isinstance(rows, list) else []:
            if not isinstance(row, dict):
                continue
            result.append(normalize_row(
                table,
                row,
                actual_column,
                committed_column,
            ))
    return tuple(sorted(result, key=lambda value: value.key))


def baseline_payload(rows: tuple[AiCostRow, ...]) -> dict[str, Any]:
    return {
        "schemaVersion": "advertified.ai-cost-baseline.v1",
        "rowCount": len(rows),
        "rows": [asdict(item) for item in rows],
    }


def delta(
    before: tuple[AiCostRow, ...],
    after: tuple[AiCostRow, ...],
) -> dict[str, Any]:
    previous = {item.key: item for item in before}
    changes = []
    actual = 0
    committed = 0
    for item in after:
        prior = previous.get(item.key)
        actual_delta = max(
            0,
            item.actual_usd_micros - (
                prior.actual_usd_micros if prior else 0
            ),
        )
        committed_delta = max(
            actual_delta,
            item.committed_usd_micros - (
                prior.committed_usd_micros if prior else 0
            ),
        )
        if not actual_delta and not committed_delta and prior:
            continue
        actual += actual_delta
        committed += committed_delta
        changes.append({
            **asdict(item),
            "actualDeltaUsdMicros": actual_delta,
            "committedDeltaUsdMicros": committed_delta,
            "newRow": prior is None,
        })
    return {
        "actualDeltaUsdMicros": actual,
        "committedDeltaUsdMicros": committed,
        "changeCount": len(changes),
        "changes": changes,
    }


def rows_from_payload(payload: dict[str, Any]) -> tuple[AiCostRow, ...]:
    return tuple(AiCostRow(**item) for item in payload.get("rows") or [])


def normalize_row(
    table: str,
    row: dict[str, Any],
    actual_column: str | None,
    committed_column: str | None,
) -> AiCostRow:
    normalized = {normalize(key): value for key, value in row.items()}
    row_id = str(
        first(normalized, "id", "runid", "executionid", "correlationid")
        or hashlib.sha256(
            json.dumps(row, sort_keys=True, default=str).encode("utf-8")
        ).hexdigest()
    )
    actual = cost_micros(
        row.get(actual_column) if actual_column else None,
        actual_column,
    )
    committed = cost_micros(
        row.get(committed_column) if committed_column else None,
        committed_column,
    )
    encoded = json.dumps(row, sort_keys=True, default=str)
    return AiCostRow(
        key=f"{table}:{row_id}",
        table=table,
        row_id=row_id,
        created_at=text(first(
            normalized,
            "createdatutc", "startedatutc", "recordedatutc", "createdat",
        )),
        purpose=str(first(
            normalized,
            "purpose", "agentcode", "operationcode", "toolcode",
        ) or table),
        model_id=text(first(
            normalized,
            "modelid", "providermodel", "model",
        )),
        actual_usd_micros=actual,
        committed_usd_micros=max(actual, committed),
        row_hash=hashlib.sha256(encoded.encode("utf-8")).hexdigest(),
    )


def first_column(
    columns: dict[str, str],
    candidates: tuple[str, ...],
) -> str | None:
    for candidate in candidates:
        if candidate in columns:
            return columns[candidate]
    return None


def cost_micros(value: Any, column: str | None) -> int:
    try:
        amount = int(value or 0)
    except (TypeError, ValueError):
        return 0
    normalized = normalize(column or "")
    if normalized.endswith("minor") and "usd" not in normalized:
        return amount * 10_000
    return amount


def first(values: dict[str, Any], *keys: str) -> Any:
    for key in keys:
        if values.get(key) is not None:
            return values[key]
    return None


def normalize(value: str) -> str:
    return "".join(character.lower() for character in value if character.isalnum())


def quote_identifier(value: str) -> str:
    if not value or any(
        not (character.isalnum() or character == "_") for character in value
    ):
        raise RuntimeError(f"Unsafe database identifier: {value!r}")
    return '"' + value.replace('"', '""') + '"'


def text(value: Any) -> str | None:
    return str(value) if value is not None and str(value).strip() else None

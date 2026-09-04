"""Check whether canonical inventory supports products with pending supplier rates."""

from __future__ import annotations

import json
from pathlib import Path

from inventory_ai_cost_ledger import REPO_ROOT, query_json

OUTPUT = (
    REPO_ROOT / "artifacts" / "inventory-corpus" / "publication"
    / "inventory-rate-schema.json"
)


def main() -> int:
    rows = query_json("""
        SELECT COALESCE(jsonb_agg(to_jsonb(columns_view)), '[]'::jsonb)
        FROM (
            SELECT table_schema, table_name, column_name, is_nullable,
                   data_type, udt_name
            FROM information_schema.columns
            WHERE table_schema = 'commercial'
              AND table_name LIKE '%inventory%rate%'
            ORDER BY table_name, ordinal_position
        ) AS columns_view
        """)
    if not isinstance(rows, list) or not rows:
        raise RuntimeError("Canonical inventory rate columns were not found.")
    critical = [
        row for row in rows
        if any(token in str(row.get("column_name", "")).lower()
               for token in ("rate_type", "currency", "amount"))
    ]
    nullable = [
        row for row in critical
        if str(row.get("is_nullable", "")).upper() == "YES"
    ]
    report = {
        "schemaVersion": "advertified.inventory-rate-schema-check.v1",
        "criticalColumnCount": len(critical),
        "nullableCriticalColumnCount": len(nullable),
        "supportsPendingSupplierRate": (
            bool(critical) and len(nullable) == len(critical)
        ),
        "columns": rows,
    }
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(
        json.dumps(report, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    print(json.dumps(report, indent=2))
    return 0 if report["supportsPendingSupplierRate"] else 2


if __name__ == "__main__":
    raise SystemExit(main())

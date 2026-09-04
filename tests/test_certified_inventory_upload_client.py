"""Offline contract-building tests for certified inventory upload."""

from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TOOLS = str(ROOT / "tools")
if TOOLS not in sys.path:
    sys.path.insert(0, TOOLS)

from inventory_certified_upload_client import (  # noqa: E402
    build_from_schema,
    build_multipart,
    deterministic_uuid,
)


def test_command_body_uses_expected_version_and_idempotency() -> None:
    schema = {
        "type": "object",
        "required": ["expectedVersion", "idempotencyKey"],
        "properties": {
            "expectedVersion": {"type": "integer"},
            "idempotencyKey": {"type": "string", "format": "uuid"},
        },
    }
    body = build_from_schema(schema, {
        "expectedVersion": 7,
        "idempotencyKey": "00000000-0000-0000-0000-000000000007",
    })
    assert body == {
        "expectedVersion": 7,
        "idempotencyKey": "00000000-0000-0000-0000-000000000007",
    }


def test_multipart_contract_uses_real_workbook(tmp_path: Path) -> None:
    workbook = tmp_path / "certified.xlsx"
    workbook.write_bytes(b"xlsx")
    schema = {
        "type": "object",
        "required": ["file", "supplierName", "idempotencyKey"],
        "properties": {
            "file": {"type": "string", "format": "binary"},
            "supplierName": {"type": "string"},
            "idempotencyKey": {"type": "string", "format": "uuid"},
        },
    }
    files, data = build_multipart(schema, {
        "supplierName": "DStv Media Sales",
        "idempotencyKey": "00000000-0000-0000-0000-000000000001",
    }, workbook)
    assert files["file"][0] == "certified.xlsx"
    assert files["file"][1] == b"xlsx"
    assert data["supplierName"] == "DStv Media Sales"


def test_idempotency_key_is_stable() -> None:
    assert deterministic_uuid("same") == deterministic_uuid("same")
    assert deterministic_uuid("same") != deterministic_uuid("different")

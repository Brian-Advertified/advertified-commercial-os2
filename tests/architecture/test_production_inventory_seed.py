"""Production inventory seed generation guardrails."""
from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path

import pytest


REPO_ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO_ROOT / "tools"))

import generate_inventory_production_seed as production_seed  # noqa: E402
import normalize_social_inventory as social_inventory  # noqa: E402


TENANT_ID = "81000000-0000-0000-0000-000000000001"
CREATOR_ID = "81000000-0000-0000-0000-000000000002"
REVIEWER_ID = "81000000-0000-0000-0000-000000000003"


def test_canonical_social_inventory_uses_governed_social_channel() -> None:
    seed = json.loads(social_inventory.DEFAULT_SEED.read_text(encoding="utf-8"))

    placements, changed = social_inventory.normalize(seed)

    assert placements > 0
    assert changed == 0


def test_seed_requires_exact_manual_review_checksum() -> None:
    seed = {"seedVersion": "test", "records": []}
    expected = hashlib.sha256(
        json.dumps(
            seed,
            ensure_ascii=False,
            sort_keys=True,
            separators=(",", ":"),
        ).encode("utf-8")
    ).hexdigest()

    assert production_seed.confirm_seed(seed, expected) == expected
    with pytest.raises(ValueError, match="checksum"):
        production_seed.confirm_seed(seed, "0" * 64)


def test_marketplace_projection_is_rebound_to_production_identity() -> None:
    sql = production_seed.marketplace_sql(TENANT_ID, CREATOR_ID)

    assert TENANT_ID in sql
    assert CREATOR_ID in sql
    assert production_seed.LOCAL_TENANT_ID not in sql
    assert production_seed.LOCAL_ACTOR_ID not in sql
    assert "status_code = 'PUBLISHED'" in sql
    assert "availability.valid_until_utc >= clock_timestamp()" in sql
    assert "ORDER BY availability.observed_at_utc DESC NULLS LAST" in sql


def test_inventory_and_marketplace_publish_in_one_transaction() -> None:
    combined = production_seed.combine_seed_sql(
        "\\set ON_ERROR_STOP on\nBEGIN;\nSELECT 1;\nCOMMIT;\n",
        "\\set ON_ERROR_STOP on\nBEGIN;\nSELECT 2;\nCOMMIT;\n",
    )

    assert combined.count("BEGIN;") == 1
    assert combined.count("COMMIT;") == 1
    assert combined.index("SELECT 1;") < combined.index("SELECT 2;")


def test_rollback_is_non_destructive_and_history_preserving() -> None:
    payload = {
        "records": [
            {
                "productId": "81000000-0000-0000-0000-000000000004",
                "publicationEligible": True,
            }
        ]
    }

    sql = production_seed.rollback_sql(
        payload,
        TENANT_ID,
        CREATOR_ID,
        "a" * 64,
        {"archived": "ARCHIVED", "inactive": "INACTIVE"},
    )

    assert "status_code = 'ARCHIVED'" in sql
    assert "status_code = 'INACTIVE'" in sql
    assert "DELETE FROM" not in sql
    assert "COMMIT;" in sql

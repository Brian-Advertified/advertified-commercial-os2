"""Regression tests for the inventory production-release boundary."""

from __future__ import annotations

import importlib.util
import sys
from dataclasses import replace
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]


def load_module():
    tools = str(REPO_ROOT / "tools")
    if tools not in sys.path:
        sys.path.insert(0, tools)
    path = REPO_ROOT / "tools" / "generate_inventory_production_release_register.py"
    spec = importlib.util.spec_from_file_location(
        "generate_inventory_production_release_register", path
    )
    assert spec and spec.loader
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def record(module, index: int, *, certified: bool = False):
    return module.FileReleaseRecord(
        source_hash=f"{index:064x}",
        file_name=f"source-{index}.pdf",
        document_class="PDF",
        import_id=f"00000000-0000-0000-0000-{index:012d}",
        import_status="REVIEW_REQUIRED",
        import_failure_code=None,
        latest_attempt_number=1,
        latest_attempt_status="COMPLETED",
        candidate_count=1,
        candidates_with_no_core_fields=0 if certified else 1,
        candidates_meeting_minimum=1 if certified else 0,
        blocking_candidate_count=1,
        approved_candidate_count=0,
        published_candidate_count=0,
        file_gold_present=certified,
        file_gold_passed=certified,
        extraction_certification=(
            "CERTIFIED_PHYSICAL_SOURCE_MATCH"
            if certified else "UNCERTIFIED_QUARANTINED"
        ),
        publication_disposition=(
            "HUMAN_REVIEW_REQUIRED"
            if certified else "PUBLICATION_PROHIBITED"
        ),
        reasons=() if certified else ("FILE_LEVEL_GOLD_MISSING",),
    )


def register(module, records, *, live: bool = False):
    return module.build_register(
        {
            "status": "ready",
            "checks": ["process", "deterministic-zero-cost"],
            "deterministicZeroCost": True,
        },
        {
            "liveExecutionEnabled": live,
            "existingCommittedCostUsdMicros": 0,
        },
        {
            "documentCount": 43,
            "datasetVersion": "fixture",
        },
        records,
    )


def test_software_can_launch_with_uncertified_corpus_quarantined() -> None:
    module = load_module()
    records = [record(module, 1, certified=True)] + [
        record(module, index) for index in range(2, 44)
    ]

    result = register(module, records)

    assert result["softwareLaunchGate"] == "GO"
    assert result["corpusPublicationGate"] == "NO_GO"
    assert result["summary"]["certifiedSourceCount"] == 1
    assert result["summary"]["quarantinedSourceCount"] == 42
    assert result["launchBoundary"][
        "uncertifiedCorpusMustRemainQuarantined"
    ] is True


def test_uncertified_publication_blocks_software_launch() -> None:
    module = load_module()
    records = [record(module, 1, certified=True)] + [
        record(module, index) for index in range(2, 44)
    ]
    records[1] = replace(
        records[1],
        published_candidate_count=1,
    )

    result = register(module, records)

    assert result["softwareLaunchGate"] == "NO_GO"
    assert "UNCERTIFIED_INVENTORY_WAS_PUBLISHED" in result[
        "softwareLaunchBlockers"
    ]


def test_live_bedrock_blocks_zero_cost_release() -> None:
    module = load_module()
    records = [record(module, index) for index in range(1, 44)]

    result = register(module, records, live=True)

    assert result["softwareLaunchGate"] == "NO_GO"
    assert "LIVE_BEDROCK_ENABLED" in result["softwareLaunchBlockers"]


def test_minimum_accepts_explicitly_quarantined_ambiguous_rate() -> None:
    module = load_module()
    values = {
        "name": "DStv Stream VOD",
        "channel": "DIGITAL",
        "productType": "DIGITAL_PLACEMENT",
        "currency": "ZAR",
        "rateAmountMinor": None,
        "extension": {
            "rateambiguity": "AMBIGUOUS_TRUNCATED_RATE",
        },
    }

    assert module.meets_minimum(values)


def test_minimum_rejects_unknown_rate_without_ambiguity() -> None:
    module = load_module()
    values = {
        "name": "Unpriced placement",
        "channel": "DIGITAL",
        "productType": "DIGITAL_PLACEMENT",
        "currency": "ZAR",
        "rateAmountMinor": None,
        "extension": {},
    }

    assert not module.meets_minimum(values)

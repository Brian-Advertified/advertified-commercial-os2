"""Production-certification pack guardrails."""

from __future__ import annotations

import ast
import hashlib
import json
import sys
from copy import deepcopy
from pathlib import Path
from typing import Any

import pytest

REPO_ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO_ROOT / "tools"))

import production_certification_contract as contract  # noqa: E402
import validate_production_certification as certification  # noqa: E402


TIMESTAMP = "2026-09-07T10:00:00Z"
FINAL_TIMESTAMP = "2026-09-07T11:00:00Z"


def evidence(path: Path, root: Path) -> dict[str, str]:
    return {
        "path": path.relative_to(root).as_posix(),
        "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
    }


def write_fixture(root: Path) -> tuple[Path, dict[str, Any]]:
    audit = root / "audit.json"
    audit.write_text('{"retained":true}\n', encoding="utf-8")
    audit_ref = evidence(audit, root)
    scenarios = []
    for journey, count in contract.JOURNEY_COUNTS.items():
        for index in range(count):
            pdf = root / "pdfs" / f"{journey.lower()}-{index + 1}.pdf"
            pdf.parent.mkdir(exist_ok=True)
            pdf.write_bytes(
                b"%PDF-1.4\n% Advertified retained proposal "
                + f"{journey}-{index}".encode("utf-8")
            )
            scenarios.append(
                scenario(journey, index, evidence(pdf, root), audit_ref)
            )
    manifest = {
        "schemaVersion": contract.SCHEMA_VERSION,
        "release": {
            "commitSha": "a" * 40,
            "buildId": "release-2026-09-07.1",
            "environment": "production",
            "containerDigests": {
                "api": "sha256:" + "b" * 64,
                "worker": "sha256:" + "c" * 64,
                "agent-runtime": "sha256:" + "d" * 64,
                "web": "sha256:" + "e" * 64,
            },
            "schemaMigrationRange": "0001..0042",
            "masterDataVersion": "2.35.0",
            "policyVersion": "2026-09-07",
            "knownLimitations": [],
            "rollbackPlan": audit_ref,
            "incidentPlan": audit_ref,
        },
        "scenarios": scenarios,
        "certificationChecks": named_records(
            "checkId", contract.CERTIFICATION_CHECKS, audit_ref
        ),
        "roleSignoffs": named_records(
            "roleCode",
            certification.active_human_roles(contract.DEFAULT_MASTER_DATA),
            audit_ref,
        ),
        "governanceSignoffs": named_records(
            "area", contract.GOVERNANCE_SIGNOFFS, audit_ref
        ),
        "finalGo": {
            "decision": "GO",
            "approvedBy": "release-authority",
            "approvedAtUtc": FINAL_TIMESTAMP,
            "evidence": audit_ref,
        },
    }
    manifest_path = root / "production-certification.json"
    manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    return manifest_path, manifest


def scenario(
    journey: str,
    index: int,
    pdf_ref: dict[str, str],
    audit_ref: dict[str, str],
) -> dict[str, Any]:
    client = (
        "Rayetsa Furniture"
        if journey == "UNBRIEFED_OPPORTUNITY" and index == 0
        else f"Acceptance Client {journey} {index + 1}"
    )
    return {
        "scenarioId": f"{journey.lower()}-{index + 1}",
        "journey": journey,
        "difficulty": ("standard", "advanced", "edge-case")[index % 3],
        "clientName": client,
        "accepted": True,
        "completedAtUtc": TIMESTAMP,
        "proposalPdf": pdf_ref,
        "commercialApproval": {
            "decision": "APPROVED",
            "approverId": "Brian",
            "approvedAtUtc": TIMESTAMP,
            "evidence": audit_ref,
        },
        "visualApproval": {
            "decision": "APPROVED",
            "approverId": "Brian",
            "approvedAtUtc": TIMESTAMP,
            "evidence": audit_ref,
        },
        "lifecycleEvidence": {
            check_id: audit_ref
            for check_id in contract.LIFECYCLE_CHECKS[journey]
        },
    }


def named_records(
    key: str,
    names: Any,
    audit_ref: dict[str, str],
) -> list[dict[str, Any]]:
    return [
        {
            key: name,
            "status": "PASS",
            "recordedBy": "certification-owner",
            "recordedAtUtc": TIMESTAMP,
            "evidence": audit_ref,
        }
        for name in sorted(names)
    ]


def write_manifest(root: Path, manifest: dict[str, Any]) -> Path:
    path = root / "production-certification.json"
    path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    return path


def test_complete_retained_pack_is_go(tmp_path: Path) -> None:
    manifest_path, _ = write_fixture(tmp_path)

    result = certification.validate_manifest(manifest_path, tmp_path)

    assert result == {
        "verdict": "GO",
        "scenarioCount": 30,
        "proposalPdfCount": 30,
        "certificationCheckCount": len(contract.CERTIFICATION_CHECKS),
        "roleSignoffCount": len(
            certification.active_human_roles(contract.DEFAULT_MASTER_DATA)
        ),
        "governanceSignoffCount": len(contract.GOVERNANCE_SIGNOFFS),
    }


def test_missing_scenario_and_rayetsa_are_no_go(tmp_path: Path) -> None:
    _, manifest = write_fixture(tmp_path)
    manifest["scenarios"] = [
        item
        for item in manifest["scenarios"]
        if item["journey"] != "UNBRIEFED_OPPORTUNITY"
    ]

    with pytest.raises(certification.CertificationError) as caught:
        certification.validate_manifest(write_manifest(tmp_path, manifest), tmp_path)

    message = str(caught.value)
    assert "scenarios must contain exactly 30 records" in message
    assert "UNBRIEFED_OPPORTUNITY must contain exactly 10 scenarios" in message
    assert "must include Rayetsa Furniture" in message


def test_duplicate_or_tampered_proposal_is_no_go(tmp_path: Path) -> None:
    _, original = write_fixture(tmp_path)
    duplicate = deepcopy(original)
    duplicate["scenarios"][1]["proposalPdf"] = duplicate["scenarios"][0]["proposalPdf"]

    with pytest.raises(certification.CertificationError, match="must be unique"):
        certification.validate_manifest(
            write_manifest(tmp_path, duplicate),
            tmp_path,
        )

    tampered = deepcopy(original)
    proposal = tmp_path / tampered["scenarios"][0]["proposalPdf"]["path"]
    proposal.write_bytes(proposal.read_bytes() + b"tampered")
    with pytest.raises(certification.CertificationError, match="checksum"):
        certification.validate_manifest(
            write_manifest(tmp_path, tampered),
            tmp_path,
        )


def test_missing_external_gate_is_no_go(tmp_path: Path) -> None:
    _, manifest = write_fixture(tmp_path)
    manifest["certificationChecks"] = [
        item
        for item in manifest["certificationChecks"]
        if item["checkId"] != "backup_restore"
    ]

    with pytest.raises(certification.CertificationError, match="backup_restore"):
        certification.validate_manifest(write_manifest(tmp_path, manifest), tmp_path)


def test_final_go_must_follow_every_approval(tmp_path: Path) -> None:
    _, manifest = write_fixture(tmp_path)
    manifest["finalGo"]["approvedAtUtc"] = "2026-09-07T09:00:00Z"

    with pytest.raises(certification.CertificationError, match="after every"):
        certification.validate_manifest(write_manifest(tmp_path, manifest), tmp_path)


def test_every_active_human_role_must_sign_off(tmp_path: Path) -> None:
    _, manifest = write_fixture(tmp_path)
    missing_role = manifest["roleSignoffs"].pop()["roleCode"]

    with pytest.raises(certification.CertificationError, match=missing_role):
        certification.validate_manifest(write_manifest(tmp_path, manifest), tmp_path)


def test_certification_validator_stays_reviewable() -> None:
    source_path = REPO_ROOT / "tools" / "validate_production_certification.py"
    source = source_path.read_text(encoding="utf-8")
    tree = ast.parse(source)
    oversized = {
        node.name: node.end_lineno - node.lineno + 1
        for node in ast.walk(tree)
        if isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef))
        and node.end_lineno is not None
        and node.end_lineno - node.lineno + 1 > 60
    }

    assert len(source.splitlines()) <= 400
    assert not oversized

"""Fail-closed tests for the retained production release evidence gate."""
from __future__ import annotations

import copy
import pytest
import importlib.util
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
MODULE_PATH = ROOT / "tools" / "validate_production_release_evidence.py"
SPEC = importlib.util.spec_from_file_location("release_evidence", MODULE_PATH)
assert SPEC and SPEC.loader
release_evidence = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(release_evidence)


def valid_evidence():
    digest = "@sha256:" + "a" * 64
    approvals = [
        {"role": role, "approver": f"{role} Owner", "approvedAtUtc": "2026-09-12T10:00:00Z",
         "evidence": f"release/{role.lower()}.json"}
        for role in sorted(release_evidence.APPROVAL_ROLES)
    ]
    return {
        "schemaVersion": release_evidence.SCHEMA_VERSION,
        "releaseId": "advertified-2026-09-12-001",
        "commitSha": "b" * 40,
        "createdAtUtc": "2026-09-12T10:00:00Z",
        "images": {
            "api": "registry.example/advertified/api" + digest,
            "migrator": "registry.example/advertified/migrator" + digest,
            "agentRuntime": "registry.example/advertified/agent" + digest,
            "web": "registry.example/advertified/web" + digest,
        },
        "configurationPreflight": {
            "passed": True, "verifiedAtUtc": "2026-09-12T09:00:00Z",
            "receipt": "release/preflight.json",
        },
        "databaseRecovery": {
            "backupEncrypted": True,
            "logicalBackupUri": "s3://advertified-backups/release.dump",
            "snapshotId": "snap-0123456789abcdef0",
            "backupCapturedAtUtc": "2026-09-12T08:00:00Z",
            "restoreRehearsal": {
                "passed": True, "isolatedTarget": True, "postgisVerified": True,
                "pgvectorVerified": True, "migrationRangeVerified": True,
                "masterDataVerified": True, "tenantIsolationVerified": True,
                "measuredRpoSeconds": 30, "measuredRtoSeconds": 420,
                "completedAtUtc": "2026-09-12T09:30:00Z",
                "receipt": "release/restore-rehearsal.json",
            },
        },
        "readiness": {
            "api": True, "worker": True, "agentRuntime": True, "web": True,
            "criticalSmokeJourneys": True, "verifiedAtUtc": "2026-09-12T09:45:00Z",
            "receipt": "release/readiness.json",
        },
        "security": {
            "tenantIsolationPassed": True, "sbomRetained": True, "secretScanPassed": True,
            "highCriticalVulnerabilities": 0, "receipt": "release/security.json",
        },
        "alerting": {
            "apiHealth": True, "workerFailures": True, "databaseHealth": True,
            "backupFailures": True, "diskCapacity": True, "receipt": "release/alerts.json",
        },
        "externalIntegrations": {
            key: {
                "activated": True,
                "reconciliationTestPassed": True,
                "verifiedAtUtc": "2026-09-12T09:50:00Z",
                "receipt": f"release/integrations/{key}.json",
            }
            for key in sorted(release_evidence.INTEGRATION_KEYS)
        },
        "fundingRoutes": manual_funding_evidence(),
        "approvals": approvals,
        "rollback": {
            "previousReleaseId": "advertified-previous-release",
            "preflightRehearsed": True,
            "databaseCompatibilityVerified": True,
            "receipt": "release/rollback-rehearsal.json",
        },
    }


def manual_funding_evidence():
    routes = {}
    for name, mode, evidence_field in (
        ("manualEft", "MANUAL_EFT", "bankingDetailsReviewed"),
        ("advertiseNowPayLater", "MANUAL_PARTNER_REFERRAL", "partnerNotificationEvidenceVerified"),
    ):
        routes[name] = {
            "activated": True, "mode": mode, evidence_field: True,
            "invoiceIdentityVerified": True, "independentReconciliationTestPassed": True,
            "duplicateReconciliationRejected": True, "verifiedAtUtc": "2026-09-12T09:50:00Z",
            "receipt": f"release/funding/{name}.json",
        }
    routes["vodaPay"] = {"activated": False, "status": "AWAITING_PROVIDER_DETAILS",
                         "decisionReference": "ADVERTIFIED.md#20.1.1"}
    return routes


def test_complete_release_evidence_passes() -> None:
    release_evidence.validate_release_evidence(valid_evidence())


def test_release_gate_rejects_missing_human_approval() -> None:
    payload = valid_evidence()
    payload["approvals"] = [
        item for item in payload["approvals"] if item["role"] != "PRIVACY"
    ]
    try:
        release_evidence.validate_release_evidence(payload)
    except release_evidence.EvidenceError as error:
        assert "PRIVACY" in str(error)
    else:
        raise AssertionError("Missing privacy approval must be a NO-GO.")


def test_release_gate_rejects_unmeasured_restore() -> None:
    payload = valid_evidence()
    payload["databaseRecovery"]["restoreRehearsal"]["measuredRtoSeconds"] = 0
    try:
        release_evidence.validate_release_evidence(payload)
    except release_evidence.EvidenceError as error:
        assert "measuredRtoSeconds" in str(error)
    else:
        raise AssertionError("An unmeasured restore must be a NO-GO.")


def test_release_gate_rejects_inactive_external_provider() -> None:
    payload = valid_evidence()
    payload["externalIntegrations"]["oidc"]["activated"] = False
    try:
        release_evidence.validate_release_evidence(payload)
    except release_evidence.EvidenceError as error:
        assert "oidc.activated" in str(error)
    else:
        raise AssertionError("Inactive identity integration must remain a production NO-GO.")


def test_release_gate_rejects_mutable_image_and_security_debt() -> None:
    for mutation, expected in (
        (("images", "api", "registry.example/advertified/api:latest"), "immutable sha256"),
        (("security", "highCriticalVulnerabilities", 1), "must be zero"),
    ):
        payload = copy.deepcopy(valid_evidence())
        section, field, value = mutation
        payload[section][field] = value
        try:
            release_evidence.validate_release_evidence(payload)
        except release_evidence.EvidenceError as error:
            assert expected in str(error)
        else:
            raise AssertionError(f"Defect {section}.{field} must be a NO-GO.")


@pytest.mark.parametrize("route, field, value", [
    ("manualEft", "bankingDetailsReviewed", False),
    ("manualEft", "invoiceIdentityVerified", False),
    ("manualEft", "duplicateReconciliationRejected", False),
    ("advertiseNowPayLater", "partnerNotificationEvidenceVerified", False),
    ("advertiseNowPayLater", "independentReconciliationTestPassed", False),
    ("advertiseNowPayLater", "mode", "AUTOMATED_PROVIDER"),
    ("vodaPay", "activated", True),
    ("vodaPay", "decisionReference", ""),
])
def test_manual_funding_decision_does_not_waive_evidence(route, field, value):
    payload = valid_evidence()
    payload["fundingRoutes"][route][field] = value
    with pytest.raises(release_evidence.EvidenceError, match=route):
        release_evidence.validate_release_evidence(payload)


def test_old_release_schema_cannot_implicitly_waive_selected_funding_routes():
    payload = valid_evidence()
    payload["schemaVersion"] = "advertified-production-release-evidence/v1"
    with pytest.raises(release_evidence.EvidenceError, match="schemaVersion"):
        release_evidence.validate_release_evidence(payload)

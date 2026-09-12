"""Fail-closed validation of retained Advertified production release evidence.

This tool is intentionally read-only. It validates evidence produced by real deployment,
backup/restore, security, smoke and human approval work; it never performs those actions.
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from datetime import datetime
from pathlib import Path
from typing import Any

SCHEMA_VERSION = "advertified-production-release-evidence/v2"
IMAGE_KEYS = {"api", "migrator", "agentRuntime", "web"}
APPROVAL_ROLES = {
    "LEGAL",
    "PRIVACY",
    "ASSET_RIGHTS",
    "ROLE_GOVERNANCE",
    "FINANCE",
    "OPERATIONS",
    "RELEASE_OWNER",
}
INTEGRATION_KEYS = {
    "oidc",
    "resend",
    "bedrock",
    "s3",
    "eventBridge",
}
DIGEST = re.compile(r"^.+@sha256:[0-9a-f]{64}$")
COMMIT = re.compile(r"^[0-9a-f]{40}$")
SNAPSHOT = re.compile(r"^snap-[0-9a-f]+$")


class EvidenceError(ValueError):
    pass


def _object(value: Any, name: str) -> dict[str, Any]:
    if not isinstance(value, dict):
        raise EvidenceError(f"{name} must be an object.")
    return value


def _text(value: Any, name: str) -> str:
    if not isinstance(value, str) or not value.strip() or "REPLACE_WITH" in value:
        raise EvidenceError(f"{name} must be a non-placeholder string.")
    return value.strip()


def _utc(value: Any, name: str) -> str:
    text = _text(value, name)
    try:
        parsed = datetime.fromisoformat(text.replace("Z", "+00:00"))
    except ValueError as error:
        raise EvidenceError(f"{name} must be ISO-8601.") from error
    if parsed.tzinfo is None or parsed.utcoffset() is None:
        raise EvidenceError(f"{name} must include a timezone.")
    return text


def _true(value: Any, name: str) -> None:
    if value is not True:
        raise EvidenceError(f"{name} must be true.")


def _non_negative(value: Any, name: str, *, positive: bool = False) -> int:
    if isinstance(value, bool) or not isinstance(value, int) or value < (1 if positive else 0):
        qualifier = "positive" if positive else "non-negative"
        raise EvidenceError(f"{name} must be a {qualifier} integer.")
    return value


def validate_release_evidence(payload: dict[str, Any]) -> None:
    if payload.get("schemaVersion") != SCHEMA_VERSION:
        raise EvidenceError(f"schemaVersion must be {SCHEMA_VERSION}.")
    commit = _text(payload.get("commitSha"), "commitSha")
    if not COMMIT.fullmatch(commit):
        raise EvidenceError("commitSha must be an exact 40-character lowercase Git SHA.")
    _text(payload.get("releaseId"), "releaseId")
    _utc(payload.get("createdAtUtc"), "createdAtUtc")
    _validate_images(_object(payload.get("images"), "images"))
    _validate_preflight(_object(payload.get("configurationPreflight"), "configurationPreflight"))
    _validate_backup(_object(payload.get("databaseRecovery"), "databaseRecovery"))
    _validate_readiness(_object(payload.get("readiness"), "readiness"))
    _validate_security(_object(payload.get("security"), "security"))
    _validate_alerting(_object(payload.get("alerting"), "alerting"))
    _validate_integrations(_object(payload.get("externalIntegrations"), "externalIntegrations"))
    _validate_funding_routes(_object(payload.get("fundingRoutes"), "fundingRoutes"))
    _validate_approvals(payload.get("approvals"))
    rollback = _object(payload.get("rollback"), "rollback")
    _text(rollback.get("previousReleaseId"), "rollback.previousReleaseId")
    _true(rollback.get("preflightRehearsed"), "rollback.preflightRehearsed")
    _true(rollback.get("databaseCompatibilityVerified"), "rollback.databaseCompatibilityVerified")
    _text(rollback.get("receipt"), "rollback.receipt")


def _validate_images(images: dict[str, Any]) -> None:
    if set(images) != IMAGE_KEYS:
        raise EvidenceError(f"images must contain exactly {sorted(IMAGE_KEYS)}.")
    for key, value in images.items():
        image = _text(value, f"images.{key}")
        if not DIGEST.fullmatch(image):
            raise EvidenceError(f"images.{key} must use an immutable sha256 digest.")


def _validate_preflight(value: dict[str, Any]) -> None:
    _true(value.get("passed"), "configurationPreflight.passed")
    _utc(value.get("verifiedAtUtc"), "configurationPreflight.verifiedAtUtc")
    _text(value.get("receipt"), "configurationPreflight.receipt")


def _validate_backup(value: dict[str, Any]) -> None:
    _true(value.get("backupEncrypted"), "databaseRecovery.backupEncrypted")
    logical = _text(value.get("logicalBackupUri"), "databaseRecovery.logicalBackupUri")
    if not logical.startswith("s3://"):
        raise EvidenceError("databaseRecovery.logicalBackupUri must be a private S3 URI.")
    snapshot = _text(value.get("snapshotId"), "databaseRecovery.snapshotId")
    if not SNAPSHOT.fullmatch(snapshot):
        raise EvidenceError("databaseRecovery.snapshotId must be an EBS snapshot id.")
    _utc(value.get("backupCapturedAtUtc"), "databaseRecovery.backupCapturedAtUtc")
    restore = _object(value.get("restoreRehearsal"), "databaseRecovery.restoreRehearsal")
    for field in (
        "passed",
        "isolatedTarget",
        "postgisVerified",
        "pgvectorVerified",
        "migrationRangeVerified",
        "masterDataVerified",
        "tenantIsolationVerified",
    ):
        _true(restore.get(field), f"databaseRecovery.restoreRehearsal.{field}")
    _non_negative(restore.get("measuredRpoSeconds"),
                  "databaseRecovery.restoreRehearsal.measuredRpoSeconds")
    _non_negative(restore.get("measuredRtoSeconds"),
                  "databaseRecovery.restoreRehearsal.measuredRtoSeconds", positive=True)
    _utc(restore.get("completedAtUtc"), "databaseRecovery.restoreRehearsal.completedAtUtc")
    _text(restore.get("receipt"), "databaseRecovery.restoreRehearsal.receipt")


def _validate_readiness(value: dict[str, Any]) -> None:
    for field in ("api", "worker", "agentRuntime", "web", "criticalSmokeJourneys"):
        _true(value.get(field), f"readiness.{field}")
    _utc(value.get("verifiedAtUtc"), "readiness.verifiedAtUtc")
    _text(value.get("receipt"), "readiness.receipt")


def _validate_security(value: dict[str, Any]) -> None:
    _true(value.get("tenantIsolationPassed"), "security.tenantIsolationPassed")
    _true(value.get("sbomRetained"), "security.sbomRetained")
    _true(value.get("secretScanPassed"), "security.secretScanPassed")
    if _non_negative(value.get("highCriticalVulnerabilities"),
                     "security.highCriticalVulnerabilities") != 0:
        raise EvidenceError("security.highCriticalVulnerabilities must be zero.")
    _text(value.get("receipt"), "security.receipt")


def _validate_alerting(value: dict[str, Any]) -> None:
    for field in ("apiHealth", "workerFailures", "databaseHealth", "backupFailures", "diskCapacity"):
        _true(value.get(field), f"alerting.{field}")
    _text(value.get("receipt"), "alerting.receipt")


def _validate_integrations(value: dict[str, Any]) -> None:
    if set(value) != INTEGRATION_KEYS:
        raise EvidenceError(
            f"externalIntegrations must contain exactly {sorted(INTEGRATION_KEYS)}.")
    for name, raw in value.items():
        integration = _object(raw, f"externalIntegrations.{name}")
        _true(integration.get("activated"), f"externalIntegrations.{name}.activated")
        _true(integration.get("reconciliationTestPassed"),
              f"externalIntegrations.{name}.reconciliationTestPassed")
        _utc(integration.get("verifiedAtUtc"), f"externalIntegrations.{name}.verifiedAtUtc")
        _text(integration.get("receipt"), f"externalIntegrations.{name}.receipt")


def _validate_funding_routes(value: dict[str, Any]) -> None:
    if set(value) != {"manualEft", "advertiseNowPayLater", "vodaPay"}:
        raise EvidenceError("fundingRoutes must declare Manual EFT, manual ANPL and deferred VodaPay.")
    for name, mode, evidence_field in (
        ("manualEft", "MANUAL_EFT", "bankingDetailsReviewed"),
        ("advertiseNowPayLater", "MANUAL_PARTNER_REFERRAL", "partnerNotificationEvidenceVerified"),
    ):
        route = _object(value[name], f"fundingRoutes.{name}")
        if route.get("mode") != mode:
            raise EvidenceError(f"fundingRoutes.{name}.mode must be {mode}.")
        for field in ("activated", "invoiceIdentityVerified", "independentReconciliationTestPassed",
                      "duplicateReconciliationRejected", evidence_field):
            _true(route.get(field), f"fundingRoutes.{name}.{field}")
        _utc(route.get("verifiedAtUtc"), f"fundingRoutes.{name}.verifiedAtUtc")
        _text(route.get("receipt"), f"fundingRoutes.{name}.receipt")
    deferred = _object(value["vodaPay"], "fundingRoutes.vodaPay")
    if deferred.get("activated") is not False or deferred.get("status") != "AWAITING_PROVIDER_DETAILS":
        raise EvidenceError("fundingRoutes.vodaPay must remain inactive pending provider details.")
    if deferred.get("decisionReference") != "ADVERTIFIED.md#20.1.1":
        raise EvidenceError("fundingRoutes.vodaPay must retain the explicit owner deferral decision.")


def _validate_approvals(value: Any) -> None:
    if not isinstance(value, list) or not value:
        raise EvidenceError("approvals must be a non-empty array.")
    by_role: dict[str, dict[str, Any]] = {}
    for index, item in enumerate(value):
        approval = _object(item, f"approvals[{index}]")
        role = _text(approval.get("role"), f"approvals[{index}].role")
        if role in by_role:
            raise EvidenceError(f"Duplicate release approval role {role}.")
        _text(approval.get("approver"), f"approvals[{index}].approver")
        _utc(approval.get("approvedAtUtc"), f"approvals[{index}].approvedAtUtc")
        _text(approval.get("evidence"), f"approvals[{index}].evidence")
        by_role[role] = approval
    missing = APPROVAL_ROLES - set(by_role)
    if missing:
        raise EvidenceError(f"Missing required release approvals: {sorted(missing)}.")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("evidence", type=Path)
    args = parser.parse_args()
    try:
        payload = json.loads(args.evidence.read_text(encoding="utf-8"))
        validate_release_evidence(_object(payload, "root"))
    except (OSError, json.JSONDecodeError, EvidenceError) as error:
        print(f"NO-GO: {error}", file=sys.stderr)
        return 1
    print("GO: retained production release evidence satisfies the repository gate.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

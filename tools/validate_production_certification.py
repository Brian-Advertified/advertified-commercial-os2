"""Fail-closed validation for the retained production-certification pack."""

from __future__ import annotations

import argparse
import hashlib
import json
from collections import Counter, defaultdict
from datetime import UTC, datetime
from pathlib import Path
from typing import Any

from production_certification_contract import (
    CERTIFICATION_CHECKS,
    COMMIT_ID,
    DEFAULT_MASTER_DATA,
    GOVERNANCE_SIGNOFFS,
    HEX_64,
    JOURNEY_COUNTS,
    LIFECYCLE_CHECKS,
    SCHEMA_VERSION,
    UNIQUE_SCENARIO_FIELDS,
)


class CertificationError(ValueError):
    """The retained pack is incomplete, inconsistent, or tampered with."""


class EvidenceVerifier:
    def __init__(self, root: Path, errors: list[str]) -> None:
        self.root = root.resolve(strict=True)
        self.errors = errors

    def check(self, value: Any, label: str, *, pdf: bool = False) -> None:
        evidence = mapping(value, label, self.errors)
        relative = text_value(evidence.get("path"), f"{label}.path", self.errors)
        expected = text_value(
            evidence.get("sha256"), f"{label}.sha256", self.errors
        )
        if not relative or not expected:
            return
        if not HEX_64.fullmatch(expected):
            self.errors.append(f"{label}.sha256 must be 64 lowercase hex characters.")
            return
        path = self._safe_path(relative, label)
        if path is None or not path.is_file():
            self.errors.append(f"{label}.path does not identify a retained file.")
            return
        payload = path.read_bytes()
        if hashlib.sha256(payload).hexdigest() != expected:
            self.errors.append(f"{label} checksum does not match the retained file.")
        if pdf and (path.suffix.lower() != ".pdf" or not payload.startswith(b"%PDF-")):
            self.errors.append(f"{label} must identify a PDF file.")

    def _safe_path(self, relative: str, label: str) -> Path | None:
        candidate = Path(relative)
        if candidate.is_absolute() or ".." in candidate.parts:
            self.errors.append(f"{label}.path must stay inside the evidence root.")
            return None
        resolved = (self.root / candidate).resolve()
        if not resolved.is_relative_to(self.root):
            self.errors.append(f"{label}.path escapes the evidence root.")
            return None
        return resolved


def mapping(value: Any, label: str, errors: list[str]) -> dict[str, Any]:
    if isinstance(value, dict):
        return value
    errors.append(f"{label} must be an object.")
    return {}


def list_value(value: Any, label: str, errors: list[str]) -> list[Any]:
    if isinstance(value, list):
        return value
    errors.append(f"{label} must be an array.")
    return []


def text_value(value: Any, label: str, errors: list[str]) -> str:
    if isinstance(value, str) and value.strip():
        return value.strip()
    errors.append(f"{label} must be a non-empty string.")
    return ""


def utc_value(value: Any, label: str, errors: list[str]) -> datetime | None:
    raw = text_value(value, label, errors)
    if not raw:
        return None
    try:
        parsed = datetime.fromisoformat(raw.replace("Z", "+00:00"))
    except ValueError:
        errors.append(f"{label} must be an ISO-8601 timestamp.")
        return None
    if parsed.utcoffset() != UTC.utcoffset(parsed):
        errors.append(f"{label} must include a UTC offset.")
        return None
    return parsed.astimezone(UTC)


def validate_approval(
    value: Any,
    label: str,
    verifier: EvidenceVerifier,
) -> tuple[str, datetime | None]:
    record = mapping(value, label, verifier.errors)
    if record.get("decision") != "APPROVED":
        verifier.errors.append(f"{label}.decision must be APPROVED.")
    actor = text_value(record.get("approverId"), f"{label}.approverId", verifier.errors)
    approved_at = utc_value(
        record.get("approvedAtUtc"), f"{label}.approvedAtUtc", verifier.errors
    )
    verifier.check(record.get("evidence"), f"{label}.evidence")
    return actor, approved_at


def validate_lifecycle(
    value: Any,
    journey: str,
    label: str,
    verifier: EvidenceVerifier,
) -> None:
    records = mapping(value, label, verifier.errors)
    required = set(LIFECYCLE_CHECKS.get(journey, ()))
    missing = sorted(required - set(records))
    if missing:
        verifier.errors.append(f"{label} is missing: {', '.join(missing)}.")
    for check_id in sorted(required & set(records)):
        verifier.check(records[check_id], f"{label}.{check_id}")


def validate_scenario(
    value: Any,
    index: int,
    verifier: EvidenceVerifier,
) -> dict[str, Any]:
    label = f"scenarios[{index}]"
    record = mapping(value, label, verifier.errors)
    scenario_id = text_value(
        record.get("scenarioId"), f"{label}.scenarioId", verifier.errors
    )
    journey = text_value(record.get("journey"), f"{label}.journey", verifier.errors)
    difficulty = text_value(
        record.get("difficulty"), f"{label}.difficulty", verifier.errors
    )
    if record.get("accepted") is not True:
        verifier.errors.append(f"{label}.accepted must be true.")
    client = text_value(record.get("clientName"), f"{label}.clientName", verifier.errors)
    completed = utc_value(
        record.get("completedAtUtc"), f"{label}.completedAtUtc", verifier.errors
    )
    pdf = mapping(record.get("proposalPdf"), f"{label}.proposalPdf", verifier.errors)
    verifier.check(pdf, f"{label}.proposalPdf", pdf=True)
    _, commercial_at = validate_approval(
        record.get("commercialApproval"), f"{label}.commercialApproval", verifier
    )
    _, visual_at = validate_approval(
        record.get("visualApproval"), f"{label}.visualApproval", verifier
    )
    validate_lifecycle(
        record.get("lifecycleEvidence"),
        journey,
        f"{label}.lifecycleEvidence",
        verifier,
    )
    timestamps = [
        timestamp
        for timestamp in (completed, commercial_at, visual_at)
        if timestamp is not None
    ]
    return {
        "label": label,
        "scenarioId": scenario_id,
        "journey": journey,
        "difficulty": difficulty.casefold(),
        "rayetsa": journey == "UNBRIEFED_OPPORTUNITY"
        and client.casefold() == "rayetsa furniture",
        "pdfPath": str(pdf.get("path") or ""),
        "pdfHash": str(pdf.get("sha256") or ""),
        "timestamps": timestamps,
    }


def validate_scenarios(
    manifest: dict[str, Any],
    verifier: EvidenceVerifier,
) -> list[datetime]:
    scenarios = list_value(manifest.get("scenarios"), "scenarios", verifier.errors)
    counts: Counter[str] = Counter()
    difficulties: dict[str, set[str]] = defaultdict(set)
    unique_values = {"scenarioId": set(), "pdfPath": set(), "pdfHash": set()}
    timestamps: list[datetime] = []
    rayetsa = False

    if len(scenarios) != sum(JOURNEY_COUNTS.values()):
        verifier.errors.append("scenarios must contain exactly 30 records.")
    for index, value in enumerate(scenarios):
        result = validate_scenario(value, index, verifier)
        counts[result["journey"]] += 1
        difficulties[result["journey"]].add(result["difficulty"])
        timestamps.extend(result["timestamps"])
        rayetsa |= result["rayetsa"]
        for key, seen in unique_values.items():
            if result[key] in seen:
                field = UNIQUE_SCENARIO_FIELDS[key]
                verifier.errors.append(
                    f"{result['label']}.{field} must be unique."
                )
            seen.add(result[key])

    for journey, expected in JOURNEY_COUNTS.items():
        if counts[journey] != expected:
            verifier.errors.append(f"{journey} must contain exactly {expected} scenarios.")
        if len(difficulties[journey]) < 3:
            verifier.errors.append(f"{journey} must contain at least three difficulty levels.")
    if not rayetsa:
        verifier.errors.append("UNBRIEFED_OPPORTUNITY must include Rayetsa Furniture.")
    return timestamps


def validate_named_records(
    values: Any,
    label: str,
    key: str,
    required: set[str],
    verifier: EvidenceVerifier,
) -> list[datetime]:
    records = list_value(values, label, verifier.errors)
    found: set[str] = set()
    timestamps: list[datetime] = []
    for index, value in enumerate(records):
        item_label = f"{label}[{index}]"
        record = mapping(value, item_label, verifier.errors)
        record_id = text_value(record.get(key), f"{item_label}.{key}", verifier.errors)
        if record_id in found:
            verifier.errors.append(f"{item_label}.{key} is duplicated.")
        found.add(record_id)
        if record.get("status") != "PASS":
            verifier.errors.append(f"{item_label}.status must be PASS.")
        text_value(record.get("recordedBy"), f"{item_label}.recordedBy", verifier.errors)
        timestamp = utc_value(
            record.get("recordedAtUtc"), f"{item_label}.recordedAtUtc", verifier.errors
        )
        if timestamp:
            timestamps.append(timestamp)
        verifier.check(record.get("evidence"), f"{item_label}.evidence")
    missing = sorted(required - found)
    unexpected = sorted(found - required)
    if missing:
        verifier.errors.append(f"{label} is missing: {', '.join(missing)}.")
    if unexpected:
        verifier.errors.append(f"{label} contains unknown entries: {', '.join(unexpected)}.")
    return timestamps


def active_human_roles(master_data_path: Path) -> set[str]:
    root = read_object(master_data_path)
    roles = root.get("collections", {}).get("roles", [])
    return {
        str(role["code"])
        for role in roles
        if role.get("isActive") is True
        and not str(role.get("code", "")).endswith("_service")
    }


def validate_release(
    manifest: dict[str, Any],
    verifier: EvidenceVerifier,
) -> None:
    release = mapping(manifest.get("release"), "release", verifier.errors)
    commit = text_value(release.get("commitSha"), "release.commitSha", verifier.errors)
    if commit and not COMMIT_ID.fullmatch(commit):
        verifier.errors.append("release.commitSha must be a lowercase Git object ID.")
    text_value(release.get("buildId"), "release.buildId", verifier.errors)
    if release.get("environment") != "production":
        verifier.errors.append("release.environment must be production.")
    for field in ("schemaMigrationRange", "masterDataVersion", "policyVersion"):
        text_value(release.get(field), f"release.{field}", verifier.errors)
    digests = mapping(
        release.get("containerDigests"), "release.containerDigests", verifier.errors
    )
    if not digests:
        verifier.errors.append("release.containerDigests must not be empty.")
    for name, digest in digests.items():
        if not isinstance(name, str) or not name or not isinstance(digest, str):
            verifier.errors.append("release.containerDigests entries must be strings.")
        elif not digest.startswith("sha256:") or not HEX_64.fullmatch(digest[7:]):
            verifier.errors.append(f"release.containerDigests.{name} is not immutable.")
    if not isinstance(release.get("knownLimitations"), list):
        verifier.errors.append("release.knownLimitations must be an array.")
    verifier.check(release.get("rollbackPlan"), "release.rollbackPlan")
    verifier.check(release.get("incidentPlan"), "release.incidentPlan")


def validate_final_go(
    manifest: dict[str, Any],
    verifier: EvidenceVerifier,
    preceding: list[datetime],
) -> None:
    final_go = mapping(manifest.get("finalGo"), "finalGo", verifier.errors)
    if final_go.get("decision") != "GO":
        verifier.errors.append("finalGo.decision must be GO.")
    text_value(final_go.get("approvedBy"), "finalGo.approvedBy", verifier.errors)
    approved_at = utc_value(
        final_go.get("approvedAtUtc"), "finalGo.approvedAtUtc", verifier.errors
    )
    verifier.check(final_go.get("evidence"), "finalGo.evidence")
    if approved_at and preceding and approved_at < max(preceding):
        verifier.errors.append("finalGo must be recorded after every retained approval and check.")


def validate_manifest(
    manifest_path: Path,
    evidence_root: Path,
    master_data_path: Path = DEFAULT_MASTER_DATA,
) -> dict[str, Any]:
    manifest = read_object(manifest_path)
    errors: list[str] = []
    verifier = EvidenceVerifier(evidence_root, errors)
    if manifest.get("schemaVersion") != SCHEMA_VERSION:
        errors.append(f"schemaVersion must be {SCHEMA_VERSION}.")
    validate_release(manifest, verifier)
    timestamps = validate_scenarios(manifest, verifier)
    timestamps += validate_named_records(
        manifest.get("certificationChecks"),
        "certificationChecks",
        "checkId",
        set(CERTIFICATION_CHECKS),
        verifier,
    )
    timestamps += validate_named_records(
        manifest.get("roleSignoffs"),
        "roleSignoffs",
        "roleCode",
        active_human_roles(master_data_path),
        verifier,
    )
    timestamps += validate_named_records(
        manifest.get("governanceSignoffs"),
        "governanceSignoffs",
        "area",
        set(GOVERNANCE_SIGNOFFS),
        verifier,
    )
    validate_final_go(manifest, verifier, timestamps)
    if errors:
        raise CertificationError("\n".join(f"- {error}" for error in errors))
    return {
        "verdict": "GO",
        "scenarioCount": sum(JOURNEY_COUNTS.values()),
        "proposalPdfCount": sum(JOURNEY_COUNTS.values()),
        "certificationCheckCount": len(CERTIFICATION_CHECKS),
        "roleSignoffCount": len(active_human_roles(master_data_path)),
        "governanceSignoffCount": len(GOVERNANCE_SIGNOFFS),
    }


def read_object(path: Path) -> dict[str, Any]:
    value = json.loads(path.resolve(strict=True).read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise CertificationError(f"{path} must contain a JSON object.")
    return value


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Validate retained Advertified production-certification evidence."
    )
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--evidence-root", type=Path, required=True)
    parser.add_argument("--master-data", type=Path, default=DEFAULT_MASTER_DATA)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    try:
        result = validate_manifest(
            args.manifest,
            args.evidence_root,
            args.master_data,
        )
    except (CertificationError, OSError, json.JSONDecodeError) as error:
        print(json.dumps({"verdict": "NO-GO", "errors": str(error).splitlines()}, indent=2))
        return 2
    print(json.dumps(result, indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

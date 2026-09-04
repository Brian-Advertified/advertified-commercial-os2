"""Approve and partially publish only fully grounded certified inventory.

All certified inventory remains uploaded. This command approves rows whose
commercial facts are complete enough for proposal pricing, publishes those rows,
and leaves every ambiguous or quote-required row at human review. It never
rejects a row merely to complete an import.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from datetime import UTC, datetime
from pathlib import Path
from typing import Any

from inventory_certified_upload_client import CertifiedInventoryApi
from report_certified_inventory_review_readiness import candidate_readiness

ROOT = Path(__file__).resolve().parents[1]
CORPUS = ROOT / "artifacts" / "inventory-corpus"
UPLOAD_ROOT = CORPUS / "certified-upload"
DATABASE_MARKER = UPLOAD_ROOT / "DATABASE_UPLOAD_RECONCILED.json"
STATE_PATH = UPLOAD_ROOT / "database-upload-state.json"
CONTRACT_PATH = UPLOAD_ROOT / "api-contract.json"
READINESS_PATH = UPLOAD_ROOT / "review-readiness.json"
RESULT_PATH = UPLOAD_ROOT / "publication-reconciliation.json"
MARKER_PATH = UPLOAD_ROOT / "PUBLISHABLE_INVENTORY_PUBLISHED.json"
TENANT_ID = "10000000-0000-0000-0000-000000000020"


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--api", default="http://127.0.0.1:5197")
    parser.add_argument("--origin", default="http://localhost:3017")
    parser.add_argument("--tenant", default=TENANT_ID)
    args = parser.parse_args()

    require(DATABASE_MARKER, "databaseUploadReconciled")
    state = read_json(STATE_PATH)
    contract = read_json(CONTRACT_PATH)
    client = CertifiedInventoryApi(args.api, args.origin, args.tenant, contract)
    client.authenticate()

    import_results: list[dict[str, Any]] = []
    total_approved = 0
    total_published = 0
    total_review_required = 0
    for entry in state.get("workbooks") or []:
        result = process_import(client, entry)
        import_results.append(result)
        total_approved += result["approvedDuringRun"]
        total_published += result["publishedCandidateCount"]
        total_review_required += result["reviewRequiredCandidateCount"]

    reconciliation = {
        "schemaVersion": "advertified.certified-inventory-publication.v1",
        "generatedAtUtc": datetime.now(UTC).isoformat(),
        "databaseUploadCertificateSha256": file_hash(DATABASE_MARKER),
        "importCount": len(import_results),
        "approvedDuringRun": total_approved,
        "publishedCandidateCount": total_published,
        "reviewRequiredCandidateCount": total_review_required,
        "rejectedCandidateCount": sum(
            item["rejectedCandidateCount"] for item in import_results
        ),
        "atLeastOneCertifiedProductPublished": total_published > 0,
        "noCandidateRejectedToCompletePublication": all(
            item["rejectedDuringRun"] == 0 for item in import_results
        ),
        "allUploadedImportsAccountedFor": len(import_results) == len(
            state.get("workbooks") or []
        ),
        "imports": import_results,
    }
    reconciliation["passed"] = (
        reconciliation["atLeastOneCertifiedProductPublished"]
        and reconciliation["noCandidateRejectedToCompletePublication"]
        and reconciliation["allUploadedImportsAccountedFor"]
    )
    write_json(RESULT_PATH, reconciliation)
    if not reconciliation["passed"]:
        MARKER_PATH.unlink(missing_ok=True)
        raise RuntimeError(
            "Certified inventory publication did not produce a safe priced subset."
        )
    write_json(MARKER_PATH, {
        "publishableInventoryPublished": True,
        "importCount": reconciliation["importCount"],
        "publishedCandidateCount": total_published,
        "reviewRequiredCandidateCount": total_review_required,
        "rejectedDuringRun": 0,
        "publicationReconciliationSha256": file_hash(RESULT_PATH),
    })
    print(json.dumps({
        "passed": True,
        "importCount": reconciliation["importCount"],
        "approvedDuringRun": total_approved,
        "publishedCandidateCount": total_published,
        "reviewRequiredCandidateCount": total_review_required,
        "rejectedDuringRun": 0,
        "output": str(RESULT_PATH.relative_to(ROOT)),
    }, indent=2))
    return 0


def process_import(
    client: CertifiedInventoryApi,
    state_entry: dict[str, Any],
) -> dict[str, Any]:
    import_id = str(state_entry["importId"])
    current = client.read_complete_import(import_id)
    before_rejected = count_status(current, "REJECTED")
    approved_during_run = 0
    blocked: list[dict[str, Any]] = []

    for candidate in current.get("candidates") or []:
        status = str(candidate.get("status") or "")
        readiness = candidate_readiness(candidate)
        if status != "REVIEW_REQUIRED":
            continue
        if not readiness["publishable"]:
            blocked.append({
                "candidateId": candidate.get("id"),
                "productCode": readiness.get("productCode"),
                "name": readiness.get("name"),
                "blockingReasons": readiness["blockingReasons"],
            })
            continue
        candidate_id = str(candidate.get("id") or candidate.get("candidateId") or "")
        if not candidate_id:
            raise RuntimeError(
                f"Import {import_id} contains a publishable candidate without an ID."
            )
        version = candidate.get("version")
        if version is None:
            version = current.get("version")
        values = candidate_values(candidate)
        client.review_candidate(
            import_id,
            candidate_id,
            int(version),
            "APPROVE",
            "Approved from the physically certified, Bedrock-validated upload. All commercial fields required for proposal pricing are source-grounded.",
            values,
        )
        approved_during_run += 1

    after_review = client.read_complete_import(import_id)
    approved_count = count_status(after_review, "APPROVED")
    if approved_count > 0:
        client.publish_import(import_id, int(after_review.get("version") or 0))
    final = client.read_complete_import(import_id)
    after_rejected = count_status(final, "REJECTED")
    published = count_status(final, "PUBLISHED")
    review_required = count_status(final, "REVIEW_REQUIRED")
    approved_remaining = count_status(final, "APPROVED")
    if after_rejected != before_rejected:
        raise RuntimeError(
            f"Import {import_id} rejected candidates during safe publication."
        )
    if approved_remaining:
        raise RuntimeError(
            f"Import {import_id} still contains approved but unpublished candidates."
        )
    return {
        "importId": import_id,
        "supplierName": state_entry["supplierName"],
        "finalImportStatus": final.get("status"),
        "approvedDuringRun": approved_during_run,
        "publishedCandidateCount": published,
        "reviewRequiredCandidateCount": review_required,
        "rejectedCandidateCount": after_rejected,
        "rejectedDuringRun": after_rejected - before_rejected,
        "blockedCandidates": blocked,
        "failureCode": final.get("failureCode"),
    }


def candidate_values(candidate: dict[str, Any]) -> dict[str, Any]:
    values = (
        candidate.get("canonicalValues")
        or candidate.get("proposedValues")
        or candidate.get("values")
    )
    if not isinstance(values, dict):
        raise RuntimeError("Candidate values are unavailable for approval.")
    return values


def count_status(import_view: dict[str, Any], status: str) -> int:
    return sum(
        str(item.get("status") or "") == status
        for item in import_view.get("candidates") or []
    )


def require(path: Path, field: str) -> dict[str, Any]:
    value = read_json(path)
    if not value.get(field):
        raise RuntimeError(f"Required certificate {path.name} is invalid.")
    return value


def file_hash(path: Path) -> str:
    return hashlib.sha256(path.resolve(strict=True).read_bytes()).hexdigest()


def read_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.resolve(strict=True).read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError(f"Expected object in {path}")
    return value


def write_json(path: Path, value: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8")


if __name__ == "__main__":
    raise SystemExit(main())

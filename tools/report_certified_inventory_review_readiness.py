"""Report publishability of every certified uploaded inventory candidate."""
from __future__ import annotations
import json
from collections import Counter
from datetime import UTC, datetime
from pathlib import Path
from typing import Any

from inventory_certified_upload_client import CertifiedInventoryApi

ROOT = Path(__file__).resolve().parents[1]
CORPUS = ROOT / "artifacts" / "inventory-corpus"
UPLOAD_ROOT = CORPUS / "certified-upload"
DATABASE_MARKER = UPLOAD_ROOT / "DATABASE_UPLOAD_RECONCILED.json"
STATE_PATH = UPLOAD_ROOT / "database-upload-state.json"
CONTRACT_PATH = UPLOAD_ROOT / "api-contract.json"
OUTPUT_PATH = UPLOAD_ROOT / "review-readiness.json"
TENANT_ID = "10000000-0000-0000-0000-000000000020"


def main() -> int:
    require(DATABASE_MARKER, "databaseUploadReconciled")
    state = read_json(STATE_PATH)
    contract = read_json(CONTRACT_PATH)
    client = CertifiedInventoryApi(
        "http://127.0.0.1:5197",
        "http://localhost:3017",
        TENANT_ID,
        contract,
    )
    client.authenticate()
    imports = []
    total = publishable = blocked = 0
    issue_counts: Counter[str] = Counter()
    for state_entry in state.get("workbooks") or []:
        current = client.read_complete_import(str(state_entry["importId"]))
        candidate_results = []
        for candidate in current.get("candidates") or []:
            result = candidate_readiness(candidate)
            candidate_results.append(result)
            total += 1
            if result["publishable"]:
                publishable += 1
            else:
                blocked += 1
                issue_counts.update(result["blockingReasons"])
        imports.append({
            "importId": state_entry["importId"],
            "supplierName": state_entry["supplierName"],
            "status": current.get("status"),
            "candidateCount": len(candidate_results),
            "publishableCount": sum(item["publishable"] for item in candidate_results),
            "blockedCount": sum(not item["publishable"] for item in candidate_results),
            "candidates": candidate_results,
        })
    report = {
        "schemaVersion": "advertified.certified-inventory-review-readiness.v1",
        "generatedAtUtc": datetime.now(UTC).isoformat(),
        "importCount": len(imports),
        "candidateCount": total,
        "publishableCount": publishable,
        "blockedCount": blocked,
        "allCandidatesPublishable": total > 0 and blocked == 0,
        "blockingReasonCounts": dict(sorted(issue_counts.items())),
        "imports": imports,
    }
    write_json(OUTPUT_PATH, report)
    print(json.dumps({
        "importCount": report["importCount"],
        "candidateCount": total,
        "publishableCount": publishable,
        "blockedCount": blocked,
        "blockingReasonCounts": report["blockingReasonCounts"],
        "output": str(OUTPUT_PATH.relative_to(ROOT)),
    }, indent=2))
    return 0


def candidate_readiness(candidate: dict[str, Any]) -> dict[str, Any]:
    values = candidate.get("canonicalValues") or candidate.get("proposedValues") or candidate.get("values") or {}
    reasons = []
    for issue in candidate.get("validation") or []:
        if issue.get("isBlocking"):
            reasons.append(str(issue.get("type") or issue.get("code") or issue.get("fieldName") or "BLOCKING_VALIDATION"))
    required = {
        "productCode": values.get("productCode"),
        "name": values.get("name"),
        "channel": values.get("channel"),
        "productType": values.get("productType"),
        "geography": values.get("geography"),
        "rateType": values.get("rateType"),
        "availability": values.get("availability"),
    }
    for field, value in required.items():
        if value in (None, ""):
            reasons.append("MISSING_" + field.upper())
    rate_amount = values.get("rateAmountMinor")
    rate_raw = str(values.get("rateRaw") or (values.get("extension") or {}).get("rate_raw") or "")
    rate_unknown = any(token in rate_raw.upper() for token in ("RATE_ON_REQUEST", "TBC", "POA"))
    if rate_amount is None and not rate_unknown:
        reasons.append("RATE_NOT_RESOLVED_OR_FLAGGED")
    if rate_amount is not None and not values.get("currency"):
        reasons.append("MISSING_CURRENCY")
    return {
        "candidateId": candidate.get("id"),
        "rowNumber": candidate.get("rowNumber"),
        "status": candidate.get("status"),
        "version": candidate.get("version"),
        "productCode": values.get("productCode"),
        "name": values.get("name"),
        "channel": values.get("channel"),
        "productType": values.get("productType"),
        "rateType": values.get("rateType"),
        "currency": values.get("currency"),
        "rateAmountMinor": rate_amount,
        "geography": values.get("geography"),
        "publishable": not reasons,
        "blockingReasons": sorted(set(reasons)),
    }


def require(path: Path, field: str) -> None:
    value = read_json(path)
    if not value.get(field):
        raise RuntimeError(f"Required certificate {path.name} is invalid.")


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

"""Write one authoritative inventory production status from durable certificates."""
from __future__ import annotations
import json
from pathlib import Path
from typing import Any

ROOT = Path(__file__).resolve().parents[1]
CORPUS = ROOT / "artifacts" / "inventory-corpus"
OUTPUT = CORPUS / "CURRENT_PRODUCTION_STATUS.json"

MARKERS = [
    ("PHYSICAL_CERTIFICATION", CORPUS / "physical-certification-v2" / "ALL_43_PHYSICALLY_CERTIFIED.json", "all43PhysicallyCertified"),
    ("BEDROCK_PREFLIGHT", CORPUS / "bedrock-production-certification" / "PREFLIGHT_APPROVED.json", "approved"),
    ("DMS_BEDROCK_CANARY", CORPUS / "bedrock-production-certification" / "DMS_CANARY_PASSED.json", "dmsCanaryPassed"),
    ("ALL_BEDROCK_RESPONSES", CORPUS / "bedrock-production-certification" / "ALL_BEDROCK_RESPONSES_CERTIFIED.json", "allBedrockResponsesCertified"),
    ("CANONICAL_ASSEMBLY", CORPUS / "certified-canonical-inventory" / "ALL_CANONICAL_PRODUCTS_ASSEMBLED.json", "allCanonicalProductsAssembled"),
    ("UPLOAD_FILES", CORPUS / "certified-upload" / "UPLOAD_FILES_VERIFIED.json", "uploadFilesVerified"),
    ("DATABASE_UPLOAD", CORPUS / "certified-upload" / "DATABASE_UPLOAD_RECONCILED.json", "databaseUploadReconciled"),
    ("PUBLISHABLE_INVENTORY", CORPUS / "certified-upload" / "PUBLISHABLE_INVENTORY_PUBLISHED.json", "publishableInventoryPublished"),
    ("BRIEF_TO_PROPOSAL", CORPUS / "production-journey" / "BRIEF_TO_PROPOSAL_PASSED.json", "briefToProposalPassed"),
]

def main() -> int:
    gates = []
    highest = "NOT_STARTED"
    blocked = None
    for name, path, field in MARKERS:
        value = read_optional(path)
        passed = bool(value and value.get(field))
        gates.append({
            "gate": name,
            "passed": passed,
            "marker": str(path.relative_to(ROOT)),
            "details": value or {},
        })
        if passed and blocked is None:
            highest = name
        elif not passed and blocked is None:
            blocked = name
    ledger = read_optional(CORPUS / "bedrock-production-certification" / "usage-ledger.json") or {}
    physical = read_optional(CORPUS / "physical-certification-v2" / "corpus.json") or {}
    plan = read_optional(CORPUS / "bedrock-production-certification" / "plan.json") or {}
    status = {
        "schemaVersion": "advertified.inventory-production-status.v1",
        "highestCompletedGate": highest,
        "nextBlockedGate": blocked,
        "complete": all(item["passed"] for item in gates),
        "gates": gates,
        "physical": {
            "sourceCount": physical.get("sourceCount"),
            "certifiedCount": physical.get("certifiedCount"),
            "failedCount": physical.get("failedCount"),
            "physicalAnchorCount": physical.get("physicalAnchorCount"),
            "matchedPhysicalAnchorCount": physical.get("matchedPhysicalAnchorCount"),
            "transcriptionRowCount": physical.get("transcriptionRowCount"),
            "blockerCounts": physical.get("blockerCounts") or {},
        },
        "bedrock": {
            "programmeLimitUsdMicros": plan.get("programmeLimitUsdMicros", 5_000_000),
            "historicalUsageReserveUsdMicros": ledger.get("historicalUsageReserveUsdMicros", 188_122),
            "actualNewCostUsdMicros": ledger.get("actualNewCostUsdMicros", 0),
            "programmeUsedOrReservedUsdMicros": ledger.get("programmeUsedOrReservedUsdMicros", 188_122),
            "remainingProgrammeAllowanceUsdMicros": ledger.get("remainingProgrammeAllowanceUsdMicros", 4_811_878),
            "packetCountPlanned": plan.get("packetCount"),
            "ledgerEntryCount": len(ledger.get("entries") or []),
            "completedValidatedPacketCount": sum(
                item.get("status") == "COMPLETED" and bool(item.get("validationPassed"))
                for item in ledger.get("entries") or []
            ),
            "failedPacketCount": sum(
                not (item.get("status") == "COMPLETED" and bool(item.get("validationPassed")))
                for item in ledger.get("entries") or []
            ),
            "failureSummaries": [
                {
                    "packetId": item.get("packetId"),
                    "fileName": item.get("fileName"),
                    "status": item.get("status"),
                    "validationFailures": item.get("validationFailures") or [],
                    "actualCostUsdMicros": item.get("actualCostUsdMicros", 0),
                }
                for item in ledger.get("entries") or []
                if not (item.get("status") == "COMPLETED" and bool(item.get("validationPassed")))
            ][:50],
        },
    }
    OUTPUT.write_text(json.dumps(status, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(json.dumps(status, indent=2, sort_keys=True))
    return 0

def read_optional(path: Path) -> dict[str, Any] | None:
    if not path.exists():
        return None
    value = json.loads(path.read_text(encoding="utf-8"))
    return value if isinstance(value, dict) else None

if __name__ == "__main__":
    raise SystemExit(main())

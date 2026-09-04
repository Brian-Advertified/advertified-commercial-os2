"""Final fail-closed assertion for inventory certification through proposal use."""

from __future__ import annotations

import json
from datetime import UTC, datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CERT = ROOT / "artifacts" / "inventory-corpus" / "certification"
LIMIT = 5_000_000


def main() -> int:
    physical = read("physical-corpus-certification.json")
    bedrock = read("bedrock-corpus-evaluation.json")
    upload = read("inventory-upload-verification.json")
    proposal = read("brief-to-proposal-verification.json")
    status = read("INVENTORY_PROGRAM_STATUS.json")
    checks = {
        "physicalSourceCountIs43": physical.get("summary", {}).get("sourceCount") == 43,
        "physicalPassed43": physical.get("passed") is True
        and physical.get("summary", {}).get("passedSourceCount") == 43
        and physical.get("summary", {}).get("failedSourceCount") == 0,
        "physicalNoCoreEmpty": physical.get("summary", {}).get("coreEmptyCandidateCount") == 0,
        "physicalNoUngroundedRates": physical.get("summary", {}).get("unmatchedCandidateRateCount") == 0,
        "physicalNoDuplicates": physical.get("summary", {}).get("duplicateCandidateCount") == 0,
        "bedrockPassed43": bedrock.get("passed") is True
        and bedrock.get("sourceCount") == 43
        and bedrock.get("passedSourceCount") == 43,
        "bedrockDidNotChangeSourceFacts": bedrock.get("changedSourceFactCount") == 0,
        "bedrockDescriptionsComplete": bedrock.get("missingDescriptionCount") == 0,
        "bedrockCodesValid": bedrock.get("invalidSemanticCodeCount") == 0,
        "bedrockWithinUsd5": int(bedrock.get("committedCostUsdMicros") or 0)
        + int(bedrock.get("historicalReserveUsdMicros") or 0)
        <= LIMIT,
        "allSourcesUploaded": upload.get("passed") is True
        and upload.get("sourceCount") == 43
        and upload.get("uploadedCandidateCount") == upload.get("expectedCandidateCount"),
        "someCertifiedInventoryPublished": int(upload.get("publishedCandidateCount") or 0) > 0,
        "noApprovedRowsLeftUnpublished": upload.get("approvedNotPublishedCount") == 0,
        "noUnexplainedReviewRows": upload.get("unexplainedReviewCount") == 0,
        "briefToProposalPassed": proposal.get("passed") is True
        and int(proposal.get("publishedCorpusProductCodeCount") or 0) > 0
        and bool(proposal.get("matchedProductCodes") or proposal.get("matchedProductIds")),
        "programmeStatusPassed": status.get("passed") is True
        and status.get("nextIncompleteStage") is None,
    }
    failures = [name for name, passed in checks.items() if not passed]
    result = {
        "schemaVersion": "advertified.inventory-program-completion.v1",
        "verifiedAtUtc": datetime.now(UTC).isoformat(),
        "passed": not failures,
        "checks": checks,
        "failures": failures,
        "physical": physical.get("summary"),
        "bedrock": {
            "sourceCount": bedrock.get("sourceCount"),
            "candidateCount": bedrock.get("candidateCount"),
            "committedCostUsdMicros": bedrock.get("committedCostUsdMicros"),
            "historicalReserveUsdMicros": bedrock.get("historicalReserveUsdMicros"),
            "programmeLimitUsdMicros": LIMIT,
        },
        "upload": {
            "sourceCount": upload.get("sourceCount"),
            "uploadedCandidateCount": upload.get("uploadedCandidateCount"),
            "publishedCandidateCount": upload.get("publishedCandidateCount"),
            "reviewRequiredCandidateCount": upload.get("reviewRequiredCandidateCount"),
            "unexplainedReviewCount": upload.get("unexplainedReviewCount"),
        },
        "proposal": {
            "publishedCorpusProductCodeCount": proposal.get("publishedCorpusProductCodeCount"),
            "matchedProductCodes": proposal.get("matchedProductCodes"),
            "matchedProductIds": proposal.get("matchedProductIds"),
        },
    }
    output = CERT / "INVENTORY_PROGRAM_COMPLETION.json"
    output.write_text(json.dumps(result, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(json.dumps(result, indent=2))
    return 0 if not failures else 2


def read(name: str) -> dict:
    path = CERT / name
    if not path.exists():
        return {}
    value = json.loads(path.read_text(encoding="utf-8"))
    return value if isinstance(value, dict) else {}


if __name__ == "__main__":
    raise SystemExit(main())

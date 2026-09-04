"""Capture immutable candidate facts immediately before governed Bedrock enrichment."""

from __future__ import annotations

import hashlib
import json
from datetime import UTC, datetime
from pathlib import Path
from typing import Any

from inventory_corpus_api import InventoryApi

ROOT = Path(__file__).resolve().parents[1]
CORPUS = ROOT / "artifacts" / "inventory-corpus"
TENANT = "10000000-0000-0000-0000-000000000020"


def main() -> int:
    physical = read_json(CORPUS / "certification" / "physical-corpus-certification.json")
    if physical.get("passed") is not True:
        raise RuntimeError("Physical certification must pass 43/43 before baseline capture.")
    api = InventoryApi("http://127.0.0.1:5197", "http://localhost:3017", TENANT)
    api.start_session()
    preflight = api.request("GET", api.tenant_path("/inventory-semantic-preflight"))
    sources = sorted(preflight.get("sources") or [], key=lambda item: str(item.get("fileName") or "").lower())
    if len(sources) != 43:
        raise RuntimeError(f"Expected 43 sources, found {len(sources)}.")
    files = []
    for source in sources:
        view = api.read_complete_import(str(source["importId"]))
        candidates = [baseline_candidate(candidate) for candidate in view.get("candidates") or []]
        files.append({
            "sourceHash": source["sourceHash"],
            "fileName": source["fileName"],
            "importId": source["importId"],
            "importVersion": view.get("version"),
            "candidateCount": len(candidates),
            "candidates": candidates,
        })
    payload = {
        "schemaVersion": "advertified.inventory-pre-bedrock-baseline.v1",
        "capturedAtUtc": datetime.now(UTC).isoformat(),
        "sourceCount": len(files),
        "candidateCount": sum(item["candidateCount"] for item in files),
        "files": files,
    }
    canonical = json.dumps(payload, sort_keys=True, separators=(",", ":"))
    payload["baselineHash"] = hashlib.sha256(canonical.encode("utf-8")).hexdigest()
    path = CORPUS / "certification" / "pre-bedrock-candidate-baseline.json"
    path.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(json.dumps({
        "sourceCount": payload["sourceCount"],
        "candidateCount": payload["candidateCount"],
        "baselineHash": payload["baselineHash"],
        "output": str(path.relative_to(ROOT)),
    }, indent=2))
    return 0


def baseline_candidate(candidate: dict[str, Any]) -> dict[str, Any]:
    values = candidate.get("canonicalValues") or candidate.get("proposedValues") or candidate.get("values") or {}
    evidence = candidate.get("evidence") or []
    return {
        "candidateId": candidate.get("id"),
        "rowNumber": candidate.get("rowNumber"),
        "sourceLocator": candidate.get("sourceLocator"),
        "sourceFacts": {
            "productCode": values.get("productCode"),
            "name": values.get("name"),
            "geography": values.get("geography"),
            "address": values.get("address"),
            "latitude": values.get("latitude"),
            "longitude": values.get("longitude"),
            "rateType": values.get("rateType"),
            "currency": values.get("currency"),
            "rateAmountMinor": values.get("rateAmountMinor"),
            "availability": values.get("availability"),
            "supplierCommercial": values.get("supplierCommercial"),
            "supplierContacts": values.get("supplierContacts") or [],
            "commercialTerms": values.get("commercialTerms"),
            "deliverable": values.get("deliverable"),
            "spatial": values.get("spatial"),
            "package": values.get("package"),
            "extension": source_extension(values.get("extension") or {}),
            "supplierEvidence": [
                evidence_fact(item)
                for item in evidence
                if normalize(str(item.get("fieldName") or "")) in {"supplier", "suppliername"}
            ],
            "rateEvidence": [
                evidence_fact(item)
                for item in evidence
                if normalize(str(item.get("fieldName") or "")) == "rate"
            ],
        },
        "semanticFacts": {
            "channel": values.get("channel"),
            "productType": values.get("productType"),
            "description": values.get("description"),
        },
    }


def source_extension(extension: dict[str, Any]) -> dict[str, Any]:
    semantic_keys = {
        "semanticdescription", "semantickeywords", "semanticmodel",
        "semanticpromptversion", "semanticconfidence", "embeddingtext",
    }
    return {
        key: value
        for key, value in extension.items()
        if normalize(str(key)) not in semantic_keys
    }


def evidence_fact(item: dict[str, Any]) -> dict[str, Any]:
    return {
        "fieldName": item.get("fieldName"),
        "rawValue": item.get("rawValue"),
        "normalizedValue": item.get("normalizedValue"),
        "sourceLocator": item.get("sourceLocator"),
        "sourceHash": item.get("sourceHash"),
        "evidenceBasis": item.get("evidenceBasis"),
    }


def normalize(value: str) -> str:
    return "".join(character.lower() for character in value if character.isalnum())


def read_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.resolve(strict=True).read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError(f"Expected an object in {path}.")
    return value


if __name__ == "__main__":
    raise SystemExit(main())

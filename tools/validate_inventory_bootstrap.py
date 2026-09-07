"""Validate a manually transcribed inventory bootstrap without reading inventory values from sources."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path


def checksum(value: object) -> str:
    encoded = json.dumps(value, ensure_ascii=False, sort_keys=True,
                         separators=(",", ":")).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


def issue(errors: list[str], condition: bool, message: str) -> None:
    if not condition:
        errors.append(message)


def validate(seed: dict, source_dir: Path | None) -> list[str]:
    errors: list[str] = []
    issue(errors, seed.get("schemaVersion") == "advertified.production-inventory-bootstrap.v1",
          "unsupported schemaVersion")
    issue(errors, bool(seed.get("seedVersion")), "seedVersion is required")
    sources = seed.get("sources", [])
    source_by_name = {item.get("file"): item for item in sources}
    suppliers = {item.get("key") for item in seed.get("suppliers", [])}
    documents = seed.get("documents", [])
    document_by_key = {item.get("key"): item for item in documents}
    records = seed.get("records", [])
    pricing_schedules = seed.get("pricingSchedules", [])
    planning_evidence = seed.get("planningEvidence", [])
    issue(errors, len(sources) == len({item.get("file") for item in sources}),
          "duplicate source filename")
    issue(errors, len(records) == len({item.get("key") for item in records}),
          "duplicate inventory key")
    issue(errors, len(documents) == len(document_by_key), "duplicate document key")
    schedule_cells = {
        (item.get("documentKey"), item.get("schedule"), item.get("code"),
         item.get("durationSeconds")) for item in pricing_schedules
    }
    issue(errors, len(pricing_schedules) == len(schedule_cells),
          "duplicate pricing schedule cell")
    for source in sources:
        file_name, digest = source.get("file"), source.get("sha256")
        issue(errors, bool(file_name), "source filename is required")
        issue(errors, isinstance(digest, str) and len(digest) == 64,
              f"invalid checksum for {file_name}")
        issue(errors, source.get("review") in {"pending", "text_reviewed", "complete"},
              f"invalid review state for {file_name}")
        if source_dir and file_name:
            path = source_dir / file_name
            issue(errors, path.is_file(), f"source missing: {file_name}")
            if path.is_file():
                actual = hashlib.sha256(path.read_bytes()).hexdigest()
                issue(errors, digest == actual, f"source checksum mismatch: {file_name}")
                issue(errors, source.get("sizeBytes") == path.stat().st_size,
                      f"source size mismatch: {file_name}")
    for document in documents:
        key, file_name = document.get("key"), document.get("file")
        issue(errors, bool(key), "document key is required")
        issue(errors, file_name in source_by_name, f"unknown document source for {key}")
        issue(errors, document.get("supplierKey") in suppliers,
              f"unknown document supplier for {key}")
        issue(errors, bool(document.get("title")), f"document title is required for {key}")
        issue(errors, bool(document.get("channels")), f"document channels are required for {key}")
        expected = source_by_name.get(file_name, {}).get("sha256")
        issue(errors, document.get("sourceVersion") == f"sha256:{expected}",
              f"document sourceVersion mismatch for {key}")
    complete_files = {item.get("file") for item in sources if item.get("review") == "complete"}
    documented_files = {item.get("file") for item in documents}
    issue(errors, complete_files <= documented_files,
          "every fully reviewed source requires document metadata")
    for record in records:
        key = record.get("key")
        issue(errors, bool(key), "inventory key is required")
        issue(errors, record.get("supplierKey") in suppliers,
              f"unknown supplier for {key}")
        document = document_by_key.get(record.get("documentKey"))
        issue(errors, document is not None, f"unknown document for {key}")
        issue(errors, document is None or document.get("supplierKey") == record.get("supplierKey"),
              f"document and record supplier mismatch for {key}")
        issue(errors, bool(record.get("name")), f"name is required for {key}")
        issue(errors, not record.get("publicationEligible") or
              len(str(record.get("name", "")).strip()) >= 5,
              f"publishable name must identify the offer for {key}")
        issue(errors, bool(record.get("channel")) and bool(record.get("productType")),
              f"channel and productType are required for {key}")
        issue(errors, bool(record.get("format")) and bool(record.get("placement")),
              f"format and placement are required for {key}")
        rates = record.get("rates") or ([record["rate"]] if record.get("rate") else [])
        issue(errors, bool(rates), f"at least one price is required for {key}")
        for rate_index, rate in enumerate(rates, 1):
            prefix = f"rate {rate_index} for {key}"
            issue(errors, bool(rate.get("raw")), f"raw price is required for {prefix}")
            if record.get("publicationEligible"):
                issue(errors, isinstance(rate.get("amountMinor"), int)
                      and rate["amountMinor"] >= 0,
                      f"publishable amountMinor must be a non-negative integer for {prefix}")
                issue(errors, bool(rate.get("currency")) and bool(rate.get("rateType"))
                      and bool(rate.get("buyingUnit")),
                      f"publishable currency, rateType and buyingUnit are required for {prefix}")
        package = record.get("package")
        if package:
            issue(errors, bool(package.get("name")) and bool(package.get("components")),
                  f"package name and components are required for {key}")
            for component in package.get("components", []):
                issue(errors, bool(component.get("name")) and component.get("quantity") is not None
                      and bool(component.get("unit")),
                      f"package component name, quantity and unit are required for {key}")
        provenance = record.get("provenance") or {}
        issue(errors, provenance.get("file") in {item.get("file") for item in sources},
              f"unknown provenance source for {key}")
        locator = any((isinstance(provenance.get("page"), int),
                       isinstance(provenance.get("slide"), int),
                       bool(provenance.get("worksheet"))))
        issue(errors, bool(provenance.get("raw")) and locator,
              f"precise provenance is required for {key}")
        issue(errors, not record.get("publicationEligible") or not record.get("issue"),
              f"unresolved record cannot be publication eligible: {key}")
        issue(errors, record.get("publicationEligible") or bool(record.get("issue")),
              f"review-only record requires a specific issue: {key}")
        source = source_by_name.get(provenance.get("file"), {})
        issue(errors, document is None or provenance.get("file") == document.get("file"),
              f"document and provenance source mismatch for {key}")
        issue(errors, not record.get("publicationEligible") or source.get("review") == "complete",
              f"published record must have a fully reviewed source: {key}")
    for cell in pricing_schedules:
        label = (f"{cell.get('schedule')} {cell.get('code')} "
                 f"{cell.get('durationSeconds')}s")
        issue(errors, cell.get("documentKey") in document_by_key,
              f"unknown pricing schedule document for {label}")
        issue(errors, bool(cell.get("schedule")) and bool(cell.get("code")),
              f"pricing schedule and code are required for {label}")
        issue(errors, isinstance(cell.get("durationSeconds"), int) and
              cell["durationSeconds"] > 0,
              f"positive duration is required for {label}")
        issue(errors, isinstance(cell.get("amountMinor"), int) and
              cell["amountMinor"] >= 0 and bool(cell.get("raw")) and
              bool(cell.get("currency")),
              f"complete rate evidence is required for {label}")
        issue(errors, bool(cell.get("provenance")),
              f"pricing schedule provenance is required for {label}")
    for evidence in planning_evidence:
        issue(errors, evidence.get("documentKey") in document_by_key,
              "unknown planning-evidence document")
        issue(errors, bool(evidence.get("name")) and bool(evidence.get("provenance"))
              and isinstance(evidence.get("facts"), dict),
              "planning evidence requires a name, provenance and facts")
    if seed.get("manualCertificationStatus") == "COMPLETE":
        issue(errors, all(item.get("review") == "complete" for item in sources),
              "complete certification requires every source to be fully reviewed")
    if source_dir:
        actual_files = {path.name for path in source_dir.rglob("*") if path.is_file()}
        declared_files = {item.get("file") for item in sources}
        issue(errors, actual_files == declared_files,
              "source manifest does not exactly cover the source directory")
    return errors


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("seed", type=Path)
    parser.add_argument("--source-dir", type=Path)
    args = parser.parse_args()
    seed = json.loads(args.seed.read_text(encoding="utf-8"))
    errors = validate(seed, args.source_dir)
    print(json.dumps({"seedChecksum": checksum(seed), "records": len(seed.get("records", [])),
                      "sources": len(seed.get("sources", [])), "errors": errors}, indent=2))
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())

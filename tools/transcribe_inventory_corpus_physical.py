"""Generate deterministic physical inventory rows for all 43 corpus files."""

from __future__ import annotations

import json
from datetime import UTC, datetime
from pathlib import Path
from typing import Any

from inventory_physical_transcriber import transcribe_document

ROOT = Path(__file__).resolve().parents[1]
CORPUS = ROOT / "artifacts" / "inventory-corpus"
MANIFEST_PATH = CORPUS / "source-manifest.json"
SOURCE_MAP_ROOT = CORPUS / "semantic-v1"
OUTPUT_ROOT = CORPUS / "physical-transcription"


def main() -> int:
    manifest = read_json(MANIFEST_PATH)
    OUTPUT_ROOT.mkdir(parents=True, exist_ok=True)
    documents = []
    for document in manifest.get("documents", []):
        source_hash = str(document["sha256"])
        source_map = read_json(SOURCE_MAP_ROOT / f"{source_hash}.json")
        transcription = transcribe_document(document, source_map)
        write_json(OUTPUT_ROOT / f"{source_hash}.json", transcription)
        documents.append({
            "sourceHash": source_hash,
            "fileName": transcription["fileName"],
            "documentClass": transcription["documentClass"],
            "supplierName": transcription["supplierName"],
            "channelHint": transcription["channelHint"],
            "rowCount": transcription["rowCount"],
        })
    register = {
        "schemaVersion": "advertified.inventory-physical-transcription-register.v1",
        "generatedAtUtc": datetime.now(UTC).isoformat(),
        "datasetVersion": manifest.get("datasetVersion"),
        "sourceCount": len(documents),
        "rowCount": sum(item["rowCount"] for item in documents),
        "documents": documents,
    }
    write_json(OUTPUT_ROOT / "register.json", register)
    print(json.dumps({
        "sourceCount": register["sourceCount"],
        "rowCount": register["rowCount"],
        "documentsWithZeroRows": [
            item["fileName"] for item in documents if item["rowCount"] == 0
        ],
        "output": str((OUTPUT_ROOT / "register.json").relative_to(ROOT)),
    }, indent=2))
    return 0 if len(documents) == 43 and all(item["rowCount"] > 0 for item in documents) else 2


def read_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.resolve(strict=True).read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError(f"Expected object in {path}")
    return value


def write_json(path: Path, value: dict[str, Any]) -> None:
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8")


if __name__ == "__main__":
    raise SystemExit(main())

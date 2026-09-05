"""Audit retained independent source maps and bind them to immutable sources."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--source-maps", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    manifest = json.loads(args.manifest.read_text(encoding="utf-8"))
    records: list[dict[str, Any]] = []
    for source in manifest["files"]:
        source_hash = source["content_hash"]
        path = args.source_maps / f"{source_hash}.json"
        payload = json.loads(path.read_text(encoding="utf-8"))
        if payload.get("sourceHash") != source_hash:
            raise ValueError(f"Source-map hash identity mismatch: {path}")
        records.append({
            "relative_path": source["relative_path"],
            "source_hash": source_hash,
            "source_map_path": str(path.resolve()),
            "source_map_sha256": sha256(path),
            "schema_version": payload.get("schemaVersion"),
            "extractor_version": payload.get("extractorVersion"),
            "counts": payload.get("counts"),
        })
    payload = {
        "schema_version": "advertified.inventory-source-map-binding/1.0",
        "source_manifest_sha256": sha256(args.manifest),
        "source_map_count": len(records),
        "sources": records,
    }
    args.output.write_text(
        json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8"
    )
    print(json.dumps({"source_map_count": len(records)}, indent=2))


if __name__ == "__main__":
    main()

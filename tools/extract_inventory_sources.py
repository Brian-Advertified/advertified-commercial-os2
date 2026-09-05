"""Create immutable semantic source maps for governed inventory recovery."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
from typing import Any

from inventory_source_readers import read_source

SCHEMA_VERSION = "advertified.inventory-source-map.v2"
EXTRACTOR_VERSION = "advertified-source-reader/2.0.0"
REPO_ROOT = Path(__file__).resolve().parents[1]


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument(
        "manifest", type=Path,
    )
    parser.add_argument(
        "--evidence", type=Path,
        required=True,
    )
    parser.add_argument("--document", action="append", default=[])
    parser.add_argument(
        "--maximum-new", type=int,
        help="Bound new maps per resumable operator run.",
    )
    parser.add_argument(
        "--ocr-container",
        default="advertified-os2-dev-docling-1",
    )
    args = parser.parse_args()
    if (args.maximum_new is not None and
            args.maximum_new < 1):
        raise ValueError(
            "Maximum new maps must be positive."
        )
    manifest_path = args.manifest.resolve(strict=True)
    manifest = json.loads(
        manifest_path.read_text(encoding="utf-8")
    )
    recorded_source = manifest.get("sourceRoot")
    if args.source is None and not recorded_source:
        raise ValueError(
            "The corpus source path is not configured."
        )
    source_root = (
        args.source or Path(recorded_source)
    ).resolve(strict=True)
    requested = set(args.document)
    selected = [
        item for item in manifest["documents"]
        if not requested or item["relativePath"] in requested
    ]
    missing = requested.difference(
        item["relativePath"] for item in selected
    )
    if missing:
        raise ValueError(
            f"Unknown source documents: {sorted(missing)}"
        )
    output_root = args.evidence.resolve() / "semantic-v1"
    output_root.mkdir(parents=True, exist_ok=True)
    new_results = 0
    for index, item in enumerate(selected, start=1):
        source = (
            source_root / item["relativePath"]
        ).resolve(strict=True)
        if (source_root not in source.parents or
                sha256(source) != item["sha256"]):
            raise ValueError(
                f"Source identity mismatch: {item['relativePath']}"
            )
        destination = output_root / f"{item['sha256']}.json"
        if cached_result(destination, item["sha256"]):
            print(json.dumps({
                "position": index,
                "total": len(selected),
                "source": item["relativePath"],
                "state": "cached",
            }), flush=True)
            continue
        if (args.maximum_new is not None and
                new_results >= args.maximum_new):
            break
        extracted = read_source(source, args.ocr_container)
        payload = {
            "schemaVersion": SCHEMA_VERSION,
            "extractorVersion": EXTRACTOR_VERSION,
            "sourceHash": item["sha256"],
            "relativePath": item["relativePath"],
            **extracted,
        }
        write_json(destination, payload)
        new_results += 1
        print(json.dumps({
            "position": index,
            "total": len(selected),
            "source": item["relativePath"],
            **extracted["counts"],
        }), flush=True)
    return 0


def cached_result(path: Path, source_hash: str) -> bool:
    if not path.exists():
        return False
    try:
        payload = json.loads(
            path.read_text(encoding="utf-8")
        )
    except (OSError, json.JSONDecodeError):
        return False
    return (
        payload.get("schemaVersion") == SCHEMA_VERSION
        and payload.get("extractorVersion") == EXTRACTOR_VERSION
        and payload.get("sourceHash") == source_hash
    )


def write_json(path: Path, payload: dict[str, Any]) -> None:
    temporary = path.with_suffix(path.suffix + ".tmp")
    try:
        temporary.write_text(
            json.dumps(payload, indent=2, sort_keys=True) + "\n",
            encoding="utf-8",
        )
        os.replace(temporary, path)
    finally:
        temporary.unlink(missing_ok=True)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(
                lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


if __name__ == "__main__":
    raise SystemExit(main())

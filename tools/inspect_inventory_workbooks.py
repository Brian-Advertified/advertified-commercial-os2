"""Create hash-bound, read-only structural evidence for inventory XLSX sources."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import zipfile
from datetime import datetime, timezone
from pathlib import Path
from xml.etree import ElementTree as ET


MAIN = "http://schemas.openxmlformats.org/spreadsheetml/2006/main"
REL = "http://schemas.openxmlformats.org/officeDocument/2006/relationships"
PKG_REL = "http://schemas.openxmlformats.org/package/2006/relationships"
NS = {"m": MAIN, "r": REL, "p": PKG_REL}


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def xml(root: zipfile.ZipFile, name: str) -> ET.Element:
    return ET.fromstring(root.read(name))


def shared_strings(root: zipfile.ZipFile) -> list[str]:
    if "xl/sharedStrings.xml" not in root.namelist():
        return []
    tree = xml(root, "xl/sharedStrings.xml")
    return ["".join(node.itertext()) for node in tree.findall("m:si", NS)]


def workbook_sheets(root: zipfile.ZipFile) -> list[dict]:
    book = xml(root, "xl/workbook.xml")
    rels = xml(root, "xl/_rels/workbook.xml.rels")
    targets = {
        item.attrib["Id"]: item.attrib["Target"]
        for item in rels.findall("p:Relationship", NS)
    }
    result = []
    for item in book.findall("m:sheets/m:sheet", NS):
        target = targets[item.attrib[f"{{{REL}}}id"]].replace("\\", "/")
        if target.startswith("/"):
            target = target[1:]
        elif not target.startswith("xl/"):
            target = f"xl/{target}"
        result.append({
            "name": item.attrib["name"],
            "state": item.attrib.get("state", "visible"),
            "path": target,
        })
    return result


def cell_value(cell: ET.Element, strings: list[str]) -> dict:
    kind = cell.attrib.get("t")
    value = cell.findtext("m:v", default=None, namespaces=NS)
    inline = cell.find("m:is", NS)
    formula = cell.findtext("m:f", default=None, namespaces=NS)
    display = value
    if kind == "s" and value is not None:
        index = int(value)
        display = strings[index] if index < len(strings) else None
    elif kind == "inlineStr" and inline is not None:
        display = "".join(inline.itertext())
    return {
        "cell": cell.attrib.get("r"),
        "type": kind,
        "style_index": int(cell.attrib["s"]) if "s" in cell.attrib else None,
        "formula": formula,
        "cached_value": value if formula is not None else None,
        "value": display,
    }


def worksheet_evidence(root: zipfile.ZipFile, sheet: dict, strings: list[str]) -> dict:
    tree = xml(root, sheet["path"])
    cells = [cell_value(cell, strings) for cell in tree.findall(".//m:sheetData/m:row/m:c", NS)]
    cells = [cell for cell in cells if cell["value"] is not None or cell["formula"] is not None]
    hidden_rows = [
        int(row.attrib["r"])
        for row in tree.findall(".//m:sheetData/m:row", NS)
        if row.attrib.get("hidden") == "1"
    ]
    hidden_columns = []
    for column in tree.findall("m:cols/m:col", NS):
        if column.attrib.get("hidden") == "1":
            hidden_columns.append({"min": int(column.attrib["min"]), "max": int(column.attrib["max"])})
    merges = [item.attrib["ref"] for item in tree.findall("m:mergeCells/m:mergeCell", NS)]
    drawings = [item.attrib.get(f"{{{REL}}}id") for item in tree.findall("m:drawing", NS)]
    return {
        **sheet,
        "dimension": tree.find("m:dimension", NS).attrib.get("ref") if tree.find("m:dimension", NS) is not None else None,
        "populated_cell_count": len(cells),
        "formula_cell_count": sum(cell["formula"] is not None for cell in cells),
        "hidden_rows": hidden_rows,
        "hidden_columns": hidden_columns,
        "merged_ranges": merges,
        "drawing_relationship_ids": drawings,
        "cells": cells,
    }


def inspect(entry: dict, media_dir: Path | None) -> dict:
    source = Path(entry["absolute_path"])
    actual_hash = sha256(source)
    if actual_hash != entry["content_hash"]:
        raise ValueError(f"Hash mismatch for {source.name}")
    with zipfile.ZipFile(source) as root:
        strings = shared_strings(root)
        sheets = [worksheet_evidence(root, sheet, strings) for sheet in workbook_sheets(root)]
        media = []
        for name in sorted(item for item in root.namelist() if item.startswith("xl/media/") and not item.endswith("/")):
            payload = root.read(name)
            media_hash = hashlib.sha256(payload).hexdigest()
            evidence_path = None
            if media_dir is not None:
                media_dir.mkdir(parents=True, exist_ok=True)
                suffix = Path(name).suffix.lower()
                destination = media_dir / f"{actual_hash}-{Path(name).stem}-{media_hash[:16]}{suffix}"
                destination.write_bytes(payload)
                evidence_path = str(destination.resolve())
            media.append({
                "path": name,
                "byte_size": len(payload),
                "sha256": media_hash,
                "evidence_path": evidence_path,
            })
        return {
            "source_id": entry["source_id"],
            "file_name": source.name,
            "source_hash": actual_hash,
            "sheets": sheets,
            "embedded_media": media,
        }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--media-dir", type=Path)
    args = parser.parse_args()
    manifest = json.loads(args.manifest.read_text(encoding="utf-8"))
    workbooks = [
        inspect(entry, args.media_dir)
        for entry in manifest["files"]
        if entry["format"] == "excel"
    ]
    report = {
        "schema_version": "advertified.inventory-workbook-structure.v1",
        "generated_at_utc": datetime.now(timezone.utc).isoformat(),
        "method": "Read-only Open XML inspection; values preserve formula/cache distinction.",
        "workbooks": workbooks,
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
    for workbook in workbooks:
        print(workbook["file_name"])
        for sheet in workbook["sheets"]:
            print(
                f"  {sheet['name']}: {sheet['dimension']}, {sheet['populated_cell_count']} cells, "
                f"{sheet['formula_cell_count']} formulas, {len(sheet['hidden_rows'])} hidden rows"
            )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

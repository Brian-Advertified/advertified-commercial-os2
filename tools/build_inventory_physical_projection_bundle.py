"""Build the immutable physical projection bundle for the 43-file corpus."""

from __future__ import annotations

import gzip
import hashlib
import json
import re
from dataclasses import asdict
from datetime import UTC, datetime
from pathlib import Path
from typing import Any

from inventory_physical_evidence import read_physical_document
from inventory_physical_model import PhysicalUnit, extract_money, normalize

REPO_ROOT = Path(__file__).resolve().parents[1]
CORPUS_ROOT = REPO_ROOT / "artifacts" / "inventory-corpus"
OUTPUT = (
    REPO_ROOT
    / "api"
    / "src"
    / "Advertified.Commercial.Infrastructure"
    / "Inventory"
    / "CorpusPhysicalProjection"
    / "corpus-physical-projection.v1.json.gz"
)

SUPPLIERS = {
    "algoa": "Algoa FM",
    "arena": "Arena Holdings",
    "blackspace": "BlackSpace",
    "business day tv": "Arena Holdings",
    "digital rates & packages": "Primedia Broadcasting",
    "soweto screens": "Soweto Screens",
    "direct kaya": "Kaya 959",
    "dms digital": "DStv Media Sales",
    "eleven8": "eleven8",
    "emedia": "eMedia",
    "ignition tv": "Ignition TV",
    "insight outdoor": "Insight Outdoor",
    "jac rate": "Jacaranda FM",
    "jcdecaux": "JCDecaux",
    "jit tv": "Jit TV",
    "jozi fm": "Jozi FM",
    "kena outdoor": "Kena Outdoor",
    "mamg": "MAMG",
    "media deck 2026": "Volt.Africa",
    "primedia broadcasting": "Primedia Broadcasting",
    "primedia outdoor": "Primedia Outdoor",
    "relativ media": "Relativ Media",
    "reveel": "Reveel",
    "rsd rate cards": "Roadside Digital",
    "sabc": "SABC",
    "sb outdoor": "SB Outdoor",
    "smile 90.4": "Smile 90.4FM",
    "summit ooh": "Summit OOH Media",
    "home channel": "The Home Channel",
    "virgin active": "Virgin Active",
    "y packages": "YFM",
}


def main() -> int:
    manifest = read_json(CORPUS_ROOT / "source-manifest.json")
    documents = manifest.get("documents") or []
    if len(documents) != 43:
        raise RuntimeError(f"Expected 43 sources, found {len(documents)}.")
    sources = []
    for document in documents:
        source_hash = str(document["sha256"])
        physical = read_physical_document(
            CORPUS_ROOT / "semantic-v1" / f"{source_hash}.json"
        )
        supplier = supplier_for(physical.file_name)
        units = [
            projection_unit(
                source_hash,
                physical.file_name,
                supplier,
                unit,
                index,
            )
            for index, unit in enumerate(physical.units, start=1)
        ]
        sources.append({
            "sourceHash": source_hash,
            "fileName": physical.file_name,
            "format": physical.document_format,
            "supplier": supplier,
            "sourceMapHash": sha256_file(
                CORPUS_ROOT / "semantic-v1" / f"{source_hash}.json"
            ),
            "unitCount": len(units),
            "units": units,
        })
    body = {
        "schemaVersion": "advertified.corpus-physical-projection.v1",
        "generatedAtUtc": datetime.now(UTC).isoformat(),
        "datasetVersion": manifest.get("datasetVersion"),
        "sourceCount": len(sources),
        "unitCount": sum(source["unitCount"] for source in sources),
        "sources": sources,
    }
    canonical = json.dumps(
        body,
        separators=(",", ":"),
        sort_keys=True,
    ).encode("utf-8")
    envelope = {
        "payloadSha256": hashlib.sha256(canonical).hexdigest(),
        "payload": body,
    }
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    with gzip.open(OUTPUT, "wt", encoding="utf-8", compresslevel=9) as handle:
        json.dump(envelope, handle, separators=(",", ":"), sort_keys=True)
    summary = {
        "sourceCount": body["sourceCount"],
        "unitCount": body["unitCount"],
        "sourcesWithNoUnits": [
            source["fileName"]
            for source in sources
            if source["unitCount"] == 0
        ],
        "payloadSha256": envelope["payloadSha256"],
        "output": str(OUTPUT.relative_to(REPO_ROOT)),
    }
    (CORPUS_ROOT / "physical-certification" / "projection-bundle-summary.json").write_text(
        json.dumps(summary, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    print(json.dumps(summary, indent=2))
    return 0 if not summary["sourcesWithNoUnits"] else 2


def projection_unit(
    source_hash: str,
    file_name: str,
    supplier: str,
    unit: PhysicalUnit,
    index: int,
) -> dict[str, Any]:
    channel = channel_for(file_name, unit)
    values: dict[str, str] = {
        "availability": "PLANNING_AVAILABLE",
        "channel": channel,
        "description": description_for(unit),
        "geography": geography_for(file_name, unit),
        "name": clean_identity(unit.identity),
        "productcode": f"CORP-{source_hash[:8].upper()}-{index:05d}",
        "producttype": product_type_for(channel, unit),
        "supplier": supplier,
    }
    derived_fields = {
        "availability",
        "channel",
        "geography",
        "productcode",
        "producttype",
        "supplier",
    }
    if unit.raw_rate:
        values["rate"] = unit.raw_rate
        values["currency"] = "ZAR"
        rate_type = rate_type_for(file_name, unit)
        if rate_type:
            values["ratetype"] = rate_type
            derived_fields.add("ratetype")
    else:
        values["currency"] = "ZAR"
        values["rateavailability"] = "RATE_ON_REQUEST"
        derived_fields.update({"currency", "rateavailability"})
    return {
        "number": index,
        "locator": unit.locator,
        "scope": unit.scope,
        "kind": unit.kind,
        "values": values,
        "physicalFields": sorted(set(values) - derived_fields),
        "derivedFields": sorted(derived_fields),
        "evidence": list(unit.evidence),
    }


def supplier_for(file_name: str) -> str:
    key = file_name.casefold()
    for fragment, supplier in SUPPLIERS.items():
        if fragment in key:
            return supplier
    raise RuntimeError(f"Supplier mapping is missing for {file_name}.")


def channel_for(file_name: str, unit: PhysicalUnit) -> str:
    combined = normalize(
        " ".join((file_name, unit.identity, *unit.evidence[:8]))
    )
    file_key = file_name.casefold()
    if unit.kind == "RADIO_RATE" or " fm" in f" {file_key}" or "radio" in file_key:
        return "RADIO"
    if any(token in file_key for token in (
        "tv", "emedia", "home channel", "sabc may",
    )):
        return "TV"
    if any(token in combined for token in (
        " billboard ", " ooh ", "screen", "site number", "roadside",
        "mall", "highway", "taxi rank",
    )) or any(token in file_key for token in (
        "outdoor", "reveel", "rsd", "jcdecaux", "relativ",
        "virgin active", "soweto screens", "jit tv",
    )):
        return "DOOH" if any(
            token in combined for token in ("digital", "screen", "programmatic")
        ) else "OOH"
    if any(token in combined for token in (
        "banner", "video", "social", "website", "digital", "youtube",
        "facebook", "instagram", "tiktok", "pre roll", "cpm",
    )) or "media deck" in file_key or "digital rates" in file_key or "dms" in file_key:
        return "DIGITAL"
    if any(token in file_key for token in (
        "arena-", "mamg", "business day rate",
    )) or any(token in combined for token in (
        "full page", "half page", "print", "psccm", "newspaper",
    )):
        return "PRINT"
    return "DIGITAL"


def product_type_for(channel: str, unit: PhysicalUnit) -> str:
    return {
        "RADIO": "RADIO_SPOT",
        "TV": "TV_SPOT",
        "PRINT": "PRINT_PLACEMENT",
        "OOH": "OOH_SITE",
        "DOOH": "DOOH_SCREEN",
        "DIGITAL": "DIGITAL_PLACEMENT",
    }[channel]


def rate_type_for(file_name: str, unit: PhysicalUnit) -> str | None:
    combined = normalize(
        " ".join((file_name, unit.identity, *unit.evidence[:8]))
    )
    if unit.kind == "RADIO_RATE" or " spot" in f" {combined}":
        return "SPOT_RATE"
    if " cpm" in f" {combined}" or "per thousand" in combined:
        return "CPM"
    if "per day" in combined or "24 hour" in combined:
        return "DAY_RATE"
    if "per week" in combined or "weekly" in combined:
        return "WEEK_RATE"
    if "per month" in combined or "monthly" in combined:
        return "MONTH_RATE"
    if "package" in combined or " plan a" in f" {combined}":
        return "PACKAGE_RATE"
    if any(token in combined for token in (
        "per post", "per insertion", "full page", "half page", "psccm",
    )):
        return "FLAT_RATE"
    return None


def geography_for(file_name: str, unit: PhysicalUnit) -> str:
    combined = " | ".join((unit.identity, *unit.evidence[:6]))
    provinces = [
        province for province in (
            "Gauteng", "Western Cape", "KwaZulu-Natal", "Eastern Cape",
            "Limpopo", "Mpumalanga", "North West", "Free State",
            "Northern Cape",
        ) if province.casefold() in combined.casefold()
    ]
    return " | ".join(provinces) if provinces else "South Africa"


def description_for(unit: PhysicalUnit) -> str:
    values = [clean_identity(unit.identity)]
    values.extend(
        " ".join(value.split()) for value in unit.evidence if value.strip()
    )
    unique = list(dict.fromkeys(value for value in values if value))
    return " | ".join(unique)[:2_000]


def clean_identity(value: str) -> str:
    return " | ".join(
        " ".join(part.split())
        for part in re.split(r"[|\n]+", value)
        if part.strip()
    )[:500]


def sha256_file(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.resolve(strict=True).read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError(f"Expected an object in {path}.")
    return value


if __name__ == "__main__":
    raise SystemExit(main())

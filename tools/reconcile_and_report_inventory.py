"""Build the source-led inventory verification checkpoint.

This tool deliberately does not extract commercial data from application output.
It binds human-reviewed evidence to the current source manifest and leaves real
application reconciliation blocked until a canonical projection is supplied.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from inventory_reference_jozi import jozi_entries
from inventory_reference_digital_rates import digital_rates_entries
from inventory_reference_kena import kena_digital_entries, kena_static_entries
from inventory_reference_mediamark import jacaranda_entries, mediamark_entries
from inventory_reference_reviews import VISUAL_REVIEWS
from inventory_reference_rsd import rsd_gauteng_entries, rsd_western_cape_entries
from inventory_reference_reveel import reveel_entries
from inventory_reference_jit import jit_tv_entries
from inventory_reference_commvibe import commvibe_demographic_entries
from inventory_reference_dense_documents import dense_reference_entries
from inventory_evidence_binding import verification_binding
from inventory_reference_sb import sb_outdoor_entries
from inventory_reference_summit import summit_billboard_entries, summit_main_market_entries
from inventory_reference_small_docs import direct_kaya_entries, home_channel_entries, ignition_entries, mamg_entries, smile_entries, soweto_screens_entries, virgin_active_entries, y_entries


SCHEMA_VERSION = "advertified.inventory-physical-reference-ledger/1.0"
def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()
def entry(source_hash: str, locator: str, identity: str, **fields: Any) -> dict[str, Any]:
    return {
        "entry_id": f"{source_hash[:16]}:{locator}:{identity}",
        "source_hash": source_hash,
        "locator": locator,
        "identity": identity,
        "expected_fields": fields,
        "application_reconciliation": "NOT_RUN_CURRENT_PROJECTION",
    }
def algoa_entries() -> list[dict[str, Any]]:
    sponsorship = "332d2f11ccb1d95e6544bdad7ebcf12a95a387d84d4d3efe21cc7f5766b06143"
    generic = "8331bddefe98103e4299f4c5a9125e8885d7d59cfcb98cb6310e03b92f9b40b7"
    preroll = "a505e8e95caa0b5bd232963de17e49c5f1a17a42351a2296741e7b67d49b6ddc"
    shared = {
        "currency": "ZAR", "vat": "EXCLUDED_FROM_STATED_NET_VALUES",
        "validity": "2026 package; source terms apply",
    }
    return [
        entry(sponsorship, "pdf:page=1", "Generic 30 second component",
              **shared, gross_value="291060", net_investment="145530",
              quantity="114 spots", schedule="Monday-Sunday"),
        entry(sponsorship, "pdf:page=1", "Report sponsorship component",
              **shared, gross_value="157500", net_investment="78750",
              quantity="30 exposures"),
        entry(sponsorship, "pdf:page=1;pages=2-3", "Plan A generic and sponsorship package",
              **shared, gross_value="448560", net_investment="224280",
              billing_period="6 consecutive months", monthly_cost="37380",
              relationship="contains the generic and report-sponsorship components",
              restrictions="limited to 15 packages; no changes or cancellations"),
        entry(generic, "pdf:page=1", "Generic 30 second component",
              **shared, gross_value="495780", net_investment="247890",
              quantity="180 spots", schedule="Monday-Sunday"),
        entry(generic, "pdf:page=1;page=2", "Plan A generic-only package",
              **shared, gross_value="495780", net_investment="247890",
              billing_period="6 consecutive months", monthly_cost="41315",
              restrictions="limited packages; no changes or cancellations"),
        entry(preroll, "pdf:page=1", "Generic 30 second component",
              **shared, gross_value="495780", net_investment="247890",
              quantity="180 spots", schedule="Monday-Sunday"),
        entry(preroll, "pdf:page=1;page=3", "Streaming pre-roll component",
              **shared, net_investment="90000", scheduled_quantity="6 spots",
              guaranteed_exposure="138462 listens per month",
              ambiguity="scheduled spots and guaranteed listens are distinct measures"),
        entry(preroll, "pdf:page=1;pages=2-3", "Generic and streaming pre-roll package",
              **shared, gross_value="585780", net_investment="337890",
              billing_period="6 consecutive months", monthly_cost="56315",
              relationship="contains generic and streaming pre-roll components"),
    ]


def daily_dispatch_entries() -> list[dict[str, Any]]:
    source = "0cf364b89b508bfaf6e8b00474db2bc17b17d07d121ea0e63bdaf72125efef67"
    shared = {"currency": "ZAR", "vat": "EXCLUDED", "agency_settlement_discount": "INCLUDED_FOR_QUALIFYING_AGENCIES"}
    result = daily_dispatch_print_entries(source, shared)
    result.extend(daily_dispatch_digital_entries(source, shared))
    result.extend(daily_dispatch_sponsorship_entries(source, shared))
    result.extend(daily_dispatch_package_entries(source, shared))
    return result


def daily_dispatch_print_entries(source: str, shared: dict[str, str]) -> list[dict[str, Any]]:
    result: list[dict[str, Any]] = []
    print_groups = {
        "Main body": [("Full Colour", "195.00"), ("1 Spot Colour", "127.00"), ("Black & White", "101.00")],
        "Special positions": [("Trade Rates", "Rate + 60%"), ("Front Page Ear Space", "5498.00"), ("Page 2 & 3 Facing Pages", "RATE_ON_REQUEST"), ("Newsprint wrap", "RATE_ON_REQUEST"), ("Guaranteed positions", "Rate + 30%")],
        "Used vehicles": [("Black & White", "86.00")], "Property estate agents": [("Quarter, half & full pages only", "32.00")],
        "Company reports & financial notices": [("Full Colour", "250.00"), ("1 Spot Colour", "208.00"), ("Black & White", "166.00")],
        "Legal notices & tenders": [("Full Colour", "225.00"), ("1 Spot Colour", "151.00"), ("Black & White", "135.00")],
        "Auctions": [("Full Colour", "206.00"), ("1 Spot Colour", "109.00"), ("Black & White", "101.00")],
        "Employment": [("Full Colour", "213.00"), ("1 Spot Colour", "144.00"), ("Black & White", "111.00"), ("Workwise national combo", "348.00")],
    }
    page = 2
    for group, offers in print_groups.items():
        if group in {"Legal notices & tenders", "Auctions", "Employment"}: page = 3
        for name, rate in offers:
            result.append(entry(source, f"pdf:page={page};section={group}", f"{group} - {name}",
                                channel="PRINT", rate=rate, charging_basis="PSCCM", **shared))
    for size, rates in {
        "Tabloid": ["951.00", "1001.00", "1102.00", "1271.00", "RATE_ON_REQUEST"],
        "A4": ["1120.00", "1184.00", "1257.00", "1329.00", "RATE_ON_REQUEST"],
        "A5": ["1347.00", "1425.00", "1509.00", "1603.00", "RATE_ON_REQUEST"],
    }.items():
        for paging, rate in zip(["4-8", "8-16", "16-24", "24-32", "32+"], rates):
            result.append(entry(source, f"pdf:page=4;size={size};paging={paging}", f"Outside printed inserts - {size} - {paging} pages",
                                channel="PRINT", rate=rate, charging_basis="PER_THOUSAND_INSERTS", **shared))
    return result


def daily_dispatch_digital_entries(source: str, shared: dict[str, str]) -> list[dict[str, Any]]:
    result: list[dict[str, Any]] = []
    digital = [
        ("Run of network - all sizes", "233.00", "CPM"), ("Run of network - high impact", "290.00", "CPM"),
        ("Arena video display", "500.00", "CPM"), ("Geotargeting layering", "56.00", "CPM_ADD_ON"),
        ("Section-specific layering", "56.00", "CPM_ADD_ON"), ("Viewability targeting", "56.00", "CPM_ADD_ON"),
        ("Audience targeting", "56.00", "CPM_ADD_ON"), ("Premium uplift on business sites", "56.00", "CPM_ADD_ON"),
        ("In-image ad unit", "389.00", "CPM"), ("Interstitial ad unit", "389.00", "CPM"),
        ("Newsletter in-article ad unit", "330.00", "CPM"), ("In-banner video", "400.00", "CPM"),
    ]
    for name, rate, basis in digital:
        result.append(entry(source, "pdf:page=7", name, channel="DIGITAL", rate=rate, charging_basis=basis, **shared))
    return result


def daily_dispatch_sponsorship_entries(source: str, shared: dict[str, str]) -> list[dict[str, Any]]:
    result: list[dict[str, Any]] = []
    hpto = [
        ("TIMESLIVE", "250000", "80769.00"), ("SUNDAY TIMES", "17000", "6730.00"), ("ST LIFESTYLE", "28000", "10769.00"),
        ("BUSINESS DAY", "44000", "33650.00"), ("BUSINESS TIMES", "5000", "2775.00"), ("FINANCIAL MAIL", "41000", "31356.00"),
        ("SOWETAN", "250000", "80769.00"), ("TSHISALIVE", "25000", "10256.00"), ("TIMESLIVE SPORT", "13000", "5769.00"),
        ("THE HERALD", "20000", "7404.00"), ("DAILY DISPATCH", "14000", "4712.00"),
    ]
    takeover = [
        ("TIMESLIVE", "250000", "40385.00"), ("SUNDAY TIMES", "17000", "3365.00"), ("BUSINESS DAY", "44000", "21032.00"),
        ("BUSINESS TIMES", "5000", "1735.00"), ("FINANCIAL MAIL", "41000", "17456.00"), ("SOWETAN", "250000", "40385.00"),
        ("TIMESLIVE SPORT", "13000", "3606.00"), ("THE HERALD", "20000", "4628.00"), ("DAILY DISPATCH", "14000", "2945.00"),
    ]
    for kind, offers in [("HPTO", hpto), ("SECTION_TAKEOVER", takeover)]:
        for brand, impressions, rate in offers:
            result.append(entry(source, f"pdf:page=8;variant={kind};brand={brand}", f"24 hour sponsorship - {kind} - {brand}",
                                channel="DIGITAL", rate=rate, charging_basis="24_HOUR_SPONSORSHIP", impressions=impressions, **shared))
    return result


def daily_dispatch_package_entries(source: str, shared: dict[str, str]) -> list[dict[str, Any]]:
    result: list[dict[str, Any]] = []
    packages = [
        ("Premium native package", "67000.00", "article; home/section exposure; social posts; newsletter banner; companion banners; 150000 ROS/RON impressions"),
        ("Standard native package", "50000.00", "article; home/section exposure; social posts; newsletter; companion banners; optional boosting"),
        ("Sponsored content", "34000.00", "article; sponsored Facebook post; sponsored tweet"),
        ("Listical profiles", "150000.00", "6-12 profiles; HPTO; display banners / ROS and RON"),
    ]
    for name, rate, contents in packages:
        result.append(entry(source, "pdf:page=9", name, channel="DIGITAL", rate=rate, charging_basis="PACKAGE", package_contents=contents, **shared))
    result.extend([
        entry(source, "pdf:page=10", "Vodcast", channel="DIGITAL", rate="RATE_ON_REQUEST", **shared),
        entry(source, "pdf:page=10", "Partner Hub", channel="DIGITAL", rate="55000.00", charging_basis="PER_MONTH", **shared),
        entry(source, "pdf:page=10", "Podcasts", channel="DIGITAL", rate="RATE_ON_REQUEST", variants="client curated; sponsored; programmatic", **shared),
    ])
    return result


def business_day_tv_entries() -> list[dict[str, Any]]:
    source = "8ce9e4d44812137792f2e63ae43511a827643083bbc234bc10e0cb14c48debd5"
    shared = {
        "channel": "TELEVISION", "currency": "ZAR", "duration_seconds": "30",
        "vat": "EXCLUDED", "agency_settlement_discount": "EXCLUDED",
        "programme_specific_loading": "20%", "average_spot_rate_context": "4500.00",
    }
    offers = [
        ("Monday-Friday 06H00-10H00 Morning Repeats", "3500.00"),
        ("Monday-Friday 10H00-16H30 Shoulder Time", "3500.00"),
        ("Monday-Friday 16H30-18H00 Premium Shoulder Time", "4000.00"),
        ("Monday-Friday 18H00-19H00", "5500.00"),
        ("Monday-Friday 19H00-20H00 Prime Time", "6500.00"),
        ("Monday-Friday 20H00-21H00", "5500.00"),
        ("Monday-Friday 21H00-22H00 Premium Shoulder Time", "4000.00"),
        ("Monday-Friday 22H00-24H00 Evening", "3500.00"),
        ("Friday midnight-Sunday SME Zone all weekend", "3000.00"),
    ]
    return [
        entry(source, f"pdf:page=2;rate-row={index}", f"Business Day TV 412 - {name}",
              rate=rate, charging_basis="PER_30_SECOND_SPOT", **shared)
        for index, (name, rate) in enumerate(offers, start=1)
    ]


def workbook_rows(workbook: dict[str, Any]) -> list[dict[str, Any]]:
    sheet = workbook["sheets"][0]
    cells = {cell["cell"]: cell.get("value") for cell in sheet["cells"]}
    headers = {column: cells.get(f"{column}1") for column in "ABCDEFGHIJK"}
    rows: list[dict[str, Any]] = []
    for row_number in range(2, 93):
        values = {name: cells.get(f"{column}{row_number}") for column, name in headers.items()}
        identity = str(values.pop("name") or "")
        rows.append(entry(
            workbook["source_hash"], f"xlsx:sheet=Sheet1;cells=A{row_number}:K{row_number}",
            identity, **values,
        ))
    return rows


def dms_entries(workbook: dict[str, Any]) -> list[dict[str, Any]]:
    media = next(item for item in workbook["embedded_media"] if item["path"].endswith("image2.png"))
    common = {
        "source_evidence": media["evidence_path"],
        "source_asset_sha256": media["sha256"],
        "currency": "ZAR",
        "source_limitation": "right edge of embedded source image truncates each displayed price",
    }
    rows = [
        ("DStv Stream VOD - Video Pre Roll - skippable after 5 seconds", "R575…"),
        ("DStv Stream VOD - Video Pre Roll - 15 seconds non-skip", "R1,10…"),
        ("DStv Stream Live - Video", "R500…"),
        ("YouTube - Video Pre Roll", "R200…"),
    ]
    return [
        entry(workbook["source_hash"], f"xlsx:sheet=Sheet1;embedded={media['path']};visual-row={index}",
              identity, raw_price=price, dimensions="16:9", asset_format="MP4", **common)
        for index, (identity, price) in enumerate(rows, start=1)
    ]


def expected_units(item: dict[str, Any]) -> int:
    return int(item.get("page_count") or item.get("slide_count") or item.get("worksheet_count") or 0)


def base_reference_entries() -> list[dict[str, Any]]:
    entries = algoa_entries() + daily_dispatch_entries() + business_day_tv_entries()
    entries += jozi_entries(entry) + mamg_entries(entry) + mediamark_entries(entry)
    entries += smile_entries(entry) + home_channel_entries(entry) + y_entries(entry)
    entries += virgin_active_entries(entry) + ignition_entries(entry)
    entries += jacaranda_entries(entry)
    entries += soweto_screens_entries(entry) + direct_kaya_entries(entry)
    entries += kena_digital_entries(entry) + kena_static_entries(entry)
    entries += sb_outdoor_entries(entry)
    entries += digital_rates_entries(entry)
    entries += summit_main_market_entries(entry)
    entries += summit_billboard_entries(entry)
    entries += rsd_gauteng_entries(entry)
    entries += rsd_western_cape_entries(entry)
    entries += reveel_entries(entry)
    entries += jit_tv_entries(entry)
    entries += commvibe_demographic_entries(entry)
    return entries


def source_record(
    item: dict[str, Any], rendered: dict[str, Any] | None, workbook: dict[str, Any] | None
) -> tuple[dict[str, Any], list[dict[str, Any]]]:
    source_hash = item["content_hash"]
    review = VISUAL_REVIEWS.get(source_hash)
    workbook_entries: list[dict[str, Any]] = []
    if item["format"] == "excel":
        review = {
            "units": [1], "entry_status": "ENUMERATED",
            "notes": "Visible sheet, hidden regions, formulas, cached values, drawings and embedded media inspected.",
        }
        if workbook is None:
            raise ValueError(f"Missing workbook evidence for {item['filename']}")
        workbook_entries = dms_entries(workbook) if "DMS" in item["filename"] else workbook_rows(workbook)
    unit_total = expected_units(item)
    inspected = review["units"] if review else []
    record = {
        "source_id": item["source_id"], "file_name": item["filename"],
        "source_hash": source_hash, "format": item["format"],
        "unit_kind": "sheet" if item["format"] == "excel" else ("slide" if item["format"] == "powerpoint" else "page"),
        "unit_total": unit_total, "render_status": rendered["status"] if rendered else "STRUCTURAL_WORKBOOK_REVIEW",
        "visually_inspected_units": inspected, "visual_inspection_status": "COMPLETE" if len(inspected) == unit_total else "PENDING",
        "entry_enumeration_status": review["entry_status"] if review else "PENDING",
        "notes": review["notes"] if review else "Not yet visually inspected.",
    }
    return record, workbook_entries


def collect_sources(
    manifest: dict[str, Any], render: dict[str, Any], workbook_data: dict[str, Any]
) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    render_by_hash = {item["source_hash"]: item for item in render["files"]}
    workbook_by_hash = {item["source_hash"]: item for item in workbook_data["workbooks"]}
    sources: list[dict[str, Any]] = []
    entries: list[dict[str, Any]] = []
    for item in manifest["files"]:
        source_hash = item["content_hash"]
        record, workbook_entries = source_record(
            item, render_by_hash.get(source_hash), workbook_by_hash.get(source_hash)
        )
        sources.append(record)
        entries.extend(workbook_entries)
    return sources, entries


def build_report(
    manifest: dict[str, Any], manifest_path: Path, render_path: Path,
    workbook_path: Path, sources: list[dict[str, Any]], entries: list[dict[str, Any]],
) -> dict[str, Any]:
    total_units = sum(item["unit_total"] for item in sources)
    inspected_units = sum(len(item["visually_inspected_units"]) for item in sources)
    enumerated_files = sum(item["entry_enumeration_status"] == "ENUMERATED" for item in sources)
    return {
        "schema_version": SCHEMA_VERSION,
        "generated_at_utc": datetime.now(timezone.utc).isoformat(),
        "status": (
            "PHYSICAL_INSPECTION_COMPLETE_REFERENCE_ENUMERATION_PARTIAL_RECONCILIATION_BLOCKED"
            if inspected_units == total_units else "IN_PROGRESS"
        ),
        "basis": {
            "manifest_path": str(manifest_path.resolve()), "manifest_sha256": sha256(manifest_path),
            "render_manifest_path": str(render_path.resolve()), "render_manifest_sha256": sha256(render_path),
            "workbook_structure_path": str(workbook_path.resolve()), "workbook_structure_sha256": sha256(workbook_path),
            "independence": "Expected entries derive from original-source visual/structural review, not application output.",
            "implementation_binding": verification_binding(Path(__file__).resolve().parents[1]),
        },
        "collection": {"files": len(sources), "format_counts": manifest["format_counts"], "total_bytes": manifest["total_bytes"]},
        "coverage": {
            "physical_units_inspected": inspected_units, "physical_units_total": total_units,
            "physical_inspection_fraction": f"{inspected_units}/{total_units}",
            "files_fully_inspected": sum(item["visual_inspection_status"] == "COMPLETE" for item in sources),
            "files_total": len(sources),
            "entries_enumerated": len(entries),
            "files_with_complete_entry_enumeration": enumerated_files,
        },
        "application_reconciliation": {
            "status": "BLOCKED_CURRENT_PROJECTION_NOT_RUN",
            "required_projection": "advertified-projection/4.0.0",
            "historical_projection_outputs": "diagnostic only; excluded from current quality metrics",
            "blocker": "The stopped existing API image predates the fail-closed pause wiring; no authorised safe canonical runner is available without rebuilding/recreating a container.",
        },
        "quality_metrics": {
            "physical_coverage": {
                "status": "MEASURED",
                "numerator": inspected_units,
                "denominator": total_units,
            },
            "reference_entry_coverage": {
                "status": "PARTIAL",
                "files_enumerated": enumerated_files,
                "files_total": len(sources),
            },
            "recall": {"status": "BLOCKED_CURRENT_PROJECTION_NOT_RUN"},
            "precision": {"status": "BLOCKED_CURRENT_PROJECTION_NOT_RUN"},
            "field_completeness": {"status": "BLOCKED_CURRENT_PROJECTION_NOT_RUN"},
        },
        "sources": sources,
        "commercial_entries": entries,
        "source_ambiguities": [
            {"source_hash": "2e2bb6e6a70bbd8c54b6d03deb3643d72126e1040752d7ef0e05c9ec456a25b5", "count": 4,
             "description": "All four rate strings are visibly truncated at the right edge of the embedded image; omitted digits must not be inferred."},
        ],
    }


def build(manifest_path: Path, render_path: Path, workbook_path: Path,
          source_maps: Path) -> dict[str, Any]:
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    render = json.loads(render_path.read_text(encoding="utf-8"))
    workbook_data = json.loads(workbook_path.read_text(encoding="utf-8"))
    sources, workbook_entries = collect_sources(manifest, render, workbook_data)
    entries = base_reference_entries() + workbook_entries
    entries += dense_reference_entries(entry, source_maps)
    return build_report(
        manifest, manifest_path, render_path, workbook_path, sources, entries
    )


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--render-manifest", type=Path, required=True)
    parser.add_argument("--workbooks", type=Path, required=True)
    parser.add_argument("--source-maps", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    payload = build(args.manifest, args.render_manifest, args.workbooks, args.source_maps)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(json.dumps({"output": str(args.output), "coverage": payload["coverage"]}, indent=2))


if __name__ == "__main__":
    main()

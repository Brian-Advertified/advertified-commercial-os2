"""Build deterministic Advertified audience-intelligence bootstrap datasets.

Production output contains only public reference aggregates that Advertified may carry with
its inventory release. Optional MAPS output is proof-only and is never promoted by this tool.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import re
import shutil
import subprocess
import tempfile
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PRODUCTION_OUTPUT = ROOT / "data" / "production" / "audience-bootstrap.v1.json"
RESEARCH_OUTPUT = ROOT / "data" / "research" / "audience-bootstrap.proof.v1.json"
MAPS_URL = "https://mrfsa.org.za/wp-content/uploads/2021/05/MAPS-WAVE-1-2020-DEMOGRAPHICS.pdf"

PROVINCES = {
    "WC": "Western Cape", "EC": "Eastern Cape", "NC": "Northern Cape",
    "FS": "Free State", "KZN": "KwaZulu-Natal", "NW": "North West",
    "GP": "Gauteng", "MP": "Mpumalanga", "LP": "Limpopo", "ZA": "South Africa",
}
ORDER = ["WC", "EC", "NC", "FS", "KZN", "NW", "GP", "MP", "LP", "ZA"]
MAPS_GEOS = [
    ("ZA", "South Africa", "COUNTRY"), ("METRO", "Metro", "SETTLEMENT"),
    ("URBAN", "Urban", "SETTLEMENT"), ("RURAL", "Rural", "SETTLEMENT"),
    ("EC", "Eastern Cape", "PROVINCE"), ("FS", "Free State", "PROVINCE"),
    ("GP", "Gauteng", "PROVINCE"), ("KZN", "KwaZulu-Natal", "PROVINCE"),
    ("LP", "Limpopo", "PROVINCE"), ("MP", "Mpumalanga", "PROVINCE"),
    ("NC", "Northern Cape", "PROVINCE"), ("NW", "North West", "PROVINCE"),
    ("WC", "Western Cape", "PROVINCE"),
]
MAPS_SECTIONS = {
    "AGE GROUPS", "GENDER", "MARITAL STATUS", "ETHNIC GROUP",
    "LANGUAGE MOST OFTEN SPOKEN", "HIGHEST LEVEL OF EDUCATION", "WORK STATUS",
    "SOURCES OF INCOME", "MONTHLY HOUSEHOULD INCOME", "MONTHLY PERSONAL INCOME",
    "YOUNG OR UNMARRIED CHILDREN", "CHILDREN CURRENTLY LIVING WITH YOU",
    "DEPENDENT CHILDREN", "OTHER DEPENDENTS (NOT OWN CHILDREN)",
    "OTHER DEPENDENTS LIVING WITH YOU", "NUMBER OF PEOPLE CURRENTLY LIVING IN HH",
    "NUMBER OF DOMESTIC WORKERS", "NUMBER OF DOMESTIC WORKERS LIVE ON PREMISES",
}

CENSUS_LANGUAGE = {
    "Afrikaans": [41.2, 9.6, 54.6, 10.3, 1.0, 5.2, 7.7, 3.2, 2.3, 10.6],
    "English": [22.0, 4.8, 2.4, 1.5, 14.4, 1.0, 9.2, 1.5, 1.0, 8.7],
    "IsiNdebele": [0.2, 0.1, 0.0, 0.1, 0.0, 0.4, 3.1, 9.9, 1.1, 1.7],
    "IsiXhosa": [31.4, 81.8, 4.5, 5.5, 3.1, 4.8, 6.7, 1.0, 0.2, 16.3],
    "IsiZulu": [0.4, 0.3, 0.3, 3.7, 80.0, 1.6, 23.1, 27.8, 0.6, 24.4],
    "Sepedi": [0.1, 0.0, 0.1, 0.2, 0.1, 2.1, 12.6, 10.3, 55.5, 10.0],
    "Sesotho": [1.0, 2.4, 1.2, 72.3, 0.6, 5.9, 13.1, 2.3, 0.8, 7.8],
    "Setswana": [0.1, 0.0, 35.7, 5.3, 0.0, 72.8, 10.4, 1.6, 1.4, 8.3],
    "Sign language": [0.01, 0.01, 0.02, 0.01, 0.01, 0.03, 0.02, 0.02, 0.02, 0.02],
    "SiSwati": [0.0, 0.0, 0.0, 0.1, 0.0, 0.2, 0.9, 30.5, 0.3, 2.8],
    "Tshivenda": [0.1, 0.0, 0.1, 0.1, 0.0, 0.4, 2.4, 0.2, 17.4, 2.5],
    "XiTsonga": [0.2, 0.1, 0.1, 0.2, 0.0, 3.1, 7.0, 10.6, 17.3, 4.7],
    "Khoi, Nama & San languages": [0.00, 0.01, 0.17, 0.01, 0.00, 0.01, 0.01, 0.01, 0.01, 0.01],
    "Shona": [2.0, 0.5, 0.4, 0.3, 0.3, 1.6, 2.1, 0.6, 1.6, 1.2],
    "Chichewa/Chewa/Nyanja/Chinyanja": [0.5, 0.1, 0.1, 0.0, 0.2, 0.2, 0.6, 0.1, 0.0, 0.3],
    "Portuguese": [0.1, 0.0, 0.1, 0.0, 0.1, 0.2, 0.3, 0.3, 0.0, 0.2],
    "Other": [0.7, 0.4, 0.3, 0.3, 0.2, 0.3, 0.7, 0.3, 0.4, 0.4],
}
GHS_INTERNET = {
    "Mobile": [80.6, 71.1, 68.8, 76.3, 84.9, 75.4, 77.3, 82.4, 81.4, 78.9],
    "Fixed Internet at home": [49.7, 11.6, 11.6, 11.6, 10.6, 8.9, 31.1, 7.3, 5.9, 20.6],
    "Internet at work": [21.5, 8.7, 9.4, 9.4, 10.7, 5.9, 18.5, 8.9, 3.7, 12.9],
    "Public Wi-Fi": [16.4, 4.8, 12.6, 7.9, 4.3, 4.9, 10.4, 9.3, 2.3, 8.2],
    "Internet Café": [12.7, 2.8, 0.2, 3.2, 2.9, 2.0, 7.4, 6.6, 0.6, 5.4],
    "At an educational facility": [7.6, 3.9, 0.8, 5.9, 2.4, 4.3, 4.8, 2.1, 0.7, 4.0],
    "At a library": [6.8, 0.8, 0.2, 3.2, 3.1, 0.9, 1.1, 0.9, 0.4, 2.1],
    "Any kind of access": [93.8, 74.5, 74.7, 83.1, 87.3, 77.9, 88.5, 85.4, 83.8, 85.6],
}
QLFS = {
    "WC": (19.5, 54.6, 67.8), "EC": (47.5, 28.9, 55.1),
    "NC": (28.7, 36.3, 51.0), "FS": (37.3, 39.6, 63.1),
    "KZN": (32.5, 34.8, 51.6), "NW": (38.1, 30.6, 49.5),
    "GP": (34.6, 43.9, 67.2), "MP": (36.6, 39.1, 61.7),
    "LP": (32.0, 36.6, 53.9), "ZA": (33.6, 39.6, 59.6),
}


def geo(code: str) -> dict[str, str]:
    return {"level": "COUNTRY" if code == "ZA" else "PROVINCE", "code": code, "name": PROVINCES[code]}


def observation(source: str, code: str, group: str, segment: str, locator: str,
                *, share: float | None = None, count: int | None = None,
                policy: str = "AGGREGATE_PLANNING_ONLY", stability: str = "PUBLISHED_OFFICIAL") -> dict:
    result = {
        "source": source, "geography": geo(code),
        "dimensions": {"group": group, "segment": segment},
        "stability": stability, "activationPolicy": policy, "sourceLocator": locator,
    }
    if share is not None:
        result["sharePercent"] = share
    if count is not None:
        result["audienceCount"] = count
    return result


def production_payload() -> dict:
    observations: list[dict] = []
    for language, values in CENSUS_LANGUAGE.items():
        for code, value in zip(ORDER, values):
            observations.append(observation(
                "stats-sa-census-2022-statistical-release", code,
                "HOUSEHOLD_LANGUAGE", language,
                f"P0301.4 Table 2.9::{language}::{code}", share=value))
    observations.extend([
        observation("stats-sa-census-2022-statistical-release", "ZA", "SEX", "Male",
                    "P0301.4 Executive Summary::Sex::Male", share=48.5),
        observation("stats-sa-census-2022-statistical-release", "ZA", "SEX", "Female",
                    "P0301.4 Executive Summary::Sex::Female", share=51.5),
        observation("stats-sa-census-2022-statistical-release", "ZA", "TOTAL_POPULATION", "All",
                    "Census 2022 portal::Total population", count=62027503),
        observation("stats-sa-census-2022-statistical-release", "GP", "TOTAL_POPULATION", "All",
                    "Census 2022 portal::Gauteng total population", count=15099422),
        observation("stats-sa-census-2022-statistical-release", "ZA", "POPULATION_GROUP", "Indian/Asian",
                    "P0301.4 Table 2.4 narrative::Indian/Asian::ZA", count=1697505,
                    policy="SENSITIVE_CONTEXT_ONLY"),
        observation("stats-sa-census-2022-statistical-release", "KZN", "POPULATION_GROUP", "Indian/Asian",
                    "P0301.4 Table 2.4 narrative::Indian/Asian::KZN", count=1157542,
                    policy="SENSITIVE_CONTEXT_ONLY"),
        observation("stats-sa-census-2022-statistical-release", "GP", "POPULATION_GROUP", "Indian/Asian",
                    "P0301.4 Table 2.4 narrative::Indian/Asian::GP", count=329736,
                    policy="SENSITIVE_CONTEXT_ONLY"),
    ])
    for access, values in GHS_INTERNET.items():
        for code, value in zip(ORDER, values):
            observations.append(observation(
                "stats-sa-ghs-2025", code, "HOUSEHOLD_INTERNET_ACCESS", access,
                f"P0318 2025 Table 14.1::{access}::{code}", share=value))
    measures = ("Unemployment rate", "Absorption rate", "Labour force participation rate")
    for code, values in QLFS.items():
        for measure, value in zip(measures, values):
            observations.append(observation(
                "stats-sa-qlfs-2026-q2", code, "LABOUR_MARKET", measure,
                f"P0211 Q2 2026 Section 5::{measure}::{code}", share=value))

    sources = [
        {
            "key": "stats-sa-census-2022-statistical-release",
            "title": "Census 2022 Statistical Release P0301.4", "publisher": "Statistics South Africa",
            "measurementPeriod": "2022",
            "sourceLocator": "https://census.statssa.gov.za/assets/documents/2022/P03014_Census_2022_Statistical_Release.pdf",
            "licenceStatus": "PUBLIC_ATTRIBUTION_REQUIRED", "promotionStatus": "PRODUCTION_ALLOWED",
            "usageScope": "AGGREGATE_PLANNING",
            "methodology": "Official published Census 2022 aggregate results. Keep Stats SA attribution and do not resell the underlying/reprocessed dataset as a standalone data product without permission.",
            "universe": "South African Census 2022 population/households as defined by each cited table.",
            "capabilities": ["province", "household_language", "sex", "population_group", "population_total"],
        },
        {
            "key": "stats-sa-ghs-2025", "title": "General Household Survey 2025 P0318",
            "publisher": "Statistics South Africa", "measurementPeriod": "2025",
            "sourceLocator": "https://www.statssa.gov.za/publications/P0318/P03182025.pdf",
            "licenceStatus": "PUBLIC_ATTRIBUTION_REQUIRED", "promotionStatus": "PRODUCTION_ALLOWED",
            "usageScope": "AGGREGATE_PLANNING",
            "methodology": "Official annual household survey; bootstrap observations are published aggregate percentages from Table 14.1.",
            "universe": "South African households represented by GHS 2025.",
            "capabilities": ["province", "household_internet_access"],
        },
        {
            "key": "stats-sa-qlfs-2026-q2", "title": "Quarterly Labour Force Survey Q2 2026 P0211",
            "publisher": "Statistics South Africa", "measurementPeriod": "2026-Q2",
            "sourceLocator": "https://www.statssa.gov.za/publications/P0211/P02112ndQuarter2026.pdf",
            "licenceStatus": "PUBLIC_ATTRIBUTION_REQUIRED", "promotionStatus": "PRODUCTION_ALLOWED",
            "usageScope": "AGGREGATE_PLANNING", "methodology": "Official QLFS Q2 2026 labour-market rates.",
            "universe": "Persons aged 15-64 for the reported labour-market measures.",
            "capabilities": ["province", "unemployment_rate", "absorption_rate", "labour_force_participation_rate"],
        },
        {
            "key": "stats-sa-census-2022-microdata", "title": "South African Census 2022 10% Sample",
            "publisher": "Statistics South Africa / DataFirst", "measurementPeriod": "2022",
            "sourceLocator": "https://www.datafirst.uct.ac.za/dataportal/index.php/catalog/982",
            "licenceStatus": "CC_BY_4_0_WITH_CONFIDENTIALITY_DECLARATION",
            "promotionStatus": "FREE_DOWNLOAD_PENDING_LOGIN", "usageScope": "AGGREGATE_QUERY_AFTER_DOWNLOAD",
            "methodology": "Edited anonymised 10% Census sample. DataFirst requires a free account and a non-reidentification declaration. Aggregate queries must retain sampling and quality caveats.",
            "universe": "Households and persons in the Census 2022 10% sample; lowest released geography is local municipality.",
            "capabilities": ["province", "district_municipality", "local_municipality", "urban_rural", "sex", "age", "age_group", "relationship_to_household_head", "marital_status", "household_language", "citizenship", "country_of_birth", "region_of_birth", "usual_residence", "education"],
            "knownCodes": {"countryOfBirth": {"Bangladesh": 306, "India": 315, "Pakistan": 335}, "householdLanguage": {"IsiZulu": 5, "SiSwati": 10}},
            "qualityNotes": [
                "Country of birth has a reported 42.3% aggregate index of inconsistency against the PES and must carry a quality caveat.",
                "Income and employment sections were not released in the 2022 microdata due data-quality concerns; use GHS/QLFS instead.",
            ],
        },
    ]
    return {"schemaVersion": "audience-intelligence-bootstrap.v1", "generatedOn": "2026-09-10", "sources": sources, "observations": observations}


def maps_text(pdf_path: Path) -> str:
    executable = shutil.which("pdftotext")
    if executable is None:
        raise RuntimeError("pdftotext is required for optional MAPS proof ingestion")
    output = pdf_path.with_suffix(".txt")
    subprocess.run([executable, "-layout", str(pdf_path), str(output)], check=True)
    return output.read_text(encoding="utf-8", errors="ignore")


def parse_maps(text: str, source_hash: str) -> dict:
    observations: list[dict] = []
    section: str | None = None
    lines = text.splitlines()
    for index, line in enumerate(lines):
        stripped = line.strip()
        if stripped in MAPS_SECTIONS:
            section = stripped
            continue
        if section is None or "Audience(000)" not in line:
            continue
        label_index = index + 1
        while label_index < len(lines) and not lines[label_index].strip():
            label_index += 1
        if label_index >= len(lines):
            continue
        label = lines[label_index].strip()
        pct_index = label_index + 1
        while pct_index < len(lines) and not lines[pct_index].strip():
            pct_index += 1
        if pct_index >= len(lines) or "%Col" not in lines[pct_index] or label in MAPS_SECTIONS:
            continue
        counts = re.split(r"\s{2,}", line.split("Audience(000)", 1)[1].strip())
        shares = re.split(r"\s{2,}", lines[pct_index].split("%Col", 1)[1].strip())
        if len(counts) != 13 or len(shares) != 13:
            continue
        for (code, name, level), raw_count, raw_share in zip(MAPS_GEOS, counts, shares):
            stars = 2 if raw_count.startswith("**") else 1 if raw_count.startswith("*") else 0
            count = int(raw_count.lstrip("*").replace(" ", "")) * 1000
            share = float(raw_share.replace(",", "."))
            policy = "SENSITIVE_CONTEXT_ONLY" if section == "ETHNIC GROUP" else "AGGREGATE_PLANNING_ONLY"
            observations.append({
                "source": "mrf-maps-2020-wave1-demographics",
                "geography": {"level": level, "code": code, "name": name},
                "dimensions": {"group": section, "segment": label},
                "audienceCount": count, "sharePercent": share,
                "stability": "HIGHLY_UNSTABLE" if stars == 2 else "RELATIVELY_UNSTABLE" if stars == 1 else "PUBLISHED",
                "activationPolicy": policy,
                "sourceLocator": f"MAPS-WAVE-1-2020-DEMOGRAPHICS.pdf::{section}::{label}::{code}",
            })
    source = {
        "key": "mrf-maps-2020-wave1-demographics", "title": "MAPS Wave 1 2020 Demographics",
        "publisher": "Marketing Research Foundation", "measurementPeriod": "2020-07/2020-12",
        "sourceLocator": MAPS_URL, "sourceHash": source_hash,
        "licenceStatus": "PUBLIC_RELEASE_RIGHTS_FOR_PRODUCTION_NOT_VERIFIED",
        "promotionStatus": "PROOF_ONLY_DO_NOT_PROMOTE", "usageScope": "AGGREGATE_RESEARCH_PROOF",
        "methodology": "MRF public demographic release. The document warns that it is half of an annual sample and marks relatively unstable (*) and highly unstable (**) estimates.",
        "universe": "Weighted population represented in the public MAPS release.",
        "capabilities": sorted({item["dimensions"]["group"] for item in observations}),
        "qualityNotes": ["Do not use as current production currency without confirming MRF licence/contract terms.", "Preserve source stability flags; never hide * or ** warnings."],
    }
    return {"schemaVersion": "audience-intelligence-bootstrap.v1", "generatedOn": "2026-09-10", "sources": [source], "observations": observations}


def write_json(path: Path, payload: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, ensure_ascii=False, separators=(",", ":")) + "\n", encoding="utf-8")


def build(include_maps: bool) -> tuple[dict, dict | None]:
    production = production_payload()
    proof = None
    if include_maps:
        with tempfile.TemporaryDirectory(prefix="advertified-maps-") as directory:
            pdf = Path(directory) / "maps.pdf"
            urllib.request.urlretrieve(MAPS_URL, pdf)
            digest = hashlib.sha256(pdf.read_bytes()).hexdigest()
            proof = parse_maps(maps_text(pdf), digest)
    return production, proof


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--include-maps", action="store_true")
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    production, proof = build(args.include_maps)
    expected = json.dumps(production, ensure_ascii=False, separators=(",", ":")) + "\n"
    if args.check:
        if not PRODUCTION_OUTPUT.exists() or PRODUCTION_OUTPUT.read_text(encoding="utf-8") != expected:
            raise SystemExit("production audience bootstrap is stale; regenerate it")
        if args.include_maps:
            expected_proof = json.dumps(proof, ensure_ascii=False, separators=(",", ":")) + "\n"
            if not RESEARCH_OUTPUT.exists() or RESEARCH_OUTPUT.read_text(encoding="utf-8") != expected_proof:
                raise SystemExit("MAPS proof bootstrap is stale; regenerate it")
        print(f"Audience bootstrap current: {len(production['observations'])} production observations")
        return 0
    write_json(PRODUCTION_OUTPUT, production)
    if proof is not None:
        write_json(RESEARCH_OUTPUT, proof)
    print(f"Wrote {len(production['observations'])} production observations to {PRODUCTION_OUTPUT.relative_to(ROOT)}")
    if proof is not None:
        print(f"Wrote {len(proof['observations'])} MAPS proof observations to {RESEARCH_OUTPUT.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

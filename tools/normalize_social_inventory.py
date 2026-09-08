"""Normalize governed social inventory classifications in the canonical seed."""
from __future__ import annotations

import argparse
import json
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_SEED = REPO_ROOT / "data" / "production" / "inventory-bootstrap.v1.json"
MASTER_DATA = REPO_ROOT / "shared" / "contracts" / "master-data.json"


def active_code(collection: str, label: str) -> str:
    master_data = json.loads(MASTER_DATA.read_text(encoding="utf-8"))
    matches = [
        item["code"]
        for item in master_data["collections"][collection]
        if item["displayLabel"] == label and item["isActive"]
    ]
    if len(matches) != 1:
        raise ValueError(f"Expected one active {collection} item labelled {label}.")
    return matches[0]


SOCIAL_NAME_TERMS = (
    "social", "facebook", "instagram", "tiktok", "linkedin", "youtube", "twitter",
)


def is_explicit_social_product(record: dict) -> bool:
    name = str(record.get("name") or "").casefold()
    return any(term in name for term in SOCIAL_NAME_TERMS)


def normalize(seed: dict) -> tuple[int, int]:
    social_channel = active_code("channels", "Social Media")
    social_placement = active_code("inventoryProductTypes", "Social Placement")
    placements = [record for record in seed["records"] if is_explicit_social_product(record)]
    if not placements:
        raise ValueError("The canonical seed contains no explicitly named social inventory.")
    changed = 0
    for record in placements:
        if (record.get("channel"), record.get("productType")) != (
            social_channel,
            social_placement,
        ):
            record["channel"] = social_channel
            record["productType"] = social_placement
            changed += 1
    return len(placements), changed


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--seed", type=Path, default=DEFAULT_SEED)
    parser.add_argument("--check", action="store_true")
    parser.add_argument("--details", action="store_true")
    args = parser.parse_args()
    seed = json.loads(args.seed.read_text(encoding="utf-8"))
    placements, changed = normalize(seed)
    explicit_social = [
        record for record in seed["records"] if is_explicit_social_product(record)
    ]
    result = {
        "socialPlacements": placements,
        "publishedSocialPlacements": sum(
            1 for record in explicit_social if record.get("publicationEligible") is True
        ),
        "recordsRequiringChange": changed,
        "mode": "check" if args.check else "write",
    }
    if args.details:
        result["records"] = [
            record for record in seed["records"] if is_explicit_social_product(record)
        ]
    print(json.dumps(result, indent=2, ensure_ascii=False))
    if args.check:
        return 0 if changed == 0 else 2
    if changed:
        args.seed.write_text(
            json.dumps(seed, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
            newline="\n",
        )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

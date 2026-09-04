"""Render concise unresolved physical-certification failures."""
from __future__ import annotations
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REPORT = ROOT / "artifacts" / "inventory-corpus" / "physical-certification" / "corpus-physical-certification.json"
OUTPUT = REPORT.with_name("FAILURES.md")

def main() -> int:
    report = json.loads(REPORT.read_text(encoding="utf-8"))
    rows = ["# Physical certification failures", ""]
    for item in report.get("documents", []):
        if item.get("physically_certified"):
            continue
        rows.extend([
            f"## {item.get('file_name')}",
            "",
            f"- Anchors: {item.get('matched_anchor_count')}/{item.get('physical_anchor_count')}",
            f"- Candidates: {item.get('candidate_count')}",
            f"- Core empty: {item.get('core_empty_candidate_count')}",
            f"- Missing identity: {item.get('candidate_count', 0) - item.get('candidate_with_identity_count', 0)}",
            f"- Missing classification: {item.get('candidate_count', 0) - item.get('candidate_with_classification_count', 0)}",
            f"- Missing supplier: {item.get('candidate_count', 0) - item.get('candidate_with_supplier_count', 0)}",
            f"- Missing rate/explicit unknown: {item.get('candidate_count', 0) - item.get('candidate_with_rate_or_explicit_unknown_count', 0)}",
            f"- Duplicates: {item.get('duplicate_candidate_count')}",
            f"- Blockers: {', '.join(item.get('blockers') or [])}",
            "",
            "First unmatched physical anchors:",
        ])
        for anchor in (item.get("unmatched_anchors") or [])[:8]:
            rows.append(
                f"- `{anchor.get('kind')}` page/slide {anchor.get('ordinal')}: "
                f"`{str(anchor.get('raw') or '')[:160]}` ({anchor.get('locator')})"
            )
        rows.append("")
    OUTPUT.write_text("\n".join(rows), encoding="utf-8")
    print(str(OUTPUT.relative_to(ROOT)))
    return 0

if __name__ == "__main__":
    raise SystemExit(main())

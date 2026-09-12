"""Export the canonical Python scenario definitions for the pinned API test graph."""
import argparse
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "agent-runtime"))
from business_scenarios.backend_acceptance_catalog import BACKEND_ACCEPTANCE_SCENARIOS

TARGET = ROOT / "api/tests/Advertified.Commercial.Api.Tests/Fixtures/BackendScenarios/catalogue.json"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    content = json.dumps({
        "generated_by": "tools/generate_backend_scenario_catalog.py",
        "source": "agent-runtime/business_scenarios/backend_acceptance_catalog.py",
        "scenarios": [case.model_dump(mode="json") for case in BACKEND_ACCEPTANCE_SCENARIOS],
    }, indent=2) + "\n"
    if args.check:
        if not TARGET.exists() or TARGET.read_text(encoding="utf-8") != content:
            raise SystemExit("API scenario catalogue is stale; regenerate it.")
    else:
        TARGET.parent.mkdir(parents=True, exist_ok=True)
        TARGET.write_text(content, encoding="utf-8")
    print(f"Verified {len(BACKEND_ACCEPTANCE_SCENARIOS)} canonical scenario definitions.")


if __name__ == "__main__":
    main()

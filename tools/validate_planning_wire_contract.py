"""Validate an actual C# adapter export with the production Python request model; no I/O providers."""

import argparse
import json
from pathlib import Path
import sys

from pydantic import ValidationError

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "agent-runtime"))
from planning_contracts import InventoryIntelligenceAgentRequest  # noqa: E402


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("payload", type=Path)
    args = parser.parse_args()
    payload = json.loads(args.payload.read_text(encoding="utf-8"))
    request = InventoryIntelligenceAgentRequest.model_validate_json(json.dumps(payload))
    assert {candidate.is_eligible for candidate in request.inventory.candidates} == {True, False}
    payload["inventory"]["candidates"][0]["score"] = 0.01
    try:
        InventoryIntelligenceAgentRequest.model_validate_json(json.dumps(payload))
    except ValidationError as error:
        assert "matching governed" in str(error)
    else:
        raise AssertionError("Mismatched deterministic suitability was accepted")
    print("PASS: actual C# eligible/rejected request validates; mismatched score rejected")


if __name__ == "__main__":
    main()

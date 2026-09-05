"""Validate exported C# supplied-brief requests/responses and grounding without providers."""

import argparse
import json
from pathlib import Path
import sys
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "agent-runtime"))
from contracts import AgentOutputEnvelope  # noqa: E402
from supplied_brief_contracts import SuppliedBriefRequest, SuppliedBriefArtifact  # noqa: E402
from supplied_brief_service import validate_source, validate_grounding  # noqa: E402
from agent_registry import AgentCode  # noqa: E402
from fastapi import HTTPException  # noqa: E402
from runtime_execution import execute_agent, DETERMINISTIC_MODE, BEDROCK_MODE  # noqa: E402


def validate_runtime_route(request, response):
    body = request.model_dump_json().encode()
    try:
        execute_agent(AgentCode.BRIEF_DRAFTING, body, DETERMINISTIC_MODE)
    except HTTPException as error:
        assert error.status_code == 503
    else:
        raise AssertionError("Supplied brief unexpectedly fell back to a fixture")
    simulated = json.loads(body)
    simulated["invocation"]["provider_policy"].update(
        provider="bedrock", model="offline-contract-provider", allow_live=True, cost_cap_minor=1)
    # Intercept the sole provider boundary before using the simulated policy; no SDK can execute.
    with patch("runtime_execution.generate_with_bedrock", return_value=response) as provider:
        result = execute_agent(AgentCode.BRIEF_DRAFTING, json.dumps(simulated).encode(), BEDROCK_MODE)
        provider.assert_called_once()
    assert result["artifact"]["source_hash"] == request.source.source_hash


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    args = parser.parse_args()
    for name in ("OOH_ONLY", "FULL_CAMPAIGN", "AMBIGUOUS"):
        request = SuppliedBriefRequest.model_validate_json(
            (args.directory / f"brief-{name}-request.json").read_bytes())
        response = AgentOutputEnvelope[SuppliedBriefArtifact].model_validate_json(
            (args.directory / f"brief-{name}-response.json").read_bytes())
        validate_source(request)
        validate_grounding(request, response)
        validate_runtime_route(request, response)
        print(f"PASS: actual C# {name} contracts, grounding, fake-provider runtime and no fixture fallback")


if __name__ == "__main__":
    main()

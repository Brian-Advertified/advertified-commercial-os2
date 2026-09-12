"""Representative frontier-model Audience Intelligence comparison.

Reuses the same scenario definitions and business flags as the full battery. Change MODEL
only when intentionally comparing another governed Bedrock model; the commercial scenarios
and acceptance instrumentation remain identical.
"""

import json

from business_scenarios.bedrock_support import configure_live_bedrock
from business_scenarios.test_audience_complexity_live import SCENARIOS, _run

MODEL = "global.openai.gpt-5.6-sol"
SELECTED = {
    "mukuru_strict_budgetless_sensitive_context",
    "rayetsa_sparse_retail_no_audience",
    "vaccination_awareness_detailed_public_service",
    "contradictory_media_requirements",
}


def test_frontier_audience_comparison_live() -> None:
    model = configure_live_bedrock(MODEL)
    results = [_run(item) for item in SCENARIOS if item.name in SELECTED]
    print(f"\nFRONTIER AUDIENCE COMPARISON: {model}\n")
    for result in results:
        print(json.dumps(result, indent=2, default=str))
    assert len(results) == len(SELECTED)

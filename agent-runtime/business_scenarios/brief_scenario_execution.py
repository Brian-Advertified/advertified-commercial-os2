"""Run real runtime/authentication/grounding boundaries with synthetic model proposals."""
from unittest.mock import patch

from supplied_brief_model_input import build_model_input
from supplied_brief_service import canonicalize_grounding
from business_scenarios.brief_scenario_fixtures import fixture

from business_scenarios.scenario_runtime_http import invoke


def execute(scenario):
    request, proposal, expected = fixture(scenario)
    observed_input = build_model_input(request)
    calls = []

    def provider(actual):
        calls.append(actual.source.source_hash)
        return canonicalize_grounding(actual, proposal)

    with patch("runtime_execution.unavailable_fixture", side_effect=provider):
        denied, response = invoke("brief_drafting", request.model_dump(mode="json"))
    result = response.json()
    if response.status_code != 200:
        raise AssertionError(f"Runtime rejected deterministic scenario: {response.status_code}: {result}")
    artifact = result["artifact"]
    checks = field_checks(artifact, expected)
    checks["original_source_preserved"] = (
        request.source.source_content == "".join(
            segment["content"] for segment in observed_input["source"]["segments"])
        and artifact["source_hash"] == request.source.source_hash)
    checks["service_authentication"] = denied.status_code == 401 and len(calls) == 1
    checks["human_review_required"] = (
        result["status"] == "REVIEW_REQUIRED"
        and result["suggested_next_action"]["requires_human"])
    unknowns = {item["field_path"] for item in artifact["questions"] if item["is_blocking"]}
    return dict(
        source_request=request.model_dump(mode="json"), output=result,
        checks=checks, unsupported_fact_count=sum(not value for value in field_checks(artifact, expected).values()),
        missing_required_fact_count=len(unknowns),
        invariant_results={
            "SOURCE_FACTS_RETAINED": checks["original_source_preserved"] and all(
                value for key, value in checks.items() if key.startswith("field_")),
            "NO_UNSUPPORTED_FACTS": all(field_checks(artifact, expected).values()),
            "MODE_DOES_NOT_SILENTLY_EXPAND": checks["field_mode"],
        },
        terminal_state=result["status"], human_review_points=["SUPPLIED_BRIEF_REVIEW"],
        commercial_reconciliation="NOT_APPLICABLE_PROPOSE_ONLY",
        tenant_security_result="RUNTIME_SERVICE_AUTH_VERIFIED" if checks["service_authentication"] else "FAILED",
        fixture_calls=len(calls),
    )


def field_checks(artifact, expected):
    draft = artifact["draft"]
    checks = {
        "field_" + field: draft[field] == expected[field]
        for field in ("business_problem", "objective", "audiences", "geographies", "timing",
                      "budget_minor", "currency")
    }
    checks["field_mode"] = artifact["campaign_mode"] == expected["mode"]
    checks["field_media"] = draft["media_requirements"] == expected["media"]
    allowed_constraints = set(expected["constraints"] + expected["media"])
    checks["field_constraints"] = (
        set(expected["constraints"]).issubset(draft["constraints"])
        and set(draft["constraints"]).issubset(allowed_constraints))
    checks["field_budget_unknown"] = draft["budget_unknown"] == (expected["budget_minor"] is None)
    return checks

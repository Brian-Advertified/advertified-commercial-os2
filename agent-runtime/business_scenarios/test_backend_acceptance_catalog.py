"""Guard the owner-required 100-case backend acceptance campaign contract."""
from collections import Counter

from business_scenarios.backend_acceptance_catalog import (
    BACKEND_ACCEPTANCE_SCENARIOS,
    CATEGORY_COUNTS,
    BackendScenarioDefinition,
    BackendScenarioRunRecord,
)


def test_catalogue_has_exact_owner_required_composition() -> None:
    assert len(BACKEND_ACCEPTANCE_SCENARIOS) == 100
    assert len({item.scenario_id for item in BACKEND_ACCEPTANCE_SCENARIOS}) == 100
    actual = Counter(item.category for item in BACKEND_ACCEPTANCE_SCENARIOS)
    assert actual == CATEGORY_COUNTS


def test_catalogue_is_invariant_driven_and_synthetic() -> None:
    for scenario in BACKEND_ACCEPTANCE_SCENARIOS:
        assert scenario.source_inputs["synthetic"] is True
        assert scenario.source_inputs["case"] == scenario.name
        assert scenario.expected_invariants
        codes = [item.code for item in scenario.expected_invariants]
        assert len(codes) == len(set(codes))
        assert all(item.expected is True for item in scenario.expected_invariants)
        assert BackendScenarioDefinition.model_validate(
            scenario.model_dump(mode="python")) == scenario


def test_every_scenario_can_emit_complete_zero_cost_receipt() -> None:
    for scenario in BACKEND_ACCEPTANCE_SCENARIOS:
        receipt = BackendScenarioRunRecord(
            scenario_id=scenario.scenario_id,
            category=scenario.category,
            source_inputs=scenario.source_inputs,
            invariant_results={
                invariant.code: True for invariant in scenario.expected_invariants
            },
            actual_result={"fixture": "contract-only"},
            lifecycle_terminal_state="COMPLETED",
            human_review_points=(),
            unsupported_fact_count=0,
            missing_required_fact_count=0,
            provider="deterministic",
            model="fixture-v1",
            tool_calls=0,
            input_tokens=0,
            output_tokens=0,
            incremental_cost_minor=0,
            cost_currency="USD",
            latency_ms=0,
            retries=0,
            commercial_reconciliation="NOT_APPLICABLE",
            tenant_security_result="PASS",
            passed=True,
        )
        exported = receipt.model_dump(mode="json")
        assert exported["scenario_id"] == scenario.scenario_id
        assert set(exported["invariant_results"]) == {
            invariant.code for invariant in scenario.expected_invariants
        }
        assert exported["incremental_cost_minor"] == 0
        assert exported["provider"] == "deterministic"

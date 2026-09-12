"""Execute catalogue inputs; fixtures supply proposals, production owns validation."""
import pytest

from business_scenarios.backend_acceptance_catalog import BACKEND_ACCEPTANCE_SCENARIOS
from business_scenarios.backend_scenario_executor import execute_case

IMPLEMENTED = [case for case in BACKEND_ACCEPTANCE_SCENARIOS
               if case.category in {"SUPPLIED_BRIEF", "INTELLIGENCE"}]


@pytest.mark.parametrize("scenario", IMPLEMENTED, ids=lambda scenario: scenario.scenario_id)
def test_scenario_executes_real_runtime(scenario):
    receipt = execute_case(scenario)
    assert receipt.passed, receipt.failure_reasons
    assert receipt.execution_status == "EXECUTED"
    assert receipt.incremental_cost_minor == 0
    assert receipt.tool_calls == 0


def test_missing_adapter_cannot_claim_execution_or_zero_unsupported_facts():
    scenario = next(case for case in BACKEND_ACCEPTANCE_SCENARIOS
                    if case.category == "FUNDING_CAMPAIGN_DELIVERY")
    receipt = execute_case(scenario)
    assert not receipt.passed
    assert receipt.execution_status == "NOT_EXECUTED"
    assert receipt.unsupported_fact_count is None
    assert receipt.missing_required_fact_count is None
    assert receipt.tenant_security_result == "NOT_VERIFIED"

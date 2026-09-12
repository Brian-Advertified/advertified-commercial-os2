"""Sequential real-surface scenario orchestration and auditable per-case receipts."""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re
from time import perf_counter

from business_scenarios.backend_acceptance_catalog import (
    BACKEND_ACCEPTANCE_SCENARIOS, CATEGORY_COUNTS, BackendScenarioRunRecord,
)
from business_scenarios.brief_scenario_execution import execute as execute_brief

from business_scenarios.intelligence_scenario_execution import execute as execute_intelligence

RUNNERS = {"SUPPLIED_BRIEF": execute_brief, "INTELLIGENCE": execute_intelligence}
ROOT = Path(__file__).resolve().parents[2]


def execute_case(scenario, api_evidence=()):
    started = perf_counter()
    if scenario.execution_surface != "AGENT_RUNTIME" and api_evidence:
        from business_scenarios.api_scenario_receipts import load_receipt
        try:
            retained = load_receipt(scenario, api_evidence)
            if retained is not None:
                return retained
        except Exception as error:
            return unavailable(scenario, f"API evidence rejected: {error}", started)
    runner = RUNNERS.get(scenario.category)
    if runner is None:
        return unavailable(scenario, "No canonical scenario adapter is implemented.", started)
    try:
        observed = runner(scenario)
    except Exception as error:
        return unavailable(scenario, f"{type(error).__name__}: {error}", started, attempted=True)
    invariants = observed["invariant_results"]
    failures = [name for name, passed in observed["checks"].items() if not passed]
    failures += [name for name, passed in invariants.items() if not passed]
    expected_codes = {item.code for item in scenario.expected_invariants}
    if set(invariants) != expected_codes:
        failures.append("Invariant result keys do not match the scenario definition.")
    usage = observed["output"]["usage"]
    return BackendScenarioRunRecord(
        scenario_id=scenario.scenario_id, category=scenario.category,
        source_inputs={"definition": scenario.source_inputs, "request": observed["source_request"]},
        invariant_results=invariants, actual_result=observed,
        lifecycle_terminal_state=observed["terminal_state"],
        human_review_points=tuple(observed["human_review_points"]),
        unsupported_fact_count=observed["unsupported_fact_count"],
        missing_required_fact_count=observed["missing_required_fact_count"],
        provider=usage["provider"], model=usage["model"], tool_calls=usage["tool_calls"],
        input_tokens=usage["input_tokens"], output_tokens=usage["output_tokens"],
        incremental_cost_minor=usage["incremental_cost_minor"], cost_currency="USD",
        latency_ms=round((perf_counter() - started) * 1000), retries=0,
        commercial_reconciliation=observed["commercial_reconciliation"],
        tenant_security_result=observed["tenant_security_result"],
        passed=not failures, failure_reasons=tuple(dict.fromkeys(failures)),
    )


def unavailable(scenario, reason, started, attempted=False):
    return BackendScenarioRunRecord(
        scenario_id=scenario.scenario_id, category=scenario.category,
        source_inputs=scenario.source_inputs,
        invariant_results={item.code: False for item in scenario.expected_invariants},
        actual_result={"blocker": reason, "measurement": "NOT_AVAILABLE"},
        lifecycle_terminal_state="NOT_OBSERVED", human_review_points=(),
        unsupported_fact_count=None, missing_required_fact_count=None,
        provider="deterministic", model="fixture-v1" if attempted else "NOT_INVOKED", tool_calls=0,
        input_tokens=0, output_tokens=0, incremental_cost_minor=0, cost_currency="USD",
        latency_ms=round((perf_counter() - started) * 1000), retries=0,
        commercial_reconciliation="NOT_VERIFIED", tenant_security_result="NOT_VERIFIED",
        passed=False, execution_status="EXECUTION_FAILED" if attempted else "NOT_EXECUTED",
        failure_reasons=(reason,),
    )


def run_campaign(evidence_name, category=None, api_evidence=()):
    if not re.fullmatch(r"[a-z0-9][a-z0-9-]{2,100}", evidence_name):
        raise ValueError("Use a new lowercase evidence name without a path.")
    destination = ROOT / "artifacts/backend-production-completion" / evidence_name
    destination.mkdir(parents=True, exist_ok=False)
    selected = [case for case in BACKEND_ACCEPTANCE_SCENARIOS
                if category is None or case.category == category]
    receipts = []
    for scenario in selected:
        receipt = execute_case(scenario, api_evidence)
        encoded = receipt.model_dump_json(indent=2) + "\n"
        path = destination / (scenario.scenario_id + ".json")
        path.write_text(encoded, encoding="utf-8")
        receipts.append(dict(
            scenario_id=receipt.scenario_id, passed=receipt.passed,
            execution_status=receipt.execution_status, path=path.name,
            sha256=hashlib.sha256(encoded.encode()).hexdigest(),
        ))
    summary = dict(
        schema="advertified.backend-scenario-execution.v1",
        generated_at=datetime.now(timezone.utc).isoformat(),
        catalogue_count=len(BACKEND_ACCEPTANCE_SCENARIOS), selected=len(selected),
        executed=sum(item["execution_status"] != "NOT_EXECUTED" for item in receipts),
        passed=sum(item["passed"] for item in receipts),
        failed=sum(not item["passed"] for item in receipts),
        not_executed=sum(item["execution_status"] == "NOT_EXECUTED" for item in receipts),
        category=category, receipts=receipts,
    )
    (destination / "summary.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({key: value for key, value in summary.items() if key != "receipts"}))
    return 0 if summary["failed"] == 0 else 1


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--evidence-name", required=True)
    parser.add_argument("--category", choices=tuple(CATEGORY_COUNTS))
    parser.add_argument("--api-evidence", action="append", type=Path, default=[],
                        help="Pinned verifier output directory; reconciles retained execution with api.trx.")
    args = parser.parse_args()
    return run_campaign(args.evidence_name, args.category, args.api_evidence)


if __name__ == "__main__":
    raise SystemExit(main())

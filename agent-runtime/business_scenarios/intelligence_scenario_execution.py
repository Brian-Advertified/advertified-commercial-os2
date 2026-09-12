"""Exercise actual intelligence routes and provenance rejection with deterministic inputs."""
from unittest.mock import patch
from uuid import UUID

from fastapi import HTTPException

from agent_registry import AgentCode
from audience_model_input import audience_model_input
from planning_contracts import AudienceDefinitionSetArtifact
from planning_service import propose_audiences
from runtime_execution import _grounded_bedrock_output
from business_scenarios.intelligence_scenario_fixtures import audience_request, location_request
from business_scenarios.scenario_runtime_http import invoke


def execute(scenario):
    location = "location_basis" in scenario.source_inputs or "geo_evidence" in scenario.source_inputs
    request = location_request(scenario) if location else audience_request(scenario)
    agent = "location_intelligence" if location else "audience_intelligence"
    denied, response = invoke(agent, request.model_dump(mode="json"))
    if response.status_code != 200:
        raise AssertionError(f"Runtime rejected {scenario.scenario_id}: {response.status_code}: {response.text}")
    output = response.json()
    checks = location_checks(request, output) if location else audience_checks(request, output)
    checks["service_authentication"] = denied.status_code == 401
    rejection = None
    if scenario.source_inputs.get("forged_claim"):
        rejection = forged_audience_rejection(request)
        checks["forged_provenance_rejected"] = (
            rejection["stage"] == "GROUNDING_VALIDATION"
            and rejection["provider_acceptance"] == "ACCEPTED")
    required = ("evidence_bindings_valid", "unknown_remains_unknown", "no_unsupported_facts")
    return dict(
        source_request=request.model_dump(mode="json"), output=output,
        checks=checks, rejected_proposal=rejection,
        unsupported_fact_count=sum(not checks[key] for key in required),
        missing_required_fact_count=len({item["field_path"] for item in output["unknowns"] if item["is_blocking"]}),
        invariant_results={key.upper(): checks[key] for key in required},
        terminal_state=output["status"], human_review_points=["INTELLIGENCE_REVIEW"],
        commercial_reconciliation="NOT_APPLICABLE_PROPOSE_ONLY",
        tenant_security_result="RUNTIME_SERVICE_AUTH_VERIFIED" if denied.status_code == 401 else "FAILED",
        fixture_calls=2 if rejection else 1,
    )


def audience_checks(request, output):
    audiences = output["artifact"]["audiences"]
    expected = list(dict.fromkeys(request.planning.audiences))
    approved_ids = {str(value) for value in request.invocation.approved_evidence_item_ids}
    unknown = all(item[field] is None for item in audiences for field in (
        "confidence", "language", "life_stage", "lsm_sem", "need_state", "buying_context"))
    valid = all(set(item["evidence_item_ids"]) <= approved_ids
                and not item["reference_observation_ids"] for item in audiences)
    projection = audience_model_input(request)
    return dict(
        evidence_bindings_valid=valid, unknown_remains_unknown=unknown,
        no_unsupported_facts=unknown and valid and all(
            item["classification"] == "CLIENT_REQUIREMENT" for item in audiences),
        supplied_audiences_retained=[item["name"] for item in audiences] == expected,
        supplied_geography_retained=all(
            set(item["geographies"]) <= set(request.planning.geographies) for item in audiences),
        context_only_evidence_not_promoted=not projection["planning"].get("reference_evidence"),
        review_is_human=output["suggested_next_action"] is None
            or output["suggested_next_action"]["requires_human"],
    )


def location_checks(request, output):
    artifact = output["artifact"]
    supplied = request.model_dump(mode="json")["location"]
    opportunities = artifact["opportunities"]
    allowed = {item["place_id"] for item in supplied["resolved_places"]}
    valid = all(set(item["place_ids"]) <= allowed and not item["reference_observation_ids"]
                for item in opportunities)
    unknown = all(item["classification"] == "HYPOTHESIS" and item["confidence"] is None
                  for item in opportunities)
    return dict(
        evidence_bindings_valid=valid, unknown_remains_unknown=unknown,
        no_unsupported_facts=valid and unknown,
        research_queries_retained=artifact["research_queries"] == supplied["research_plan"]["queries"],
        source_places_retained=artifact["resolved_places"] == supplied["resolved_places"],
        missing_evidence_visible=bool(artifact["evidence_gaps"]),
        no_place_no_opportunity=bool(allowed) or not opportunities,
    )


def forged_audience_rejection(request):
    proposal = propose_audiences(request)
    foreign = UUID("7fd5de8d-7e61-46ec-84ae-2c219a7fb984")
    audience = proposal.artifact.audiences[0].model_copy(update={"evidence_item_ids": (foreign,)})
    proposal = proposal.model_copy(update={"artifact": proposal.artifact.model_copy(
        update={"audiences": (audience,)})})
    with patch("runtime_execution.generate_with_bedrock", return_value=proposal):
        try:
            _grounded_bedrock_output(AgentCode.AUDIENCE_INTELLIGENCE, request,
                                     AudienceDefinitionSetArtifact, "Synthetic deterministic fixture")
        except HTTPException as error:
            if error.status_code == 503:
                return error.detail
            raise
    raise AssertionError("Forged audience provenance was accepted.")

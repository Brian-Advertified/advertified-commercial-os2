import hashlib
import json

from bedrock_multimodal import request_content
from bedrock_schema import source_bound_schema
from supplied_brief_contracts import SuppliedBriefRequest
from supplied_brief_model_input import (
    HISTORY_LOCATOR,
    PRIMARY_LOCATOR,
    build_model_input,
)


def supplied_request(content: str) -> SuppliedBriefRequest:
    digest = hashlib.sha256(content.encode("utf-8")).hexdigest()
    return SuppliedBriefRequest.model_validate_json(json.dumps({
        "operation": "SUPPLIED_BRIEF_UNDERSTANDING",
        "invocation": {
            "schema_version": "1.0.0",
            "tenant_id": "11111111-1111-1111-1111-111111111111",
            "actor_id": "22222222-2222-2222-2222-222222222222",
            "effective_role": "agent_runtime_service",
            "run_id": "33333333-3333-3333-3333-333333333333",
            "step_id": "33333333-3333-3333-3333-333333333333",
            "correlation_id": "33333333-3333-3333-3333-333333333333",
            "agent_code": "brief_drafting",
            "contract_version": "1.0.0",
            "prompt_version": "1.0.0",
            "resource_refs": [{
                "resource_type": "SuppliedBriefInput",
                "resource_id": "33333333-3333-3333-3333-333333333333",
                "version": 1,
            }],
            "approved_evidence_item_ids": [],
            "locale": "und",
            "account_policy_version": "1.0.0",
            "tool_policy": {
                "allowed_tools": [],
                "max_tool_calls": 0,
                "consequence_policy": "PROPOSE_ONLY",
            },
            "provider_policy": {
                "provider": "deterministic",
                "model": "fixture-v1",
                "temperature": 0,
                "timeout_seconds": 30,
                "max_attempts": 1,
                "cost_cap_minor": 0,
                "allow_live": False,
            },
            "resume": {
                "checkpoint_id": None,
                "prior_validated_output_ref": None,
                "prior_usage_ref": None,
            },
        },
        "source": {
            "source_title": "Q4 request",
            "source_content": content,
            "source_hash": digest,
            "clarifications": [{
                "field_path": "budget",
                "value": "ZAR 60,000",
            }],
        },
    }))


def test_model_input_excludes_internal_invocation_metadata() -> None:
    model_input = build_model_input(supplied_request("Promote the launch."))
    wire = json.loads(request_content(
        model_input,
        "fixture-v1",
        frozenset(),
    )[0]["text"])

    assert "invocation" not in wire
    assert "tenant_id" not in wire
    assert "actor_id" not in wire
    assert "provider_policy" not in wire
    assert "tool_policy" not in wire
    assert wire["source_revision"]["source_hash"]
    assert wire["clarifications"][0]["source_locator"] == "clarification:budget"


def test_model_input_separates_quoted_history_without_changing_text() -> None:
    content = (
        "Use social next month.\n\n"
        "On July 12, 2026, John wrote:\n"
        "Defer billboards until 2027."
    )
    segments = build_model_input(supplied_request(content))["source"]["segments"]

    assert [segment["segment_id"] for segment in segments] == [
        PRIMARY_LOCATOR,
        HISTORY_LOCATOR,
    ]
    assert segments[0]["content"] + segments[1]["content"] == content
    assert segments[0]["role"] == "PRIMARY_MESSAGE"
    assert segments[1]["role"] == "QUOTED_HISTORY"


def test_unrecognised_content_remains_one_immutable_primary_segment() -> None:
    content = "Regards,\nSarah\nPlease use Johannesburg."
    segments = build_model_input(supplied_request(content))["source"]["segments"]

    assert len(segments) == 1
    assert segments[0]["content"] == content


def test_output_schema_offers_only_present_source_locators() -> None:
    schema = {
        "type": "object",
        "properties": {
            "evidence": {
                "type": "object",
                "properties": {"source_locator": {"type": "string"}},
            },
        },
    }

    bound = json.loads(source_bound_schema(
        json.dumps(schema),
        supplied_request("Promote the launch."),
    ))
    allowed = bound["properties"]["evidence"]["properties"]["source_locator"]["enum"]

    assert allowed == [
        "supplied:title",
        PRIMARY_LOCATOR,
        "clarification:budget",
    ]
    assert HISTORY_LOCATOR not in allowed

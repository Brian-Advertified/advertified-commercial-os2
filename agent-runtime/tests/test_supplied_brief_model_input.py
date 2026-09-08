import hashlib
import json

from bedrock_multimodal import request_content
from bedrock_schema import source_bound_schema
from bedrock_supplied_brief_output import (
    supplied_brief_schema,
    wrap_supplied_brief_output,
)
from supplied_brief_contracts import SuppliedBriefArtifact, SuppliedBriefRequest
from supplied_brief_model_input import (
    HISTORY_LOCATOR,
    PRIMARY_LOCATOR,
    build_model_input,
)
from supplied_brief_service import (
    _canonical_excerpt,
    _exact_budget,
    canonicalize_grounding,
    validate_grounding,
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


def test_citation_canonicalizer_restores_one_exact_source_line() -> None:
    source = (
        "Objective: Build premium awareness.\n"
        "Media: OOH and DOOH only. Use only digital large-format sites."
    )

    assert _canonical_excerpt(
        source,
        "media ooh and dooh only use only digital large format sites",
    ) == "Media: OOH and DOOH only. Use only digital large-format sites."


def test_citation_canonicalizer_rejects_paraphrase_or_ambiguous_match() -> None:
    assert _canonical_excerpt(
        "Media: Use OOH.\nMedia: Use OOH in Gauteng.",
        "Media use OOH",
    ) is None
    assert _canonical_excerpt(
        "Objective: Build premium awareness.",
        "Grow high-value brand salience.",
    ) is None


def test_exact_budget_converts_major_units_to_minor_units() -> None:
    request = supplied_request(
        "Budget: ZAR 500,000 excluding VAT is a certification assumption."
    )
    request = request.model_copy(update={
        "source": request.source.model_copy(update={"clarifications": ()}),
    })

    assert _exact_budget(request) == ("ZAR", 50_000_000)


def test_supplied_brief_model_returns_only_artifact_and_runtime_wraps_it() -> None:
    payload = {
        "source_hash": "0" * 64,
        "client_name": "Jameson Select",
        "title": "Jameson Select high-SEM digital OOH preview",
        "campaign_mode": "OOH_ONLY",
        "campaign_mode_confidence": 1,
        "requires_human_clarification": False,
        "campaign_mode_rationale": "The source permits only OOH and DOOH.",
        "draft": {
            "business_problem": "Build premium awareness.",
            "objective": "Build premium awareness in high-SEM areas.",
            "audiences": ["Legal-drinking-age adults"],
            "geographies": ["Sandton"],
            "timing": "15 August 2026 to 30 September 2026.",
            "budget_minor": 50_000_000,
            "budget_unknown": False,
            "currency": "ZAR",
            "vat_status": "Excluding VAT",
            "fees_minor": None,
            "media_requirements": ["OOH and DOOH only"],
            "constraints": ["Do not use 3 x 6 sites"],
            "measurement": ["Proof of flight"],
            "facts": [],
            "unknowns": [],
            "assumptions": [],
            "conflicts": [],
        },
        "questions": [],
        "evidence": [{
            "field_path": "mediaRequirements",
            "kind": "SUPPLIED_CLAIM",
            "excerpt": "Media: OOH and DOOH only.",
            "confidence": 1,
            "source_locator": PRIMARY_LOCATOR,
        }],
    }

    schema = json.loads(supplied_brief_schema())
    assert "source_hash" in schema["properties"]
    assert "artifact" not in schema["properties"]

    output = wrap_supplied_brief_output(
        SuppliedBriefArtifact,
        {"artifact": payload},
    )
    assert output.schema_version == "1.0.0"
    assert output.status == "REVIEW_REQUIRED"
    assert output.artifact == SuppliedBriefArtifact.model_validate_json(
        json.dumps(payload)
    )
    assert output.evidence_bindings == ()
    assert output.suggested_next_action.command_code == "ReviewSuppliedBrief"


def _campaign_mode_payload(request: SuppliedBriefRequest, source: str) -> dict:
    return {
        "source_hash": request.source.source_hash,
        "client_name": "Jameson Select",
        "title": "Jameson Select high-SEM digital OOH preview",
        "campaign_mode": None,
        "campaign_mode_confidence": 0,
        "requires_human_clarification": False,
        "campaign_mode_rationale": "The source permits only OOH and DOOH.",
        "draft": {
            "business_problem": "Build premium awareness.",
            "objective": "Build premium awareness in high-SEM areas.",
            "audiences": ["Legal-drinking-age adults"],
            "geographies": ["Sandton"],
            "timing": "Mid August to September.",
            "budget_minor": 6_000_000,
            "budget_unknown": False,
            "currency": "ZAR",
            "vat_status": "Excluding VAT",
            "fees_minor": None,
            "media_requirements": ["OOH and DOOH only"],
            "constraints": ["Use only digital large-format sites"],
            "measurement": ["Proof of flight"],
            "facts": [],
            "unknowns": [],
            "assumptions": [],
            "conflicts": [],
        },
        "questions": [],
        "evidence": [
            {
                "field_path": "mediaRequirements",
                "kind": "SUPPLIED_CLAIM",
                "excerpt": source,
                "confidence": 1,
                "source_locator": PRIMARY_LOCATOR,
            },
        ],
    }


def test_campaign_mode_reuses_exact_compatible_media_requirement_citation() -> None:
    source = "Media: OOH and DOOH only. Use only digital large-format sites."
    request = supplied_request(source)
    payload = _campaign_mode_payload(request, source)
    payload["draft"]["constraints"] = []
    payload["evidence"].append({
        "field_path": "facts",
        "kind": "SUPPLIED_CLAIM",
        "excerpt": "This wording does not exist in the supplied Brief.",
        "confidence": 1,
        "source_locator": PRIMARY_LOCATOR,
    })
    output = wrap_supplied_brief_output(SuppliedBriefArtifact, payload)

    repaired = canonicalize_grounding(request, output)

    campaign_mode = next(
        item for item in repaired.artifact.evidence
        if item.field_path == "campaignMode"
    )
    assert repaired.artifact.campaign_mode == "OOH_ONLY"
    assert campaign_mode.excerpt == source
    assert campaign_mode.source_locator == PRIMARY_LOCATOR
    assert all(item.field_path != "facts" for item in repaired.artifact.evidence)
    assert repaired.artifact.questions == ()
    assert repaired.artifact.requires_human_clarification is False
    assert repaired.artifact.draft.budget_minor == 6_000_000
    assert repaired.artifact.draft.vat_status is None
    assert repaired.artifact.draft.constraints == (source,)
    validate_grounding(request, repaired)

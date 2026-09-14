"""Citation namespace restrictions supplement, never replace, runtime provenance checks."""
import json

import pytest

from bedrock_artifact_output import wrap_artifact_output
from bedrock_output import output_schema
from planning_contracts import AudienceDefinitionSetArtifact
from planning_service import canonicalize_audiences, propose_audiences
from test_intelligence_grounding_boundary import audience_request
from test_location_grounding_boundary import reference_observation


@pytest.mark.parametrize("has_brief_evidence", [True, False])
def test_audience_schema_separates_reference_and_brief_evidence(has_brief_evidence):
    request = audience_request()
    reference = reference_observation().model_copy(update={"activation_policy": "AUDIENCE_SEGMENT_SUPPORT"})
    brief_ids = request.invocation.approved_evidence_item_ids if has_brief_evidence else ()
    request = request.model_copy(update={
        "invocation": request.invocation.model_copy(update={"approved_evidence_item_ids": brief_ids}),
        "planning": request.planning.model_copy(update={"reference_evidence": (reference,)}),
    })
    schema = json.loads(output_schema(request, AudienceDefinitionSetArtifact, None, False, False))
    assert "$defs" not in schema
    assert "targeting_rationale" not in schema["properties"]
    assert "audiences" not in schema["properties"]
    fields = schema["properties"]["audience_1"]["properties"]
    assert fields["name"]["const"] == request.planning.audiences[0]
    assert fields["reference_observation_ids"]["items"]["enum"] == [str(reference.observation_id)]
    if brief_ids:
        assert fields["evidence_item_ids"]["items"]["enum"] == [str(item) for item in brief_ids]
    else:
        assert "evidence_item_ids" not in fields
    assert "geographies" not in fields
    output = propose_audiences(request)
    invalid = output.artifact.audiences[0].model_copy(update={
        "evidence_item_ids": (reference.observation_id,),
    })
    output = output.model_copy(update={"artifact": output.artifact.model_copy(update={
        "audiences": (invalid,),
    })})
    with pytest.raises(ValueError, match="unapproved Brief evidence"):
        canonicalize_audiences(request, output)


def test_context_only_reference_is_not_a_provider_segment_citation_choice():
    request = audience_request()
    reference = reference_observation()
    request = request.model_copy(update={"planning": request.planning.model_copy(update={
        "reference_evidence": (reference,),
    })})
    schema = json.loads(output_schema(request, AudienceDefinitionSetArtifact, None, False, False))
    assert "audiences" not in schema["properties"]
    fields = schema["properties"]["audience_1"]["properties"]
    assert "reference_observation_ids" not in fields
    output = propose_audiences(request)
    audience = output.artifact.audiences[0].model_copy(update={
        "reference_observation_ids": (reference.observation_id,),
    })
    output = output.model_copy(update={"artifact": output.artifact.model_copy(update={
        "audiences": (audience,),
    })})
    with pytest.raises(ValueError, match="only references approved for segment support"):
        canonicalize_audiences(request, output)


def test_provider_projection_keeps_context_only_research_without_expanding_citation_choices():
    from audience_model_input import audience_model_input
    request = audience_request()
    context_only = reference_observation()
    from uuid import UUID
    supported = context_only.model_copy(update={
        "observation_id": UUID("b301e9c3-5a2a-4193-8130-e3dd26d3d606"),
        "activation_policy": "AUDIENCE_SEGMENT_SUPPORT",
    })
    request = request.model_copy(update={"planning": request.planning.model_copy(update={
        "reference_evidence": (context_only, supported),
    })})
    original = request.model_dump_json()
    payload = audience_model_input(request)
    projected = payload["planning"]["reference_evidence"]
    assert [item["observation_id"] for item in projected] == [str(supported.observation_id)]
    context = payload["planning"]["context_research"]
    assert len(context) == 1
    assert "observation_id" not in context[0]
    assert context[0]["metrics"][0]["metric_code"] == context_only.metric_code
    assert "contextual market research only" in payload["reference_policy_note"]
    assert "budget_minor" not in payload["planning"]
    assert "currency" not in payload["planning"]
    assert payload["planning"]["budget"] == {"status": "UNKNOWN"}
    schema = json.loads(output_schema(request, AudienceDefinitionSetArtifact, None, False, False))
    assert "audiences" not in schema["properties"]
    fields = schema["properties"]["audience_1"]["properties"]
    choices = fields["reference_observation_ids"]["items"]["enum"]
    assert choices == [str(supported.observation_id)]
    assert request.model_dump_json() == original


def test_audience_provider_accepts_fixed_live_slots_and_rebuilds_collection():
    request = audience_request()
    provider_payload = {
        "audience_1": {
            "name": request.planning.audiences[0],
            "need_state": "Hypothesis: supplied objective may create a relevant need.",
            "buying_context": "Hypothesis: buying context requires validation.",
            "is_target": True,
        },
        "positioning_statement": "Hypothesis: bounded positioning direction.",
    }
    output = wrap_artifact_output(AudienceDefinitionSetArtifact, provider_payload, request)
    canonical = canonicalize_audiences(request, output)
    assert canonical.artifact.audiences[0].name == request.planning.audiences[0]
    assert canonical.artifact.audiences[0].is_target is True
    assert canonical.artifact.positioning_statement == "Hypothesis: bounded positioning direction."


def test_audience_provider_ignores_known_misplaced_positioning_statement_inside_legacy_items():
    request = audience_request()
    provider_payload = {
        "audiences": [{
            "name": request.planning.audiences[0],
            "need_state": "Hypothesis: supplied objective may create a relevant need.",
            "buying_context": "Hypothesis: buying context requires validation.",
            "evidence_item_ids": [],
            "reference_observation_ids": [],
            "confidence": None,
            "is_target": True,
            "positioning_statement": "Hypothesis: misplaced provider advisory copy.",
        }],
        "targeting_rationale": "Provider rationale that Advertified will canonicalize.",
        "positioning_statement": "Hypothesis: valid root positioning direction.",
    }
    output = wrap_artifact_output(AudienceDefinitionSetArtifact, provider_payload, request)
    canonical = canonicalize_audiences(request, output)
    assert canonical.artifact.audiences[0].name == request.planning.audiences[0]
    assert canonical.artifact.positioning_statement == "Hypothesis: valid root positioning direction."

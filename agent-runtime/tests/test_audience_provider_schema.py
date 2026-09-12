"""Citation namespace restrictions supplement, never replace, runtime provenance checks."""
import json

import pytest

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
    fields = schema["$defs"]["AudienceDefinition"]["properties"]
    assert fields["reference_observation_ids"]["items"]["enum"] == [str(reference.observation_id)]
    if brief_ids:
        assert fields["evidence_item_ids"]["items"]["enum"] == [str(item) for item in brief_ids]
    else:
        assert fields["evidence_item_ids"]["maxItems"] == 0
    assert fields["geographies"]["items"]["enum"] == list(request.planning.geographies)
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
    assert schema["$defs"]["AudienceDefinition"]["properties"]["reference_observation_ids"]["maxItems"] == 0
    output = propose_audiences(request)
    audience = output.artifact.audiences[0].model_copy(update={
        "reference_observation_ids": (reference.observation_id,),
    })
    output = output.model_copy(update={"artifact": output.artifact.model_copy(update={
        "audiences": (audience,),
    })})
    with pytest.raises(ValueError, match="only references approved for segment support"):
        canonicalize_audiences(request, output)


def test_provider_projection_withholds_context_only_ids_without_changing_canonical_request():
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
    assert str(context_only.observation_id) not in json.dumps(payload)
    assert payload["planning"]["reference_evidence"][0]["observation_id"] == str(supported.observation_id)
    assert "1 supplied context-only" in payload["reference_policy_note"]
    assert request.model_dump_json() == original

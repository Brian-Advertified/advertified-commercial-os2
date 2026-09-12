"""Provider proposals cannot replace canonical Location research or forge citations."""
import json

import pytest

from bedrock_failure import BedrockProviderError
from bedrock_output import decode_generated_output, output_schema
from location_intelligence_contracts import LocationIntelligenceArtifact
from location_intelligence_service import synthesize_location
from location_intelligence_validation import (
    canonicalize_location_synthesis,
    validate_location_synthesis_grounding,
)
from test_location_grounding_boundary import query, synthesis_request, synthesis_output


def decode(request, proposal):
    response = {"output": {"message": {"content": [{"toolUse": {
        "name": "submit_advertified_result", "input": proposal,
    }}]}}}
    return decode_generated_output(
        response, LocationIntelligenceArtifact, None, False, False, request,
        synthesize_location(request).usage,
    )


@pytest.mark.parametrize("empty", [True, False])
def test_provider_schema_and_decoder_preserve_request_owned_facts(empty):
    request = synthesis_request()
    if empty:
        request = request.model_copy(update={"location": request.location.model_copy(update={
            "research_plan": request.location.research_plan.model_copy(update={"queries": ()}),
            "resolved_places": (),
        })})
    original = request.model_dump(mode="json")
    schema = json.loads(output_schema(request, LocationIntelligenceArtifact, None, False, False))
    assert set(schema["properties"]) == {"summary", "opportunities", "evidence_gaps"}
    assert schema["additionalProperties"] is False
    fields = schema["$defs"]["LocationOpportunityArea"]["properties"]
    assert fields["geography"]["enum"] == ["Unseen district"]
    assert fields["reference_observation_ids"]["maxItems"] == 0
    assert fields["classification"]["enum"] == ["HYPOTHESIS"]
    assert fields["confidence"] == {"type": "null"}
    if empty:
        assert fields["place_ids"]["maxItems"] == 0
    else:
        assert fields["place_ids"]["items"]["enum"] == ["fixture:place-1"]
    if empty:
        assert schema["properties"]["opportunities"]["maxItems"] == 0
    output = decode(request, {"summary": "Further evidence is required.", "opportunities": []})
    validate_location_synthesis_grounding(request, output)
    assert output.artifact.research_queries == request.location.research_plan.queries
    assert output.artifact.resolved_places == request.location.resolved_places
    assert output.artifact.evidence_gaps == request.location.research_plan.evidence_gaps
    assert request.model_dump(mode="json") == original


@pytest.mark.parametrize("field", ["research_queries", "resolved_places", "unknown_property"])
def test_provider_cannot_echo_or_forge_immutable_fields(field):
    request = synthesis_request()
    raw = {"summary": "Further evidence is required.", "opportunities": [], field: []}
    with pytest.raises(BedrockProviderError) as failure:
        decode(request, raw)
    assert failure.value.stage == "TYPED_CONTRACT"
    assert failure.value.rejected_output == raw


def test_composed_artifact_still_rejects_forged_opportunity_provenance():
    request = synthesis_request()
    area = synthesis_output(request).artifact.opportunities[0].model_dump(mode="json")
    area["place_ids"] = ["fixture:foreign-place"]
    output = decode(request, {"summary": "Untrusted proposed area.", "opportunities": [area]})
    with pytest.raises(ValueError, match="not resolved"):
        canonicalize_location_synthesis(request, output)


def test_empty_research_plan_still_rejects_changed_canonical_queries():
    original = synthesis_request()
    request = original.model_copy(update={"location": original.location.model_copy(update={
        "research_plan": original.location.research_plan.model_copy(update={"queries": ()}),
        "resolved_places": (),
    })})
    output = synthesize_location(request)
    altered = output.model_copy(update={"artifact": output.artifact.model_copy(update={
        "research_queries": (query(),),
    })})
    with pytest.raises(ValueError, match="changed the approved research plan"):
        validate_location_synthesis_grounding(request, altered)

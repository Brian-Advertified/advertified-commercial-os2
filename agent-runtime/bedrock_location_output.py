"""Provider synthesis proposals cannot replace request-owned Location research facts."""
from __future__ import annotations

import json

from bedrock_artifact_output import wrap_artifact_output
from location_intelligence_contracts import (
    LocationIntelligenceArtifact,
    LocationSynthesisProposal,
    LocationSynthesisRequest,
)

from location_intelligence_validation import CONTEXT_ONLY_POLICIES, supported_opportunity_geographies


def location_synthesis_schema(request: LocationSynthesisRequest) -> str:
    schema = LocationSynthesisProposal.model_json_schema()
    fields = schema["$defs"]["LocationOpportunityArea"]["properties"]
    fields["geography"]["enum"] = list(supported_opportunity_geographies(request))
    for field, identifiers in (
        ("place_ids", [item.place_id for item in request.location.resolved_places]),
        ("reference_observation_ids", [str(item.observation_id) for item in request.location.reference_evidence]),
    ):
        if identifiers:
            fields[field]["items"]["enum"] = identifiers
        else:
            fields[field]["maxItems"] = 0
    if not any(item.activation_policy not in CONTEXT_ONLY_POLICIES
               for item in request.location.reference_evidence):
        fields["classification"]["enum"] = ["HYPOTHESIS"]
        fields["confidence"] = {"type": "null"}
    if not request.location.resolved_places and not request.location.reference_evidence:
        schema["properties"]["opportunities"]["maxItems"] = 0
    return json.dumps(schema, separators=(",", ":"))


def wrap_location_synthesis_output(payload: object, request: LocationSynthesisRequest):
    if isinstance(payload, dict) and set(payload) == {"artifact"}:
        payload = payload["artifact"]
    # Strict validation occurs before composition: even echoed immutable fields are forbidden.
    proposal = LocationSynthesisProposal.model_validate_json(json.dumps(payload))
    context = request.location
    artifact = LocationIntelligenceArtifact(
        **proposal.model_dump(exclude={"evidence_gaps"}),
        research_queries=context.research_plan.queries,
        resolved_places=context.resolved_places,
        evidence_gaps=tuple(dict.fromkeys((
            *context.research_plan.evidence_gaps, *proposal.evidence_gaps,
        ))),
    )
    return wrap_artifact_output(
        LocationIntelligenceArtifact, artifact.model_dump(mode="json"), request,
    )

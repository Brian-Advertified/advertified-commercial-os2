"""Keep Brief evidence and governed reference observations in distinct provider choices."""
import json

from planning_contracts import AudienceAgentRequest, AudienceDefinitionSetArtifact
from planning_service import segment_support_reference_ids


def audience_schema(request: AudienceAgentRequest) -> str:
    schema = AudienceDefinitionSetArtifact.model_json_schema()
    fields = schema["$defs"]["AudienceDefinition"]["properties"]
    for name, values in (
        ("evidence_item_ids", request.invocation.approved_evidence_item_ids),
        ("reference_observation_ids", segment_support_reference_ids(request)),
    ):
        fields[name]["description"] = (
            "Cite only the exact IDs in this field's supplied namespace; "
            "Brief evidence IDs and reference-observation IDs are not interchangeable."
        )
        if values:
            fields[name]["items"]["enum"] = [str(value) for value in values]
        else:
            fields[name]["maxItems"] = 0
    fields["geographies"]["items"]["enum"] = list(request.planning.geographies)
    return json.dumps(schema, separators=(",", ":"))

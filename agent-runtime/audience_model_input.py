"""Project only segment-authorized reference records to the Audience provider."""
from planning_contracts import AudienceAgentRequest
from planning_service import segment_support_reference_ids


def audience_model_input(request: AudienceAgentRequest) -> dict:
    payload = request.model_dump(mode="json", exclude={"invocation"})
    allowed = {str(value) for value in segment_support_reference_ids(request)}
    references = payload["planning"]["reference_evidence"]
    payload["planning"]["reference_evidence"] = [
        item for item in references if item["observation_id"] in allowed
    ]
    omitted = len(references) - len(payload["planning"]["reference_evidence"])
    if omitted:
        payload["reference_policy_note"] = (
            f"{omitted} supplied context-only observations cannot support audience segments "
            "and are withheld from this provider projection. Their absence does not establish "
            "any audience identity, traits, presence or behaviour. Preserve research gaps."
        )
    return payload

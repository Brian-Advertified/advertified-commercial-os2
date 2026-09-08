"""Approved, audience-bound structured evidence; never inferred from a persona name."""
from typing import Annotated
from uuid import UUID

from pydantic import Field
from contracts import ContractModel
from master_data_codes import EvidenceClassifications


class AudienceEvidenceFact(ContractModel):
    evidence_item_id: UUID | None = None
    brief_version_id: UUID | None = None
    audience_name: Annotated[str, Field(min_length=1, max_length=300)]
    language: Annotated[str | None, Field(max_length=100)] = None
    life_stage: Annotated[str | None, Field(max_length=200)] = None
    lsm_sem: Annotated[str | None, Field(max_length=100)] = None
    lsm_sem_taxonomy: Annotated[str | None, Field(max_length=200)] = None
    lsm_sem_taxonomy_version: Annotated[str | None, Field(max_length=100)] = None
    need_state: Annotated[str | None, Field(max_length=1000)] = None
    buying_context: Annotated[str | None, Field(max_length=500)] = None
    message_context: Annotated[str | None, Field(max_length=200)] = None
    moment_context: Annotated[str | None, Field(max_length=200)] = None


def approved_facts(request, name):
    approved = set(request.invocation.approved_evidence_item_ids)
    return tuple(item for item in (request.planning.audience_evidence or ())
                 if (item.evidence_item_id in approved or
                     item.brief_version_id == request.planning.brief_version_id)
                 and item.audience_name.casefold() == name.casefold())


def supported_values(request, name):
    facts = approved_facts(request, name)
    fields = ("language", "life_stage", "lsm_sem", "lsm_sem_taxonomy",
              "lsm_sem_taxonomy_version", "need_state", "buying_context",
              "message_context", "moment_context")
    values = {}
    for field in fields:
        supplied = {getattr(item, field).strip() for item in facts
                    if getattr(item, field) and getattr(item, field).strip()}
        values[field] = next(iter(supplied)) if len(supplied) == 1 else None
    if not all(values[key] for key in ("lsm_sem", "lsm_sem_taxonomy", "lsm_sem_taxonomy_version")):
        for key in ("lsm_sem", "lsm_sem_taxonomy", "lsm_sem_taxonomy_version"):
            values[key] = None
    return values, tuple(dict.fromkeys(item.evidence_item_id for item in facts if item.evidence_item_id))


def grounded_audience(request, item):
    values, evidence_ids = supported_values(request, item.name)
    context = [values.pop("buying_context")]
    for field, label in (("message_context", "Message context"), ("moment_context", "Moment")):
        value = values.pop(field)
        if value:
            context.append(f"{label}: {value}")
    need = values.pop("need_state")
    return item.model_copy(update={
        **values,
        "need_state": need or item.need_state,
        "buying_context": " · ".join(value for value in context if value) or item.buying_context,
        "lsm_sem_mandatory": False,
        "classification": EvidenceClassifications.INFERENCE.value if approved_facts(request, item.name) else item.classification,
        "evidence_item_ids": evidence_ids or request.invocation.approved_evidence_item_ids,
    })

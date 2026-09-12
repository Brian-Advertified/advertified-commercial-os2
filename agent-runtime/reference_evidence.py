"""Governed aggregate reference observations shared by intelligence services."""

from typing import Annotated
from uuid import UUID

from pydantic import Field

from contracts import ContractModel


class ReferenceObservation(ContractModel):
    observation_id: UUID
    source_title: Annotated[str, Field(min_length=1, max_length=500)]
    measurement_period: Annotated[str, Field(min_length=1, max_length=100)]
    geography_level: Annotated[str, Field(min_length=1, max_length=100)]
    geography_code: Annotated[str, Field(min_length=1, max_length=100)]
    geography_name: Annotated[str, Field(min_length=1, max_length=300)]
    dimensions: dict[str, Annotated[str, Field(min_length=1, max_length=500)]]
    metric_code: Annotated[str, Field(min_length=1, max_length=150)]
    metric_value: float
    metric_unit: Annotated[str, Field(min_length=1, max_length=100)]
    stability_code: Annotated[str, Field(min_length=1, max_length=100)]
    sensitivity_code: Annotated[str, Field(min_length=1, max_length=100)]
    activation_policy: Annotated[str, Field(min_length=1, max_length=100)]
    evidence_notes: tuple[Annotated[str, Field(min_length=1, max_length=1_000)], ...] = ()

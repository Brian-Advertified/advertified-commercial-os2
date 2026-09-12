"""Approved strategy facts supplied by the canonical planning API."""

from datetime import date
from typing import Annotated
from uuid import UUID

from pydantic import Field, PositiveInt, model_validator

from contracts import ContractModel, StableCode


class InventoryStrategyAudience(ContractModel):
    id: UUID
    name: Annotated[str, Field(min_length=1, max_length=300)]
    need_state: Annotated[str | None, Field(max_length=1_000)] = None
    buying_context: Annotated[str | None, Field(max_length=1_000)] = None
    geographies: tuple[str, ...]
    classification: StableCode
    exclusions: tuple[str, ...]
    evidence_item_ids: tuple[UUID, ...]


class InventoryStrategyPeriod(ContractModel):
    start: date
    end: date

    @model_validator(mode="after")
    def validate_period(self):
        if self.end < self.start:
            raise ValueError("The approved running period must end on or after its start.")
        return self


class InventoryStrategyAllocation(ContractModel):
    channel: StableCode
    budget_minor: Annotated[int, Field(ge=0)]
    role: Annotated[str, Field(min_length=1, max_length=1_000)]
    running_periods: tuple[InventoryStrategyPeriod, ...]


class InventoryStrategyContext(ContractModel):
    audience_artifact_id: UUID
    audience_artifact_version: PositiveInt
    media_strategy_artifact_id: UUID
    media_strategy_artifact_version: PositiveInt
    media_mix_version_id: UUID
    media_mix_version: PositiveInt
    objective: Annotated[str, Field(min_length=1, max_length=4_000)]
    targeting_rationale: Annotated[str | None, Field(max_length=4_000)] = None
    positioning_statement: Annotated[str | None, Field(max_length=4_000)] = None
    audiences: Annotated[tuple[InventoryStrategyAudience, ...], Field(min_length=1)]
    allocations: Annotated[tuple[InventoryStrategyAllocation, ...], Field(min_length=1)]

    @model_validator(mode="after")
    def validate_unique_inputs(self):
        audience_ids = [item.id for item in self.audiences]
        channels = [item.channel for item in self.allocations]
        if len(audience_ids) != len(set(audience_ids)) or len(channels) != len(set(channels)):
            raise ValueError("Approved strategy audiences and allocated channels must be unique.")
        return self

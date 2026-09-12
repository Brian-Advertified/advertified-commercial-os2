"""Canonical supplied-measurement assessments; these are not delivery forecasts."""

from decimal import Decimal
from typing import Annotated

from pydantic import Field

from contracts import ContractModel

NonNegativeDecimal = Annotated[Decimal, Field(ge=0)]
NonNegativeInteger = Annotated[int, Field(ge=0)]


class InventoryDigitalExposureFacts(ContractModel):
    spot_length_seconds: NonNegativeInteger | None = None
    slot_length_seconds: NonNegativeInteger | None = None
    loop_length_seconds: NonNegativeInteger | None = None
    plays_per_loop: NonNegativeInteger | None = None
    loop_share_percent: Annotated[Decimal | None, Field(ge=0, le=100)] = None


class PlannerAudienceContextFacts(ContractModel):
    name: str
    need_state: str | None = None
    buying_context: str | None = None


class InventoryPlannerReasoningFacts(ContractModel):
    planned_channel_role: str | None = None
    target_contexts: tuple[PlannerAudienceContextFacts, ...]
    required_places_matched: NonNegativeInteger
    required_places_total: NonNegativeInteger
    has_measured_target_audience: bool
    review_questions: tuple[str, ...]
    supported_reasons: tuple[str, ...]
    buying_warnings: tuple[str, ...]


class InventoryBuyAssessmentFacts(ContractModel):
    campaign_supplier_cost_minor: NonNegativeInteger | None = None
    currency: str | None = None
    reach: NonNegativeDecimal | None = None
    impressions: NonNegativeDecimal | None = None
    average_frequency: NonNegativeDecimal | None = None
    cost_per_thousand_impressions_minor: NonNegativeDecimal | None = None
    cost_per_person_reached_minor: NonNegativeDecimal | None = None
    universe: str | None = None
    measurement_period: str | None = None
    measurement_source: str | None = None
    methodology: str | None = None
    is_target_audience: bool
    digital_exposure: InventoryDigitalExposureFacts | None = None
    evidence_gaps: tuple[str, ...]
    planner_reasoning: InventoryPlannerReasoningFacts | None = None

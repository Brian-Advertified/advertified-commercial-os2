"""Compact provider contract for governed Media Strategy channel choices."""
from __future__ import annotations

import json
from decimal import Decimal
from typing import Annotated, Literal

from pydantic import Field, field_validator

from contracts import ContractModel, StableCode
from media_presentation import CHANNEL_LABELS
from media_strategy_contracts import MediaStrategyRequest


class MediaStrategyProviderRecommendation(ContractModel):
    channel: StableCode
    role: Annotated[str, Field(min_length=1, max_length=1_000)]
    classification: Literal["INFERENCE", "HYPOTHESIS"]
    budget_guidance_percent: Annotated[Decimal | None, Field(ge=0, le=100)] = None

    @field_validator("budget_guidance_percent", mode="before")
    @classmethod
    def normalize_budget_guidance(cls, value):
        if value is None:
            return None
        if isinstance(value, str) and value.strip().casefold() == "null":
            return None
        if isinstance(value, (int, float, str)):
            return Decimal(str(value))
        return value


class MediaStrategyProviderArtifact(ContractModel):
    channel_recommendations: Annotated[
        tuple[MediaStrategyProviderRecommendation, ...],
        Field(min_length=1, max_length=20),
    ]
    excluded_channels: tuple[StableCode, ...] = ()


def media_strategy_schema(request: MediaStrategyRequest) -> str:
    """Expose only provider-owned strategic choices, not canonical prose/evidence fields."""
    schema = MediaStrategyProviderArtifact.model_json_schema()
    context = request.media_strategy
    channels = list(context.available_channels)
    labels = "; ".join(f"{code}: {CHANNEL_LABELS.get(code, code)}" for code in channels)
    fields = schema["$defs"]["MediaStrategyProviderRecommendation"]["properties"]
    fields["channel"]["enum"] = channels
    fields["channel"]["description"] = (
        f"Exact registry code. {labels}. Choose only channels that satisfy the approved media requirements."
    )
    fields["role"]["description"] = (
        "Describe only the proposed communication task for this channel. Do not claim reach, footfall, audience "
        "presence, affinity, traffic, effectiveness, inventory availability, supplier rates or specific placements."
    )
    schema["properties"]["excluded_channels"]["items"]["enum"] = channels
    schema["properties"]["channel_recommendations"]["description"] = (
        "Required JSON array. Return at least one permitted channel recommendation. Do not return this array as a string."
    )
    if context.budget_minor is None:
        fields["budget_guidance_percent"] = {"type": "null"}
    else:
        fields["budget_guidance_percent"] = {
            "type": "number",
            "minimum": 0,
            "maximum": 100,
            "description": (
                "Strategic percentage guidance. Supply a value for every returned channel and make the returned "
                "recommendations total exactly 100."
            ),
        }
        recommendation = schema["$defs"]["MediaStrategyProviderRecommendation"]
        recommendation["required"] = list(dict.fromkeys(
            (*recommendation["required"], "budget_guidance_percent")
        ))
    return json.dumps(schema, separators=(",", ":"))

"""Strict proposal-stage Creative Intelligence contracts."""

from __future__ import annotations

from datetime import date
from typing import Annotated, Literal
from uuid import UUID

from pydantic import Field, model_validator

from contracts import AgentInvocationEnvelope, ContractModel
from master_data_codes import (
    AssetRightsStatuses,
    AssetTypes,
    CreativeTextRoles,
    CreativeWarningTypes,
)


class CreativeBriefContext(ContractModel):
    brief_version_id: UUID
    client_name: Annotated[str, Field(min_length=1, max_length=200)]
    objective: Annotated[str, Field(min_length=1, max_length=2_000)]
    audiences: tuple[Annotated[str, Field(min_length=1, max_length=500)], ...]
    geographies: tuple[Annotated[str, Field(min_length=1, max_length=300)], ...]
    campaign_start: date | None = None
    campaign_end: date | None = None


class CreativeAssetInput(ContractModel):
    id: UUID
    asset_type: Literal[
        AssetTypes.LOGO.value,
        AssetTypes.PRODUCT_IMAGE.value,
        AssetTypes.CREATIVE_FILE.value,
        AssetTypes.BRAND_REFERENCE.value,
    ]
    object_key: Annotated[str, Field(min_length=1, max_length=1_000)]
    source_document_id: UUID
    source_locator: Annotated[str, Field(min_length=1, max_length=500)]
    rights_status: Literal[
        AssetRightsStatuses.APPROVED.value,
        AssetRightsStatuses.UNKNOWN.value,
        AssetRightsStatuses.RESTRICTED.value,
    ]
    product_name: Annotated[str, Field(min_length=1, max_length=300)] | None = None
    evidence_item_ids: tuple[UUID, ...] = ()


class CreativeProductInput(ContractModel):
    name: Annotated[str, Field(min_length=1, max_length=300)]
    category: Annotated[str, Field(min_length=1, max_length=200)]
    asset_ids: Annotated[tuple[UUID, ...], Field(min_length=1)]
    evidence_item_ids: Annotated[tuple[UUID, ...], Field(min_length=1)]
    current_price_minor: Annotated[int, Field(ge=0)] | None = None
    currency: Annotated[str, Field(min_length=3, max_length=3)] | None = None
    offer_valid_from: date | None = None
    offer_valid_until: date | None = None

    @model_validator(mode="after")
    def validate_offer(self) -> CreativeProductInput:
        if self.current_price_minor is not None and self.currency is None:
            raise ValueError("A supplied product price requires a currency.")
        if (
            self.offer_valid_from is not None
            and self.offer_valid_until is not None
            and self.offer_valid_from > self.offer_valid_until
        ):
            raise ValueError("Offer validity start cannot be after its end.")
        return self


class CreativeFormatInput(ContractModel):
    channel: Annotated[str, Field(min_length=1, max_length=100)]
    format_code: Annotated[str, Field(min_length=1, max_length=100)]
    width: Annotated[int, Field(gt=0, le=20_000)]
    height: Annotated[int, Field(gt=0, le=20_000)]


class CreativeAgentRequest(ContractModel):
    invocation: AgentInvocationEnvelope
    brief: CreativeBriefContext
    brand_notes: tuple[Annotated[str, Field(min_length=1, max_length=500)], ...] = ()
    assets: Annotated[tuple[CreativeAssetInput, ...], Field(min_length=1)]
    products: tuple[CreativeProductInput, ...]
    formats: Annotated[
        tuple[CreativeFormatInput, ...],
        Field(min_length=1, max_length=12),
    ]

    @model_validator(mode="after")
    def validate_references(self) -> CreativeAgentRequest:
        asset_ids = {asset.id for asset in self.assets}
        missing = {
            asset_id
            for product in self.products
            for asset_id in product.asset_ids
            if asset_id not in asset_ids
        }
        if missing:
            raise ValueError("Every product asset reference must exist in the request.")
        approved_ids = set(self.invocation.approved_evidence_item_ids)
        referenced_ids = {
            evidence_id
            for product in self.products
            for evidence_id in product.evidence_item_ids
        } | {
            evidence_id
            for asset in self.assets
            for evidence_id in asset.evidence_item_ids
        }
        if not referenced_ids.issubset(approved_ids):
            raise ValueError("Creative inputs may reference only approved evidence items.")
        return self


class CreativeWarning(ContractModel):
    code: Literal[
        CreativeWarningTypes.OFFER_OUTSIDE_CAMPAIGN.value,
        CreativeWarningTypes.OFFER_VALIDITY_UNKNOWN.value,
        CreativeWarningTypes.ASSET_RIGHTS_UNCONFIRMED.value,
        CreativeWarningTypes.NO_APPROVED_PRODUCT_IMAGE.value,
    ]
    message: Annotated[str, Field(min_length=1, max_length=500)]
    product_name: Annotated[str, Field(min_length=1, max_length=300)] | None = None


class CreativeTextElement(ContractModel):
    role: Literal[
        CreativeTextRoles.HEADLINE.value,
        CreativeTextRoles.BODY.value,
        CreativeTextRoles.CTA.value,
        CreativeTextRoles.PRICE.value,
        CreativeTextRoles.DISCLOSURE.value,
    ]
    text: Annotated[str, Field(min_length=1, max_length=1_000)]
    verified: bool


class CreativeChannelConcept(ContractModel):
    channel: str
    format_code: str
    width: int
    height: int
    purpose: Annotated[str, Field(min_length=1, max_length=500)]
    visual_instruction: Annotated[str, Field(min_length=1, max_length=2_000)]
    source_asset_ids: tuple[UUID, ...]
    preserve_supplied_products: Literal[True]
    text_elements: Annotated[
        tuple[CreativeTextElement, ...],
        Field(min_length=2),
    ]


class CreativeTerritory(ContractModel):
    name: Annotated[str, Field(min_length=1, max_length=200)]
    rationale: Annotated[str, Field(min_length=1, max_length=1_000)]
    product_names: tuple[str, ...]
    audience_variants: tuple[str, ...]
    channel_concepts: Annotated[
        tuple[CreativeChannelConcept, ...],
        Field(min_length=1),
    ]


class CreativeConceptSetArtifact(ContractModel):
    brief_version_id: UUID
    territories: Annotated[
        tuple[CreativeTerritory, ...],
        Field(min_length=1, max_length=3),
    ]
    warnings: tuple[CreativeWarning, ...]
    disclosure: Literal[
        "Illustrative creative concept. Final offers, pricing, artwork and publication "
        "remain subject to approval."
    ]

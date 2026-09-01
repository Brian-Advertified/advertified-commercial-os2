"""Zero-cost Creative Intelligence proposal generation from approved source assets."""

from __future__ import annotations

from decimal import Decimal
from uuid import UUID

from contracts import (
    AgentOutputEnvelope,
    ConfidenceAssessment,
    EvidenceBinding,
    OutputStatus,
    ProviderUsage,
    SuggestedNextAction,
    UnknownItem,
)
from creative_contracts import (
    CreativeAgentRequest,
    CreativeAssetInput,
    CreativeChannelConcept,
    CreativeConceptSetArtifact,
    CreativeProductInput,
    CreativeTerritory,
    CreativeTextElement,
    CreativeWarning,
)
from master_data_codes import (
    CURRENCY_MINOR_UNIT_DIGITS,
    AssetRightsStatuses,
    AssetTypes,
    CreativeTextRoles,
    CreativeWarningTypes,
)

DISCLOSURE = (
    "Illustrative creative concept. Final offers, pricing, artwork and publication "
    "remain subject to approval."
)


def generate_creative_concepts(
    request: CreativeAgentRequest,
) -> AgentOutputEnvelope[CreativeConceptSetArtifact]:
    approved_assets = _approved_assets(request.assets)
    selected_products = request.products[:3]
    warnings = _warnings(request, approved_assets, selected_products)
    concepts = tuple(
        _channel_concept(request, product, approved_assets, media_format)
        for product in selected_products[:1]
        for media_format in request.formats[:4]
    )
    if not concepts:
        concepts = tuple(
            _brand_only_concept(request, media_format)
            for media_format in request.formats[:4]
        )
    territory = CreativeTerritory(
        name=_territory_name(selected_products),
        rationale=_territory_rationale(request, selected_products),
        product_names=tuple(product.name for product in selected_products),
        audience_variants=request.brief.audiences,
        channel_concepts=concepts,
    )
    artifact = CreativeConceptSetArtifact(
        brief_version_id=request.brief.brief_version_id,
        territories=(territory,),
        warnings=warnings,
        disclosure=DISCLOSURE,
    )
    evidence_ids = _evidence_ids(selected_products, approved_assets)
    unknowns = _unknowns(request, selected_products, approved_assets)
    return AgentOutputEnvelope(
        schema_version="1.0.0",
        status=OutputStatus.REVIEW_REQUIRED,
        artifact=artifact,
        evidence_bindings=_bindings(evidence_ids),
        unknowns=unknowns,
        assumptions=(),
        confidence=(ConfidenceAssessment(
            field_path="artifact.territories",
            confidence=Decimal("0.80") if evidence_ids else Decimal("0.40"),
        ),),
        objections=(),
        rationale=(
            "The concept uses only approved supplied assets and verified commercial text; "
            "human review is required before it can appear in a proposal."
        ),
        suggested_next_action=SuggestedNextAction(
            command_code="ReviewCreativeConcept",
            requires_human=True,
        ),
        usage=_usage(),
    )


def _approved_assets(
    assets: tuple[CreativeAssetInput, ...],
) -> dict[UUID, CreativeAssetInput]:
    return {
        asset.id: asset
        for asset in assets
        if asset.rights_status == AssetRightsStatuses.APPROVED.value
    }


def _channel_concept(
    request: CreativeAgentRequest,
    product: CreativeProductInput,
    approved_assets: dict[UUID, CreativeAssetInput],
    media_format,
) -> CreativeChannelConcept:
    product_assets = tuple(
        asset_id for asset_id in product.asset_ids if asset_id in approved_assets
    )
    text = [
        CreativeTextElement(
            role=CreativeTextRoles.HEADLINE.value,
            text=f"Discover {product.name}",
            verified=False,
        ),
        CreativeTextElement(
            role=CreativeTextRoles.BODY.value,
            text=f"Explore {product.category} from {request.brief.client_name}.",
            verified=True,
        ),
        CreativeTextElement(
            role=CreativeTextRoles.CTA.value,
            text=f"Enquire with {request.brief.client_name}",
            verified=False,
        ),
    ]
    price = _verified_price(request, product)
    if price is not None:
        text.append(CreativeTextElement(
            role=CreativeTextRoles.PRICE.value,
            text=price,
            verified=True,
        ))
    text.append(CreativeTextElement(
        role=CreativeTextRoles.DISCLOSURE.value,
        text=DISCLOSURE,
        verified=True,
    ))
    return CreativeChannelConcept(
        channel=media_format.channel,
        format_code=media_format.format_code,
        width=media_format.width,
        height=media_format.height,
        purpose=f"Show the supplied {product.name} clearly and drive an enquiry.",
        visual_instruction=(
            f"Use only the supplied product assets for {product.name}. Keep the product "
            "recognisable and prominent; do not replace or redesign it."
        ),
        source_asset_ids=product_assets,
        preserve_supplied_products=True,
        text_elements=tuple(text),
    )


def _brand_only_concept(request: CreativeAgentRequest, media_format) -> CreativeChannelConcept:
    logo_ids = tuple(
        asset.id
        for asset in request.assets
        if asset.asset_type == AssetTypes.LOGO.value
        and asset.rights_status == AssetRightsStatuses.APPROVED.value
    )
    return CreativeChannelConcept(
        channel=media_format.channel,
        format_code=media_format.format_code,
        width=media_format.width,
        height=media_format.height,
        purpose="Present the approved campaign direction without inventing a product offer.",
        visual_instruction=(
            "Use only approved brand assets; leave missing product imagery unresolved."
        ),
        source_asset_ids=logo_ids,
        preserve_supplied_products=True,
        text_elements=(
            CreativeTextElement(
                role=CreativeTextRoles.HEADLINE.value,
                text=request.brief.objective,
                verified=True,
            ),
            CreativeTextElement(
                role=CreativeTextRoles.DISCLOSURE.value,
                text=DISCLOSURE,
                verified=True,
            ),
        ),
    )


def _verified_price(
    request: CreativeAgentRequest,
    product: CreativeProductInput,
) -> str | None:
    if product.current_price_minor is None or product.currency is None:
        return None
    start = request.brief.campaign_start
    if start is not None:
        if product.offer_valid_until is None or start > product.offer_valid_until:
            return None
        if product.offer_valid_from is not None and start < product.offer_valid_from:
            return None
    minor_unit_digits = CURRENCY_MINOR_UNIT_DIGITS.get(product.currency)
    if minor_unit_digits is None:
        return None
    amount = Decimal(product.current_price_minor) / (Decimal(10) ** minor_unit_digits)
    return f"{product.currency} {amount:,.{minor_unit_digits}f}"


def _warnings(
    request: CreativeAgentRequest,
    approved_assets: dict[UUID, CreativeAssetInput],
    products: tuple[CreativeProductInput, ...],
) -> tuple[CreativeWarning, ...]:
    warnings: list[CreativeWarning] = []
    start = request.brief.campaign_start
    for product in products:
        if product.current_price_minor is not None and start is not None:
            if product.offer_valid_until is None:
                warnings.append(CreativeWarning(
                    code=CreativeWarningTypes.OFFER_VALIDITY_UNKNOWN.value,
                    message="The supplied price has no confirmed validity for the campaign period.",
                    product_name=product.name,
                ))
            elif start > product.offer_valid_until:
                warnings.append(CreativeWarning(
                    code=CreativeWarningTypes.OFFER_OUTSIDE_CAMPAIGN.value,
                    message="The supplied offer expires before the proposed campaign starts.",
                    product_name=product.name,
                ))
        if not any(asset_id in approved_assets for asset_id in product.asset_ids):
            warnings.append(CreativeWarning(
                code=CreativeWarningTypes.NO_APPROVED_PRODUCT_IMAGE.value,
                message="No approved supplied product image is available for this concept.",
                product_name=product.name,
            ))
    if any(
        asset.rights_status != AssetRightsStatuses.APPROVED.value
        for asset in request.assets
    ):
        warnings.append(CreativeWarning(
            code=CreativeWarningTypes.ASSET_RIGHTS_UNCONFIRMED.value,
            message="Some supplied assets are excluded until their usage rights are confirmed.",
        ))
    return tuple(warnings)


def _unknowns(
    request: CreativeAgentRequest,
    products: tuple[CreativeProductInput, ...],
    approved_assets: dict[UUID, CreativeAssetInput],
) -> tuple[UnknownItem, ...]:
    items: list[UnknownItem] = []
    if not products:
        items.append(UnknownItem(
            field_path="artifact.territories.product_names",
            question="Which verified products should the creative concept feature?",
            is_blocking=False,
        ))
    if products and not approved_assets:
        items.append(UnknownItem(
            field_path="artifact.territories.channel_concepts.source_asset_ids",
            question="Which supplied product images are cleared for concept use?",
            is_blocking=False,
        ))
    return tuple(items)


def _evidence_ids(
    products: tuple[CreativeProductInput, ...],
    approved_assets: dict[UUID, CreativeAssetInput],
) -> tuple[UUID, ...]:
    ids = {
        evidence_id
        for product in products
        for evidence_id in product.evidence_item_ids
    }
    ids.update(
        evidence_id
        for asset in approved_assets.values()
        for evidence_id in asset.evidence_item_ids
    )
    return tuple(sorted(ids, key=str))


def _bindings(evidence_ids: tuple[UUID, ...]) -> tuple[EvidenceBinding, ...]:
    if not evidence_ids:
        return ()
    return (EvidenceBinding(
        field_path="artifact.territories",
        evidence_item_ids=evidence_ids,
    ),)


def _territory_name(products: tuple[CreativeProductInput, ...]) -> str:
    return "Product-led creative" if products else "Brand-led creative"


def _territory_rationale(
    request: CreativeAgentRequest,
    products: tuple[CreativeProductInput, ...],
) -> str:
    if products:
        names = ", ".join(product.name for product in products)
        return f"Lead with verified products ({names}) while supporting {request.brief.objective}."
    return "Use approved brand material until verified product assets are available."


def _usage() -> ProviderUsage:
    return ProviderUsage(
        provider="deterministic",
        model="fixture-v1",
        units=0,
        tool_calls=0,
        incremental_cost_minor=0,
        cache_status="FIXTURE",
    )

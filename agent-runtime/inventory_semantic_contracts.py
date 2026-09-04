"""Typed contracts for two-stage, source-grounded inventory processing."""

from __future__ import annotations

import base64
import binascii
import hashlib
from decimal import Decimal
from typing import Annotated, Literal

from pydantic import Field, model_validator

from contracts import AgentInvocationEnvelope, ContractModel, StableCode

SourceLocator = Annotated[str, Field(min_length=1, max_length=1_000)]
SourceText = Annotated[str, Field(min_length=1, max_length=24_000)]
SourceHash = Annotated[str, Field(pattern=r"^[0-9a-f]{64}$")]
InventorySemanticOperation = Literal[
    "SOURCE_TRANSCRIPTION",
    "SEMANTIC_ENRICHMENT",
]

InventoryFieldName = Literal[
    "supplier_name",
    "product_code",
    "name",
    "channel",
    "product_type",
    "geography",
    "address",
    "latitude",
    "longitude",
    "rate_type",
    "currency",
    "rate",
    "availability",
    "description",
    "spoken_languages",
    "understood_languages",
    "life_stages",
    "lsm_sem_segments",
    "audience_taxonomy",
    "audience_taxonomy_version",
    "audience_universe",
    "audience_measurement_source",
    "audience_measurement_period",
    "audience_methodology",
    "audience_limitations",
    "audience_reach",
    "audience_reach_unit",
    "audience_listenership",
    "audience_listenership_unit",
    "audience_footfall",
    "audience_footfall_unit",
    "audience_impressions",
    "audience_impressions_unit",
    "supplier_vat_status",
    "supplier_vat_number",
    "supplier_commission_terms",
    "supplier_payment_terms",
    "supplier_cancellation_terms",
    "supplier_booking_deadline_terms",
    "vat_treatment",
    "rate_valid_from",
    "rate_valid_to",
    "production_cost_minor",
    "installation_cost_minor",
    "minimum_order",
    "discount_terms",
    "inclusions",
    "exclusions",
    "conditions",
    "booking_lead_time_days",
    "booking_deadline",
    "material_deadline",
    "cancellation_terms",
    "format",
    "buying_unit",
    "dimensions",
    "placement",
    "programme",
    "daypart",
    "spot_length_seconds",
    "loop_length_seconds",
    "slot_length_seconds",
    "plays_per_loop",
    "deliverable_quantity",
    "creative_specification",
    "country",
    "province",
    "municipality",
    "locality",
    "venue",
    "road",
    "route",
    "traffic_direction",
    "facing_bearing_degrees",
    "points_of_interest",
    "poi_name",
    "poi_category",
    "poi_latitude",
    "poi_longitude",
    "coverage_geojson",
    "catchment_geojson",
    "route_geojson",
    "direction_geojson",
    "package_code",
    "package_name",
    "package_component_codes",
    "package_discount_rule",
    "package_conditions",
    "contact_name",
    "contact_role",
    "contact_region",
    "contact_email",
    "contact_phone",
    "contact_website",
    "contact_social",
]

Transformation = Literal[
    "TRIM",
    "UPPERCASE_CODE",
    "MAJOR_TO_MINOR",
    "PARSE_DECIMAL",
    "DERIVED_FROM_CHANNEL",
    "PARSE_CURRENCY_AMOUNT",
    "DERIVED_FROM_SOURCE_CONTEXT",
]


class SemanticSourceItem(ContractModel):
    locator: SourceLocator
    kind: Literal[
        "TEXT",
        "TABLE",
        "IMAGE_OCR",
        "PAGE_OCR",
        "FILE_NAME",
        "UNSUPPORTED_EMBEDDED_ASSET",
    ]
    content: SourceText
    confidence: Annotated[Decimal | None, Field(ge=0, le=1)] = None


class SemanticSourceImage(ContractModel):
    ordinal: Annotated[int, Field(ge=1, le=20)]
    locator: SourceLocator
    format: Literal["png", "jpeg", "gif", "webp"]
    sha256: SourceHash
    byte_length: Annotated[int, Field(ge=1, le=3_750_000)]
    data_base64: Annotated[
        str,
        Field(min_length=4, max_length=5_000_000),
    ]

    @model_validator(mode="after")
    def validate_content(self) -> SemanticSourceImage:
        try:
            content = base64.b64decode(
                self.data_base64, validate=True)
        except (binascii.Error, ValueError) as error:
            raise ValueError(
                "Semantic source image encoding is invalid."
            ) from error
        if (not content or len(content) != self.byte_length or
                len(content) > 3_750_000 or
                not _has_image_signature(content, self.format) or
                hashlib.sha256(content).hexdigest() != self.sha256):
            raise ValueError(
                "Semantic source image hash or size is invalid."
            )
        return self


def _has_image_signature(content: bytes, format_name: str) -> bool:
    signatures = {
        "png": content.startswith(b"\x89PNG"),
        "jpeg": content.startswith(b"\xff\xd8\xff"),
        "gif": content.startswith(b"GIF8"),
        "webp": (
            content.startswith(b"RIFF") and
            len(content) >= 12 and
            content[8:12] == b"WEBP"
        ),
    }
    return signatures.get(format_name, False)


class SemanticExistingRow(ContractModel):
    row_number: Annotated[int, Field(ge=1)]
    locator: SourceLocator
    values: dict[StableCode, Annotated[str, Field(max_length=2_000)]]


class InventorySemanticCodes(ContractModel):
    channels: tuple[StableCode, ...]
    product_types: tuple[StableCode, ...]
    rate_types: tuple[StableCode, ...]
    currencies: tuple[StableCode, ...]
    availability_statuses: tuple[StableCode, ...]


class InventorySemanticAgentRequest(ContractModel):
    operation: InventorySemanticOperation
    invocation: AgentInvocationEnvelope
    source_hash: SourceHash
    file_name: Annotated[str, Field(min_length=1, max_length=500)]
    document_class: StableCode
    chunk_number: Annotated[int, Field(ge=1)]
    chunk_count: Annotated[int, Field(ge=1, le=256)]
    source_items: Annotated[
        tuple[SemanticSourceItem, ...],
        Field(min_length=1, max_length=100),
    ]
    existing_rows: Annotated[
        tuple[SemanticExistingRow, ...],
        Field(max_length=200),
    ] = ()
    source_images: Annotated[
        tuple[SemanticSourceImage, ...],
        Field(max_length=20),
    ] = ()
    governed_codes: InventorySemanticCodes

    @model_validator(mode="after")
    def validate_chunk(self) -> InventorySemanticAgentRequest:
        if self.chunk_number > self.chunk_count:
            raise ValueError("Semantic chunk number exceeds the chunk count.")
        locators = [item.locator for item in self.source_items]
        if len(locators) != len(set(locators)):
            raise ValueError("Semantic source locators must be unique.")
        row_locators = [item.locator for item in self.existing_rows]
        if len(row_locators) != len(set(row_locators)):
            raise ValueError("Semantic existing-row locators must be unique.")
        image_locators = [item.locator for item in self.source_images]
        if (len(image_locators) != len(set(image_locators)) or
                set(image_locators).intersection(locators)):
            raise ValueError("Semantic image locators must be unique.")
        if tuple(item.ordinal for item in self.source_images) != tuple(
                range(1, len(self.source_images) + 1)):
            raise ValueError("Semantic image ordinals must be contiguous.")
        if sum(item.byte_length for item in self.source_images) > 20_000_000:
            raise ValueError("Semantic image payload is too large.")
        if self.operation == "SEMANTIC_ENRICHMENT" and not self.existing_rows:
            raise ValueError(
                "Semantic enrichment requires deterministic source rows."
            )
        return self


class ProposedInventoryField(ContractModel):
    field_name: InventoryFieldName
    raw_value: Annotated[str, Field(min_length=1, max_length=2_000)]
    normalized_value: Annotated[str | None, Field(max_length=2_000)] = None
    source_locator: SourceLocator
    evidence_basis: Literal[
        "SUPPLIER_SUPPLIED",
        "DERIVED_POLICY",
    ]
    transformation: Transformation
    confidence: Annotated[Decimal, Field(ge=0, le=1)]


class ProposedInventoryCandidate(ContractModel):
    source_locator: SourceLocator
    fields: Annotated[
        tuple[ProposedInventoryField, ...],
        Field(min_length=1, max_length=80),
    ]
    ambiguity_notes: Annotated[
        tuple[Annotated[str, Field(min_length=1, max_length=500)], ...],
        Field(max_length=20),
    ] = ()

    @model_validator(mode="after")
    def validate_fields(self) -> ProposedInventoryCandidate:
        names = [item.field_name for item in self.fields]
        if len(names) != len(set(names)):
            raise ValueError("A semantic candidate cannot repeat a field.")
        return self


class InventorySemanticExtractionArtifact(ContractModel):
    candidates: Annotated[
        tuple[ProposedInventoryCandidate, ...],
        Field(max_length=200),
    ]
    omitted_source_locators: Annotated[
        tuple[SourceLocator, ...],
        Field(max_length=100),
    ] = ()

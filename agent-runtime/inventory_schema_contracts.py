"""Schema interpretation proposes source bindings, never commercial row values."""
from typing import Annotated, Literal

from pydantic import Field

from contracts import AgentInvocationEnvelope, ContractModel

OPERATION = "INVENTORY_SCHEMA_DISCOVERY"
Text = Annotated[str, Field(max_length=24_000)]
Locator = Annotated[str, Field(min_length=1, max_length=1_000)]
Confidence = Annotated[float, Field(ge=0, le=1)]


class SourceCell(ContractModel):
    locator: Locator
    row: Annotated[int, Field(ge=0, le=1_000_000)]
    column: Annotated[int, Field(ge=0, le=256)]
    raw_text: Text
    position_json: Text | None = None


class SourceStructure(ContractModel):
    id: Locator
    kind: Annotated[str, Field(min_length=1, max_length=50)]
    cells: Annotated[list[SourceCell], Field(max_length=10_000)]


class DiscoveryDocument(ContractModel):
    protocol_version: Literal["inventory-schema/1.0"]
    source_hash: Annotated[str, Field(pattern=r"^[0-9a-f]{64}$")]
    structure_hash: Annotated[str, Field(pattern=r"^[0-9a-f]{64}$")]
    representative_structures: Annotated[list[SourceStructure], Field(max_length=256)]
    canonical_meanings: Annotated[list[str], Field(max_length=256)]
    governed_codes: dict[str, list[str]]


class SchemaDiscoveryRequest(ContractModel):
    operation: Literal["INVENTORY_SCHEMA_DISCOVERY"]
    invocation: AgentInvocationEnvelope
    document: DiscoveryDocument


class SchemaCitation(ContractModel):
    source_locator: Locator
    quoted_text: Annotated[str, Field(min_length=1, max_length=24_000)]


class SchemaFieldMapping(ContractModel):
    canonical_meaning: Annotated[str, Field(max_length=100)] | None
    source_label: Text
    source_location: Locator
    source_structure: Locator
    source_column: Annotated[int, Field(ge=0, le=256)]
    row_offset: Annotated[int, Field(ge=0, le=1_000_000)]
    is_document_metadata: bool
    interpretation: Annotated[str, Field(min_length=1, max_length=2_000)]
    confidence: Confidence
    evidence: Annotated[list[SchemaCitation], Field(min_length=1, max_length=16)]
    interpreted_code: Annotated[str, Field(max_length=100)] | None = None
    value_source_location: Locator | None = None


class RecordBoundary(ContractModel):
    first_row: Annotated[int, Field(ge=0, le=1_000_000)]
    last_row: Annotated[int, Field(ge=0, le=1_000_000)]
    rows_per_record: Annotated[int, Field(ge=1, le=1_000_000)]
    excluded_rows: Annotated[list[int], Field(max_length=10_000)]
    exclusion_reasons: dict[int, Annotated[str, Field(min_length=1, max_length=2_000)]] | None = None


class RecordSchema(ContractModel):
    source_structure: Locator
    record_boundary: RecordBoundary
    field_mappings: Annotated[list[SchemaFieldMapping], Field(max_length=256)]
    supplier_metadata_mappings: Annotated[list[SchemaFieldMapping], Field(max_length=256)]
    asset_mappings: Annotated[list[SchemaFieldMapping], Field(max_length=256)]


class InventorySchemaProposal(ContractModel):
    protocol_version: Literal["inventory-schema/1.0"]
    source_hash: Annotated[str, Field(pattern=r"^[0-9a-f]{64}$")]
    structure_hash: Annotated[str, Field(pattern=r"^[0-9a-f]{64}$")]
    records: Annotated[list[RecordSchema], Field(max_length=256)]
    confidence: Confidence
    warnings: Annotated[list[Text], Field(max_length=256)]

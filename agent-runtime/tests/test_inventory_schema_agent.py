"""Grounding boundaries for once-per-structure inventory schema discovery."""

from decimal import Decimal
from uuid import UUID

import pytest
from fastapi import HTTPException

from agent_registry import AgentCode
from contracts import (
    AgentInvocationEnvelope,
    AgentOutputEnvelope,
    ConfidenceAssessment,
    OutputStatus,
    ProviderPolicy,
    ProviderUsage,
    ResourceReference,
    ResumeContext,
    ToolPolicy,
)
from inventory_schema_contracts import (
    OPERATION,
    DiscoveryDocument,
    InventorySchemaProposal,
    RecordBoundary,
    RecordSchema,
    SchemaCitation,
    SchemaDiscoveryRequest,
    SchemaFieldMapping,
    SourceCell,
    SourceStructure,
)
from inventory_schema_service import validate_schema_grounding
from runtime_execution import DETERMINISTIC_MODE, execute_agent

TENANT_ID = UUID("10000000-0000-0000-0000-000000000020")
ACTOR_ID = UUID("10000000-0000-0000-0000-000000000001")
RUN_ID = UUID("11111111-1111-1111-1111-111111111111")
STEP_ID = UUID("22222222-2222-2222-2222-222222222222")
CORRELATION_ID = UUID("33333333-3333-3333-3333-333333333333")
IMPORT_ID = UUID("44444444-4444-4444-4444-444444444444")
SOURCE_HASH = "a" * 64
STRUCTURE_HASH = "b" * 64


def invocation() -> AgentInvocationEnvelope:
    return AgentInvocationEnvelope(
        schema_version="1.0.0",
        tenant_id=TENANT_ID,
        actor_id=ACTOR_ID,
        effective_role="AGENT_RUNTIME_SERVICE",
        run_id=RUN_ID,
        step_id=STEP_ID,
        correlation_id=CORRELATION_ID,
        agent_code=AgentCode.INVENTORY_INTELLIGENCE,
        contract_version="1.0.0",
        prompt_version="5.0.0",
        resource_refs=(ResourceReference(
            resource_type="inventory_import",
            resource_id=IMPORT_ID,
            version=1,
        ),),
        approved_evidence_item_ids=(),
        locale="und",
        account_policy_version="1.0.0",
        tool_policy=ToolPolicy(
            allowed_tools=(), max_tool_calls=0,
            consequence_policy="PROPOSE_ONLY",
        ),
        provider_policy=ProviderPolicy(
            provider="deterministic",
            model="fixture-v1",
            temperature=0,
            timeout_seconds=30,
            max_attempts=1,
            cost_cap_minor=0,
            allow_live=False,
        ),
        resume=ResumeContext(),
    )


def document() -> DiscoveryDocument:
    return DiscoveryDocument(
        protocol_version="inventory-schema/1.0",
        source_hash=SOURCE_HASH,
        structure_hash=STRUCTURE_HASH,
        representative_structures=[SourceStructure(
            id="sheet-1",
            kind="cell",
            cells=[
                SourceCell(locator="h-product", row=1, column=1, raw_text="Product"),
                SourceCell(locator="h-rate", row=1, column=2, raw_text="Rate"),
                SourceCell(locator="r2-product", row=2, column=1, raw_text="SITE-A"),
                SourceCell(locator="r2-rate", row=2, column=2, raw_text="125000"),
            ],
        )],
        canonical_meanings=["product_code", "rate", "currency"],
        governed_codes={"currency": ["ZAR"]},
    )


def mapping(
    meaning: str = "product_code",
    label: str = "Product",
    locator: str = "h-product",
    column: int = 1,
    interpreted_code: str | None = None,
) -> SchemaFieldMapping:
    return SchemaFieldMapping(
        canonical_meaning=meaning,
        source_label=label,
        source_location=locator,
        source_structure="sheet-1",
        source_column=column,
        row_offset=0,
        is_document_metadata=False,
        interpretation=f"{label} identifies {meaning}.",
        confidence=1,
        evidence=[SchemaCitation(
            source_locator=locator,
            quoted_text=label,
        )],
        interpreted_code=interpreted_code,
    )


def proposal(*mappings: SchemaFieldMapping) -> InventorySchemaProposal:
    return InventorySchemaProposal(
        protocol_version="inventory-schema/1.0",
        source_hash=SOURCE_HASH,
        structure_hash=STRUCTURE_HASH,
        records=[RecordSchema(
            source_structure="sheet-1",
            record_boundary=RecordBoundary(
                first_row=2,
                last_row=2,
                rows_per_record=1,
                excluded_rows=[],
            ),
            field_mappings=list(mappings) if mappings else [
                mapping(),
                mapping("rate", "Rate", "h-rate", 2),
            ],
            supplier_metadata_mappings=[],
            asset_mappings=[],
        )],
        confidence=1,
        warnings=[],
    )


def envelope(artifact: InventorySchemaProposal) -> AgentOutputEnvelope[InventorySchemaProposal]:
    return AgentOutputEnvelope(
        schema_version="1.0.0",
        status=OutputStatus.REVIEW_REQUIRED,
        artifact=artifact,
        evidence_bindings=(),
        unknowns=(),
        assumptions=(),
        confidence=(ConfidenceAssessment(
            field_path="artifact.records",
            confidence=Decimal("1"),
        ),),
        objections=(),
        rationale="Source-bound schema requires governed projection.",
        suggested_next_action=None,
        usage=ProviderUsage(
            provider="deterministic",
            model="fixture-v1",
            units=0,
            tool_calls=0,
            incremental_cost_minor=0,
            cache_status="FIXTURE",
            provider_request_id=None,
            input_tokens=0,
            output_tokens=0,
            incremental_cost_usd_micros=0,
        ),
    )


def request() -> SchemaDiscoveryRequest:
    return SchemaDiscoveryRequest(
        operation=OPERATION,
        invocation=invocation(),
        document=document(),
    )


def test_source_bound_schema_passes_grounding() -> None:
    validate_schema_grounding(request(), envelope(proposal()))


def test_schema_rejects_forged_source_label() -> None:
    artifact = proposal(mapping(label="Invented Product"))
    with pytest.raises(ValueError, match="source binding"):
        validate_schema_grounding(request(), envelope(artifact))


def test_schema_rejects_ungoverned_interpreted_code() -> None:
    artifact = proposal(mapping(
        meaning="currency",
        label="Product",
        interpreted_code="USD",
    ))
    with pytest.raises(ValueError, match="ungoverned"):
        validate_schema_grounding(request(), envelope(artifact))


def test_schema_requires_every_structure_exactly_once() -> None:
    artifact = InventorySchemaProposal(
        protocol_version="inventory-schema/1.0",
        source_hash=SOURCE_HASH,
        structure_hash=STRUCTURE_HASH,
        records=[],
        confidence=1,
        warnings=[],
    )
    with pytest.raises(ValueError, match="each supplied structure"):
        validate_schema_grounding(request(), envelope(artifact))


def test_deterministic_runtime_never_invents_schema_fixture() -> None:
    with pytest.raises(HTTPException) as error:
        execute_agent(
            AgentCode.INVENTORY_INTELLIGENCE,
            request().model_dump_json().encode(),
            DETERMINISTIC_MODE,
        )
    assert error.value.status_code == 503

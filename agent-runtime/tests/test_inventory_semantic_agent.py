"""Focused contracts for two-stage inventory source processing."""

import base64
import hashlib
from decimal import Decimal
from uuid import UUID

import pytest
from pydantic import ValidationError

from agent_registry import AgentCode
from bedrock_multimodal import request_content
from contracts import (
    AgentInvocationEnvelope,
    AgentOutputEnvelope,
    ConfidenceAssessment,
    OutputStatus,
    ProviderPolicy,
    ProviderUsage,
    ResourceReference,
    ResumeContext,
    SuggestedNextAction,
    ToolPolicy,
)
from inventory_semantic_contracts import (
    InventorySemanticAgentRequest,
    InventorySemanticCodes,
    InventorySemanticExtractionArtifact,
    ProposedInventoryCandidate,
    ProposedInventoryField,
    SemanticExistingRow,
    SemanticSourceImage,
    SemanticSourceItem,
)
from inventory_semantic_service import (
    SEMANTIC_ENRICHMENT,
    SOURCE_TRANSCRIPTION,
    validate_semantic_grounding,
)
from runtime_execution import DETERMINISTIC_MODE, execute_agent

TENANT_ID = UUID("10000000-0000-0000-0000-000000000020")
ACTOR_ID = UUID("10000000-0000-0000-0000-000000000001")
RUN_ID = UUID("11111111-1111-1111-1111-111111111111")
STEP_ID = UUID("22222222-2222-2222-2222-222222222222")
CORRELATION_ID = UUID("33333333-3333-3333-3333-333333333333")
IMPORT_ID = UUID("44444444-4444-4444-4444-444444444444")
LOCATOR = "docling:page=1;text=1"


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
        prompt_version="1.0.0",
        resource_refs=(ResourceReference(
            resource_type="inventory_import",
            resource_id=IMPORT_ID,
            version=1,
        ),),
        approved_evidence_item_ids=(),
        locale="und",
        account_policy_version="1.0.0",
        tool_policy=ToolPolicy(
            allowed_tools=(),
            max_tool_calls=0,
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


def request(
    operation: str = SEMANTIC_ENRICHMENT,
) -> InventorySemanticAgentRequest:
    existing = (
        SemanticExistingRow(
            row_number=1,
            locator=LOCATOR,
            values={"name": "DStv Stream VOD", "rate": "R575"},
        ),
    ) if operation == SEMANTIC_ENRICHMENT else ()
    return InventorySemanticAgentRequest(
        operation=operation,
        invocation=invocation(),
        source_hash="a" * 64,
        file_name="DMS Digital Rate Card.xlsx",
        document_class="XLSX",
        chunk_number=1,
        chunk_count=1,
        source_items=(SemanticSourceItem(
            locator=LOCATOR,
            kind="TEXT",
            content=(
                "DStv Media Sales Digital Rate Card. DStv Stream VOD "
                "Video Pre Roll MP4 R575 R1,10."
            ),
        ),),
        existing_rows=existing,
        governed_codes=InventorySemanticCodes(
            channels=("DIGITAL",),
            product_types=("DIGITAL_PLACEMENT",),
            rate_types=("CPM",),
            currencies=("ZAR",),
            availability_statuses=("PLANNING_AVAILABLE",),
        ),
    )


def envelope(
    candidate: ProposedInventoryCandidate,
) -> AgentOutputEnvelope[InventorySemanticExtractionArtifact]:
    return AgentOutputEnvelope(
        schema_version="1.0.0",
        status=OutputStatus.REVIEW_REQUIRED,
        artifact=InventorySemanticExtractionArtifact(candidates=(candidate,)),
        evidence_bindings=(),
        unknowns=(),
        assumptions=(),
        confidence=(ConfidenceAssessment(
            field_path="artifact.candidates",
            confidence=Decimal("0.99"),
        ),),
        objections=(),
        rationale="Source-linked processing requires human review.",
        suggested_next_action=SuggestedNextAction(
            command_code="ReviewInventorySemanticExtraction",
            requires_human=True,
        ),
        usage=ProviderUsage(
            provider="deterministic",
            model="fixture-v1",
            units=0,
            tool_calls=0,
            incremental_cost_minor=0,
            cache_status="FIXTURE",
        ),
    )


def semantic_output(
    *,
    field_name: str = "channel",
    raw_value: str = "DStv Stream VOD",
    normalized_value: str | None = "DIGITAL",
    evidence_basis: str = "DERIVED_POLICY",
    candidate_locator: str = LOCATOR,
    field_locator: str = LOCATOR,
) -> AgentOutputEnvelope[InventorySemanticExtractionArtifact]:
    return envelope(ProposedInventoryCandidate(
        source_locator=candidate_locator,
        fields=(ProposedInventoryField(
            field_name=field_name,
            raw_value=raw_value,
            normalized_value=normalized_value,
            source_locator=field_locator,
            evidence_basis=evidence_basis,
            transformation="DERIVED_FROM_SOURCE_CONTEXT",
            confidence=Decimal("0.99"),
        ),),
    ))


def transcription_output(
    *,
    raw_rate: str = "R1,10",
    normalized_rate: str | None = None,
    evidence_basis: str = "SUPPLIER_SUPPLIED",
    ambiguity_notes: tuple[str, ...] = (
        "The visible amount R1,10 is incomplete or ambiguous.",
    ),
) -> AgentOutputEnvelope[InventorySemanticExtractionArtifact]:
    return envelope(ProposedInventoryCandidate(
        source_locator=LOCATOR,
        fields=(
            ProposedInventoryField(
                field_name="supplier_name",
                raw_value="DStv Media Sales",
                source_locator=LOCATOR,
                evidence_basis="SUPPLIER_SUPPLIED",
                transformation="TRIM",
                confidence=Decimal("0.99"),
            ),
            ProposedInventoryField(
                field_name="name",
                raw_value="DStv Stream VOD",
                source_locator=LOCATOR,
                evidence_basis="SUPPLIER_SUPPLIED",
                transformation="TRIM",
                confidence=Decimal("0.99"),
            ),
            ProposedInventoryField(
                field_name="rate",
                raw_value=raw_rate,
                normalized_value=normalized_rate,
                source_locator=LOCATOR,
                evidence_basis=evidence_basis,
                transformation="TRIM",
                confidence=Decimal("0.99"),
            ),
        ),
        ambiguity_notes=ambiguity_notes,
    ))


def test_source_transcription_preserves_facts_without_normalization() -> None:
    value = request(SOURCE_TRANSCRIPTION)
    validate_semantic_grounding(value, transcription_output())

    with pytest.raises(ValueError, match="not present"):
        validate_semantic_grounding(
            value,
            transcription_output(raw_rate="R9,999"),
        )
    with pytest.raises(ValueError, match="supplier-supplied"):
        validate_semantic_grounding(
            value,
            transcription_output(evidence_basis="DERIVED_POLICY"),
        )
    with pytest.raises(ValueError, match="cannot normalize"):
        validate_semantic_grounding(
            value,
            transcription_output(normalized_rate="110"),
        )
    with pytest.raises(ValueError, match="ambiguity note"):
        validate_semantic_grounding(
            value,
            transcription_output(ambiguity_notes=()),
        )


@pytest.mark.parametrize(
    "field_name",
    ("channel", "rate_type", "rate_valid_from"),
)
def test_source_transcription_rejects_non_source_fields(
    field_name: str,
) -> None:
    candidate = ProposedInventoryCandidate(
        source_locator=LOCATOR,
        fields=(
            ProposedInventoryField(
                field_name="name",
                raw_value="DStv Stream VOD",
                source_locator=LOCATOR,
                evidence_basis="SUPPLIER_SUPPLIED",
                transformation="TRIM",
                confidence=Decimal("1"),
            ),
            ProposedInventoryField(
                field_name=field_name,
                raw_value="DStv Stream VOD",
                source_locator=LOCATOR,
                evidence_basis="SUPPLIER_SUPPLIED",
                transformation="TRIM",
                confidence=Decimal("1"),
            ),
        ),
    )
    with pytest.raises(ValueError, match="semantic, governed or dated"):
        validate_semantic_grounding(
            request(SOURCE_TRANSCRIPTION), envelope(candidate)
        )


def test_source_transcription_rejects_duplicate_fields() -> None:
    field = ProposedInventoryField(
        field_name="name",
        raw_value="DStv Stream VOD",
        source_locator=LOCATOR,
        evidence_basis="SUPPLIER_SUPPLIED",
        transformation="TRIM",
        confidence=Decimal("1"),
    )
    with pytest.raises(ValidationError, match="cannot repeat a field"):
        ProposedInventoryCandidate(
            source_locator=LOCATOR,
            fields=(field, field),
        )


def test_semantic_grounding_only_enriches_existing_rows() -> None:
    value = request()
    validate_semantic_grounding(value, semantic_output())

    with pytest.raises(ValueError, match="not present"):
        validate_semantic_grounding(
            value,
            semantic_output(raw_value="Invented platform"),
        )
    with pytest.raises(ValueError, match="cannot author commercial"):
        validate_semantic_grounding(
            value,
            semantic_output(
                field_name="rate",
                raw_value="R575",
                normalized_value=None,
            ),
        )
    with pytest.raises(ValueError, match="derived policy"):
        validate_semantic_grounding(
            value,
            semantic_output(evidence_basis="SUPPLIER_SUPPLIED"),
        )
    with pytest.raises(ValueError, match="existing deterministic row"):
        validate_semantic_grounding(
            value,
            semantic_output(candidate_locator="docling:page=2;text=1"),
        )


def test_image_source_is_hash_bound_and_explicitly_accounted() -> None:
    content = b"\x89PNG\r\n\x1a\nsource-image"
    locator = "xlsx:package;embedded-part=xl%2Fmedia%2Fimage1.png"
    image = SemanticSourceImage(
        ordinal=1,
        locator=locator,
        format="png",
        sha256=hashlib.sha256(content).hexdigest(),
        byte_length=len(content),
        data_base64=base64.b64encode(content).decode(),
    )
    value = request().model_copy(update={"source_images": (image,)})
    blocks = request_content(
        value,
        "multimodal-model",
        frozenset({"multimodal-model"}),
    )
    assert locator in blocks[1]["text"]  # type: ignore[operator]
    assert blocks[2]["image"]["source"]["bytes"] == content  # type: ignore[index]
    assert image.data_base64 not in str(blocks[0]["text"])

    described = semantic_output(
        field_name="description",
        raw_value="DStv Stream VOD",
        normalized_value="DStv digital streaming placement",
        field_locator=locator,
    )
    validate_semantic_grounding(value, described)

    with pytest.raises(ValueError, match="used or explicitly omitted"):
        validate_semantic_grounding(value, semantic_output())


def test_inventory_route_accepts_both_source_operations() -> None:
    for operation in (SOURCE_TRANSCRIPTION, SEMANTIC_ENRICHMENT):
        result = execute_agent(
            AgentCode.INVENTORY_INTELLIGENCE,
            request(operation).model_dump_json().encode(),
            DETERMINISTIC_MODE,
        )
        assert result["status"] == "REVIEW_REQUIRED"
        assert result["artifact"] == {
            "candidates": [],
            "omitted_source_locators": [],
        }
        assert result["usage"]["incremental_cost_minor"] == 0

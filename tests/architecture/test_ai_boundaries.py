"""AI ownership and governed model-profile architecture checks."""

from __future__ import annotations

import json
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]


def test_governed_bedrock_routes_match_the_approved_profiles() -> None:
    settings = json.loads(
        (REPO_ROOT / "api" / "appsettings.json").read_text(
            encoding="utf-8"
        )
    )["AgentRuntime"]
    models = settings["Models"]
    sonnet = "global.anthropic.claude-sonnet-4-6"
    nova_lite = "global.amazon.nova-2-lite-v1:0"
    reasoning_agents = {
        "business_interpretation",
        "opportunity_intelligence",
        "strategy",
        "critic_readiness",
        "brief_drafting",
        "brief_drafting__supplied_brief_understanding",
        "audience",
        "inventory_intelligence",
        "media_planning",
        "proposal_narrative",
        "creative",
        "measurement",
    }

    assert {models[route] for route in reasoning_agents} == {sonnet}
    assert models["inventory_intelligence__schema_discovery"] == nova_lite
    assert models["inventory_intelligence__semantic_enrichment"] == nova_lite
    assert "source_transcription" not in models


def test_ai_interpretation_is_not_implemented_in_production_csharp() -> None:
    infrastructure = (
        REPO_ROOT
        / "api"
        / "src"
        / "Advertified.Commercial.Infrastructure"
    )
    removed = (
        "Brief/DeterministicSuppliedBriefAgentClient.cs",
        "Brief/DeterministicSuppliedBriefAgentClient.Mode.cs",
        "Brief/SuppliedBriefAgentPolicy.cs",
        "Brief/SuppliedBriefBudgetParser.cs",
        "Brief/SuppliedBriefParsingModels.cs",
        "Opportunity/InProcessOpportunityAgentClient.cs",
        "Planning/DeterministicPlanningAgentClient.cs",
        "Proposal/DeterministicProposalNarrativeClient.cs",
        "Measurement/DeterministicMeasurementAgentClient.cs",
    )
    assert all(not (infrastructure / name).exists() for name in removed)

    runtime_options = (
        infrastructure / "Opportunity" / "AgentRuntimeOptions.cs"
    ).read_text(encoding="utf-8")
    assert "InProcessDeterministic" not in runtime_options

    development = json.loads(
        (REPO_ROOT / "api" / "appsettings.Development.json").read_text(
            encoding="utf-8"
        )
    )
    assert development["SuppliedBrief"]["Mode"] == "Disabled"


def test_inventory_extraction_runtime_cannot_reference_corpus_memory() -> None:
    runtime_files = [
        *sorted((REPO_ROOT / "agent-runtime").glob("*.py")),
        *sorted((
            REPO_ROOT
            / "api"
            / "src"
            / "Advertified.Commercial.Infrastructure"
            / "Inventory"
        ).glob("*.cs")),
        REPO_ROOT / "api" / "InventoryExtractionRegistration.cs",
    ]
    forbidden = {
        "artifacts/inventory",
        "inventory-corpus",
        "physical-sources",
        "semantic-v1",
        "observed-3.9",
        "raw-docling",
        "inventory-evidence",
        "replay-input",
    }
    violations = {
        str(path.relative_to(REPO_ROOT)): token
        for path in runtime_files
        for token in forbidden
        if token in path.read_text(encoding="utf-8").casefold()
    }
    assert not violations


def test_python_inventory_projection_has_only_current_document_inputs() -> None:
    contract = (
        REPO_ROOT / "agent-runtime" / "inventory_extraction_contracts.py"
    ).read_text(encoding="utf-8")
    request_body = contract.split(
        "class InventoryProjectionRequest", 1
    )[1].split("class ExtractedRateVariant", 1)[0]

    assert "provider_document" in request_body
    assert "source_hash" not in request_body
    assert "document_class" not in request_body
    assert "file_name" not in request_body
    assert "supplier" not in request_body
    assert "previous" not in request_body
    assert "corpus" not in request_body


def test_inventory_semantic_packets_cannot_receive_source_identity() -> None:
    inventory = (
        REPO_ROOT
        / "api"
        / "src"
        / "Advertified.Commercial.Infrastructure"
        / "Inventory"
    )
    operations = (
        inventory / "InventorySemanticPacketBuilder.Operations.cs"
    ).read_text(encoding="utf-8")
    client = (
        inventory / "InventorySemanticAgentClient.cs"
    ).read_text(encoding="utf-8")
    request_build = client.split(
        "var payload = new InventorySemanticAgentRequest(", 1
    )[1].split("return AgentRuntimeHttpSupport", 1)[0]
    contract = (
        REPO_ROOT / "agent-runtime" / "inventory_semantic_contracts.py"
    ).read_text(encoding="utf-8")
    request_body = contract.split(
        "class InventorySemanticAgentRequest", 1
    )[1].split("class ProposedInventoryField", 1)[0]

    for forbidden in ("source_hash", "document_class", "file_name"):
        assert forbidden not in request_body
    assert "context.SourceHash" not in request_build
    assert "context.DocumentClass" not in request_build
    assert "InventoryExtractionRequest request" not in operations
    assert "request.SourceHash" not in operations
    assert "request.DocumentClass" not in operations
    assert "request.FileName" not in operations

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
    nova_lite = "amazon.nova-lite-v1:0"
    governed_routes = {
        "business_interpretation",
        "opportunity_intelligence",
        "strategy",
        "critic_readiness",
        "brief_drafting",
        "brief_drafting__supplied_brief_understanding",
        "audience",
        "inventory_intelligence",
        "inventory_intelligence__schema_discovery",
        "inventory_intelligence__source_transcription",
        "inventory_intelligence__semantic_enrichment",
        "media_planning",
        "proposal_narrative",
        "creative",
        "measurement",
    }

    assert set(models) == governed_routes
    assert set(models.values()) == {nova_lite}


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


def test_retired_document_provider_has_no_executable_runtime_surface() -> None:
    runtime = REPO_ROOT / "agent-runtime"
    inventory = (
        REPO_ROOT
        / "api"
        / "src"
        / "Advertified.Commercial.Infrastructure"
        / "Inventory"
    )
    retired_provider = "doc" + "ling"
    assert not list(runtime.glob(f"inventory_{retired_provider}_*.py"))
    assert not (runtime / "inventory_extraction_service.py").exists()
    assert not (
        inventory / f"{retired_provider.title()}InventoryExtractionAdapter.cs"
    ).exists()
    assert "/v1/inventory-extraction/project" not in (
        runtime / "main.py"
    ).read_text(encoding="utf-8")


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


def test_every_http_agent_route_uses_the_monthly_budget_handler() -> None:
    program = (REPO_ROOT / "api" / "Program.cs").read_text(encoding="utf-8")
    supplied = (
        REPO_ROOT / "api" / "Startup" / "SuppliedBriefConfiguration.cs"
    ).read_text(encoding="utf-8")
    clients = (
        "HttpOpportunityAgentClient",
        "HttpPlanningAgentClient",
        "HttpProposalNarrativeClient",
        "HttpMeasurementAgentClient",
        "InventorySemanticAgentClient",
    )
    for client in clients:
        registration = program.split(f"AddHttpClient<{client}>", 1)[1]
        registration = registration.split(";", 1)[0]
        assert "AddHttpMessageHandler<AiMonthlyBudgetHandler>" in registration
    registration = supplied.split(
        "AddHttpClient<HttpSuppliedBriefAgentClient>", 1
    )[1].split(";", 1)[0]
    assert "AddHttpMessageHandler<AiMonthlyBudgetHandler>" in registration

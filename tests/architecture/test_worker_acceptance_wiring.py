"""Durable opportunity-worker acceptance wiring cannot silently disappear."""

from __future__ import annotations

import json
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[2]


def test_opportunity_worker_acceptance_has_a_repeatable_command() -> None:
    package = json.loads(
        (REPO_ROOT / "web" / "package.json").read_text(encoding="utf-8")
    )
    script = package["scripts"]["test:api:integration:opportunity"]

    assert "run-api-release-tests.ps1" in script
    assert "OpportunityAcceptanceTests" in script


def test_opportunity_fixture_enables_dispatcher_without_live_ai() -> None:
    support = (
        REPO_ROOT
        / "api"
        / "tests"
        / "Advertified.Commercial.Api.Tests"
        / "OpportunityAcceptanceTests.Support.cs"
    ).read_text(encoding="utf-8")

    assert "AgentRuntimeOptions.HttpDeterministicMode" in support
    assert "AgentRuntimeOptions.DisabledMode" in support
    assert 'builder.UseSetting("Process:Role", ProcessRoleOptions.CombinedRole);' in support
    assert "services.RemoveAll<IOpportunityAgentClient>();" in support
    assert "OpportunityAgentFixture" in support

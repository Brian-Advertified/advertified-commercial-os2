"""Resolve both existing local Compose profiles without starting services or providers."""
import json
import os
from pathlib import Path
import subprocess

import pytest

ROOT = Path(__file__).resolve().parents[2]


@pytest.mark.parametrize("preview", [False, True])
def test_local_runtime_provider_policy_is_explicit_and_profiles_do_not_leak(tmp_path, preview):
    command = ["docker", "compose", "--project-name", "advertified-os2-dev",
               "--file", "infrastructure/docker-compose.yml",
               "--file", "infrastructure/docker-compose.app.yml"]
    if preview:
        command += ["--file", "infrastructure/development/docker-compose.bedrock-preview.yml"]
    result = subprocess.run(command + ["config", "--format", "json"], cwd=ROOT,
                            env={**os.environ, "USERPROFILE": str(tmp_path)},
                            capture_output=True, text=True, timeout=30, check=False)
    assert result.returncode == 0, "Local Compose configuration must resolve before any refresh."
    services = json.loads(result.stdout)["services"]
    api = services["api"]["environment"]
    expected = {
        "AgentRuntime__Mode": "Http" if preview else "HttpDeterministic",
        "AgentRuntime__Provider": "bedrock" if preview else "deterministic",
        "AgentRuntime__DefaultModel": "amazon.nova-lite-v1:0" if preview else "fixture-v1",
        "AgentRuntime__DefaultCostCapMinor": "200" if preview else "0",
        "AgentRuntime__CostCapsMinor__media_strategy": "5" if preview else "0",
        "AgentRuntime__AllowLive": "true" if preview else "false",
        "AgentRuntime__MaxAttempts": "1",
        "InventoryProcessing__Paused": "true",
    }
    for key, value in expected.items():
        assert api.get(key) == value, key
    runtime = services["agent-runtime"]["environment"]
    assert runtime["ADVERTIFIED_AGENT_RUNTIME_MODE"] == ("bedrock" if preview else "deterministic")


def test_local_api_startup_excludes_optional_services_and_inventory_bootstrap():
    command = ["docker", "compose", "--project-name", "advertified-os2-dev",
               "--file", "infrastructure/docker-compose.yml",
               "--file", "infrastructure/docker-compose.app.yml"]
    result = subprocess.run(command + ["config", "--format", "json"], cwd=ROOT,
                            capture_output=True, text=True, timeout=30, check=False)
    assert result.returncode == 0
    services = json.loads(result.stdout)["services"]
    api_dependencies = services["api"].get("depends_on", {})
    assert "redis" not in api_dependencies
    assert "mailhog" not in api_dependencies
    assert "development-inventory-seed" not in api_dependencies
    inventory_seed = services["development-inventory-seed"]
    assert "development-seed" in inventory_seed["depends_on"]

    workspace_seed = (ROOT / "infrastructure" / "development" / "seed-local-workspace.sql").read_text(
        encoding="utf-8")
    inventory_seed_sql = (ROOT / "infrastructure" / "development" / "seed-local-inventory.sql").read_text(
        encoding="utf-8")
    assert "inventory-bootstrap.generated.sql" not in workspace_seed
    assert "publish-current-inventory-to-marketplace.sql" not in workspace_seed
    assert "\\ir inventory-bootstrap.generated.sql" in inventory_seed_sql
    assert "publish-current-inventory-to-marketplace.sql" not in inventory_seed_sql
    assert "development-marketplace-seed" not in services


def test_local_start_helper_uses_canonical_compose_guard_and_keeps_optional_services_opt_in():
    helper = (ROOT / "tools" / "start-local-advertified-containers.ps1").read_text(encoding="utf-8")
    assert "advertified-compose.ps1" in helper
    assert "Assert-AdvertifiedComposeProject -RequireExisting" in helper
    assert "Invoke-AdvertifiedCompose" in helper
    assert "'up', '--detach', '--no-build'" in helper
    assert "[switch]$BackendOnly" in helper
    assert "[switch]$IncludeOptionalInfrastructure" in helper
    assert "'redis', 'mailhog'" in helper
    assert "docker @arguments" not in helper
    assert "advertified-os2-dev-redis-1" not in helper
    assert "advertified-os2-dev-mailhog-1" not in helper
    assert "without running migrator or development seed" not in helper

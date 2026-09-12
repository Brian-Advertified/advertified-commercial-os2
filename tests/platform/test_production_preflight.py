"""Execute the real production preflight with synthetic configuration, never deploy it."""

import json
import shutil
import subprocess
from pathlib import Path
from tempfile import TemporaryDirectory

import pytest

ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "tools/validate-production-configuration.ps1"


def read_template():
    result = {}
    for line in (ROOT / "infrastructure/env.production.example").read_text(encoding="utf-8").splitlines():
        if line.strip() and not line.startswith("#"):
            key, value = line.split("=", 1)
            result[key] = "fixture-configuration" if "REPLACE_WITH" in value else value
    return result


def configuration(directory):
    values = read_template()
    for key in values:
        if key.endswith("_IMAGE"):
            values[key] = "registry.example/fixture@sha256:" + "a" * 64
        elif "CostCap" in key:
            values[key] = "5"
    values.update({
        "ADVERTIFIED_API_ENV_FILE": str(directory / "api.fixture"),
        "ADVERTIFIED_WORKER_ENV_FILE": str(directory / "worker.fixture"),
        "ADVERTIFIED_MIGRATOR_ENV_FILE": str(directory / "migrator.fixture"),
        "ADVERTIFIED_AGENT_ENV_FILE": str(directory / "agent.fixture"),
        "ADVERTIFIED_API_CONFIG_FILE": str(directory / "appsettings.Production.json"),
        "ADVERTIFIED_DATA_PROTECTION_KEYS_DIR": str(directory / "keys"),
        "ADVERTIFIED_DATA_PROTECTION_CERT_PATH": str(directory / "certificate.pfx"),
        "ConnectionStrings__CommercialDatabase": "Host=database.example;Database=advertified;Username=api;Password=fixture-api",
        "ConnectionStrings__WorkerSchedulerDatabase": "Host=database.example;Database=advertified;Username=worker;Password=fixture-worker",
        "Authentication__Oidc__Authority": "https://identity.example",
        "Authentication__Oidc__LogoutEndpoint": "https://identity.example/logout",
        "AgentRuntime__BaseUrl": "http://agent-runtime:8080",
        "AgentRuntime__ServiceKey": "fixture-service-key-not-a-live-secret",
        "ADVERTIFIED_AGENT_RUNTIME_SERVICE_KEY": "fixture-service-key-not-a-live-secret",
        "ADVERTIFIED_BEDROCK_MODEL_ALLOWLIST": "synthetic-config-test-model",
        "ADVERTIFIED_BEDROCK_MULTIMODAL_MODEL_ALLOWLIST": "synthetic-config-test-model",
        "ADVERTIFIED_BEDROCK_PRICING_JSON": json.dumps({"synthetic-config-test-model": {
            "input_per_million_usd": 0.1, "output_per_million_usd": 0.2}}),
        "InventorySemantic__ModelId": "synthetic-config-test-model",
        "InventorySemantic__InputPricePerMillionTokensUsdMicros": "60000",
        "InventorySemantic__OutputPricePerMillionTokensUsdMicros": "240000",
        "InventorySemantic__PerCallCostCapUsdMicros": "50000",
        "InventorySemantic__CertificationBudgetUsdMicros": "5000000",
    })
    models = json.loads((ROOT / "infrastructure/production/appsettings.Production.example.json")
                       .read_text().replace("REPLACE_WITH_APPROVED_MODEL", "synthetic-config-test-model"))
    models["LocationIntelligence"]["Discovery"]["Endpoint"] = "https://location.example/"
    models["LocationIntelligence"]["PoiDiscovery"]["Endpoint"] = "https://poi.example/api/interpreter"
    migrator = {
        "ASPNETCORE_ENVIRONMENT": "Production",
        "ADVERTIFIED_MIGRATION_CONNECTION_STRING": (
            "Host=database.example;Database=advertified;Username=migrator;Password=fixture-migrator"
        ),
    }
    return values, models, migrator


def write_fixture(directory, values, models, migrator):
    host = {key: value for key, value in values.items() if key.startswith("ADVERTIFIED_")}
    api = {
        key: value for key, value in values.items()
        if not key.startswith(("ADVERTIFIED_", "VITE_"))
        and key != "ConnectionStrings__WorkerSchedulerDatabase"
    }
    worker = {
        "ConnectionStrings__WorkerSchedulerDatabase": values["ConnectionStrings__WorkerSchedulerDatabase"]
    }
    agent = {key: value for key, value in values.items() if key.startswith("ADVERTIFIED_")}
    files = (
        ("host.fixture", host),
        ("api.fixture", api),
        ("worker.fixture", worker),
        ("migrator.fixture", migrator),
        ("agent.fixture", agent),
    )
    for name, content in files:
        (directory / name).write_text(
            "\n".join(f"{key}={value}" for key, value in content.items()), encoding="utf-8")
    (directory / "appsettings.Production.json").write_text(json.dumps(models), encoding="utf-8")
    (directory / "keys").mkdir(exist_ok=True)
    (directory / "certificate.pfx").write_bytes(b"configuration-only-test-not-a-certificate")


@pytest.fixture
def fixture_config():
    base = ROOT / "artifacts/backend-production-completion/preflight-fixtures"
    base.mkdir(parents=True, exist_ok=True)
    with TemporaryDirectory(prefix="synthetic-", dir=base) as temp:
        directory = Path(temp)
        values, models, migrator = configuration(directory)
        yield directory, values, models, migrator


def invoke_preflight(directory):
    powershell = shutil.which("pwsh") or shutil.which("powershell")
    assert powershell, "PowerShell is required to execute the actual deployment preflight."
    return subprocess.run([
        powershell, "-NoProfile", "-File", str(SCRIPT),
        "-HostEnvironmentFile", str(directory / "host.fixture"),
        "-ApiEnvironmentFile", str(directory / "api.fixture"),
        "-WorkerEnvironmentFile", str(directory / "worker.fixture"),
        "-MigratorEnvironmentFile", str(directory / "migrator.fixture"),
        "-AgentEnvironmentFile", str(directory / "agent.fixture"),
    ], cwd=ROOT, capture_output=True, text=True, timeout=30, check=False)


def test_current_native_extraction_configuration_needs_no_retired_docling_settings(fixture_config):
    directory, values, models, migrator = fixture_config
    assert "InventoryExtraction__ApiKey" not in values
    assert "InventoryExtraction__BaseUrl" not in values
    assert values["AgentRuntime__BaseUrl"] == "http://agent-runtime:8080"
    write_fixture(directory, values, models, migrator)
    result = invoke_preflight(directory)
    assert result.returncode == 0, result.stdout + result.stderr


@pytest.mark.parametrize("defect,expected", [
    ("key_mismatch", "service keys do not match"),
    ("retired_agent", "retired or unknown agent"),
    ("model_not_allowed", "allow-listed active agent"),
    ("ambiguous_model_environment", "mounted JSON"),
    ("unsafe_http_runtime", "exact private Compose endpoint"),
    ("private_runtime_path", "exact private Compose endpoint"),
    ("public_nominatim", "non-public credential-free HTTPS provider endpoint"),
    ("public_overpass", "non-public credential-free HTTPS provider endpoint"),
    ("http_location", "non-public credential-free HTTPS provider endpoint"),
    ("different_env_file", "does not match the file being validated"),
    ("different_worker_file", "does not match the file being validated"),
    ("different_migrator_file", "does not match the file being validated"),
    ("shared_worker_connection", "API and worker database connections must be distinct"),
    ("api_worker_leak", "API environment must not contain the worker scheduler database connection"),
    ("worker_extra_secret", "Worker-only environment contains unrelated application configuration"),
    ("worker_missing_connection", "ConnectionStrings__WorkerSchedulerDatabase"),
    ("shared_migration_connection", "must be distinct from API and worker"),
    ("migrator_extra_secret", "contains unrelated application configuration"),
    ("migrator_missing_connection", "ADVERTIFIED_MIGRATION_CONNECTION_STRING"),
    ("mutable_image", "immutable sha256 digest"),
    ("inventory_paused", "must be 'false'"),
    ("semantic_disabled", "must be 'true'"),
    ("semantic_cap_mismatch", "do not reconcile"),
])
def test_preflight_rejects_configuration_defects_before_deployment(fixture_config, defect, expected):
    directory, values, models, migrator = fixture_config
    if defect == "key_mismatch":
        values["ADVERTIFIED_AGENT_RUNTIME_SERVICE_KEY"] = "different-fixture-service-key"
    elif defect == "retired_agent":
        values["AgentRuntime__CostCapsMinor__audience"] = "5"
    elif defect == "model_not_allowed":
        models["AgentRuntime"]["Models"]["audience_intelligence"] = "not-approved"
    elif defect == "ambiguous_model_environment":
        values["AgentRuntime__Models__brief_drafting__supplied_brief_understanding"] = "synthetic-config-test-model"
    elif defect == "unsafe_http_runtime":
        values["AgentRuntime__BaseUrl"] = "http://runtime.example:8080"
    elif defect == "private_runtime_path":
        values["AgentRuntime__BaseUrl"] = "http://agent-runtime:8080/invoke"
    elif defect == "public_nominatim":
        models["LocationIntelligence"]["Discovery"]["Endpoint"] = "https://nominatim.openstreetmap.org/"
    elif defect == "public_overpass":
        models["LocationIntelligence"]["PoiDiscovery"]["Endpoint"] = "https://overpass-api.de/api/interpreter"
    elif defect == "http_location":
        models["LocationIntelligence"]["Discovery"]["Endpoint"] = "http://location.example/"
    elif defect == "different_env_file":
        other = directory / "different-api.fixture"
        other.write_text("not the file under validation", encoding="utf-8")
        values["ADVERTIFIED_API_ENV_FILE"] = str(other)
    elif defect == "different_worker_file":
        other = directory / "different-worker.fixture"
        other.write_text("not the file under validation", encoding="utf-8")
        values["ADVERTIFIED_WORKER_ENV_FILE"] = str(other)
    elif defect == "different_migrator_file":
        other = directory / "different-migrator.fixture"
        other.write_text("not the file under validation", encoding="utf-8")
        values["ADVERTIFIED_MIGRATOR_ENV_FILE"] = str(other)
    elif defect == "shared_worker_connection":
        values["ConnectionStrings__WorkerSchedulerDatabase"] = values["ConnectionStrings__CommercialDatabase"]
    elif defect == "shared_migration_connection":
        migrator["ADVERTIFIED_MIGRATION_CONNECTION_STRING"] = values["ConnectionStrings__CommercialDatabase"]
    elif defect == "migrator_extra_secret":
        migrator["EmailAutomation__ResendApiKey"] = "must-not-be-here"
    elif defect == "migrator_missing_connection":
        del migrator["ADVERTIFIED_MIGRATION_CONNECTION_STRING"]
    elif defect == "mutable_image":
        values["ADVERTIFIED_API_IMAGE"] = "registry.example/fixture:latest"
    elif defect == "inventory_paused":
        values["InventoryProcessing__Paused"] = "true"
    elif defect == "semantic_disabled":
        values["InventorySemantic__Enabled"] = "false"
    elif defect == "semantic_cap_mismatch":
        values["InventorySemantic__PerCallCostCapUsdMicros"] = "60000"
    write_fixture(directory, values, models, migrator)
    if defect == "api_worker_leak":
        with (directory / "api.fixture").open("a", encoding="utf-8") as handle:
            handle.write("\nConnectionStrings__WorkerSchedulerDatabase=must-not-be-in-api")
    elif defect == "worker_extra_secret":
        with (directory / "worker.fixture").open("a", encoding="utf-8") as handle:
            handle.write("\nEmailAutomation__ResendApiKey=must-not-be-here")
    elif defect == "worker_missing_connection":
        (directory / "worker.fixture").write_text("", encoding="utf-8")
    result = invoke_preflight(directory)
    assert result.returncode != 0
    assert expected in result.stdout + result.stderr


def test_private_services_have_outbound_access_but_no_published_ports(fixture_config):
    directory, values, models, migrator = fixture_config
    write_fixture(directory, values, models, migrator)
    result = subprocess.run([
        "docker", "compose", "--env-file", str(directory / "host.fixture"),
        "--file", str(ROOT / "infrastructure/docker-compose.production.yml"),
        "--profile", "migration", "config", "--format", "json",
    ], cwd=ROOT, capture_output=True, text=True, timeout=30, check=False)
    assert result.returncode == 0, result.stderr
    resolved = json.loads(result.stdout)
    assert resolved["networks"]["backend"]["internal"] is True
    assert resolved["networks"]["agent"]["internal"] is True
    assert resolved["networks"]["outbound"].get("internal", False) is False
    for name in ("api", "worker", "agent-runtime", "migrator"):
        service = resolved["services"][name]
        assert "outbound" in service["networks"]
        assert not service.get("ports")
        assert service["read_only"] is True
    migrator_environment = resolved["services"]["migrator"]["environment"]
    assert migrator_environment["ADVERTIFIED_MIGRATION_CONNECTION_STRING"] == migrator[
        "ADVERTIFIED_MIGRATION_CONNECTION_STRING"
    ]
    assert "Authentication__Oidc__ClientSecret" not in migrator_environment
    assert "EmailAutomation__ResendApiKey" not in migrator_environment
    assert "AgentRuntime__ServiceKey" not in migrator_environment
    api_environment = resolved["services"]["api"]["environment"]
    worker_environment = resolved["services"]["worker"]["environment"]
    assert "ConnectionStrings__WorkerSchedulerDatabase" not in api_environment
    assert worker_environment["ConnectionStrings__WorkerSchedulerDatabase"] == values[
        "ConnectionStrings__WorkerSchedulerDatabase"
    ]
    assert api_environment["ConnectionStrings__CommercialDatabase"] == values[
        "ConnectionStrings__CommercialDatabase"
    ]
    assert worker_environment["ConnectionStrings__CommercialDatabase"] == values[
        "ConnectionStrings__CommercialDatabase"
    ]
    assert worker_environment["Process__Role"] == "Worker"
    assert "agent" in resolved["services"]["agent-runtime"]["networks"]
    assert "backend" not in resolved["services"]["agent-runtime"]["networks"]
    for name in ("api", "worker"):
        assert "agent" in resolved["services"][name]["networks"]
        mounts = resolved["services"][name]["volumes"]
        assert any(mount["target"] == "/app/appsettings.Production.json" and mount["read_only"] for mount in mounts)
    assert "agent" not in resolved["services"]["web"]["networks"]
    assert "clamav" not in resolved["services"]
    assert all(port["host_ip"] == "127.0.0.1" for port in resolved["services"]["web"]["ports"])

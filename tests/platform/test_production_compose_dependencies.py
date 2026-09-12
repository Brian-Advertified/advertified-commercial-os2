"""Production Compose dependencies and file-protection invariants."""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
COMPOSE = ROOT / "infrastructure" / "docker-compose.production.yml"
ENVIRONMENT = ROOT / "infrastructure" / "env.production.example"


def _service_block(text: str, service: str, next_service: str) -> str:
    return text.split(f"\n  {service}:\n", 1)[1].split(f"\n  {next_service}:\n", 1)[0]


def test_production_compose_has_no_local_malware_daemon() -> None:
    compose = COMPOSE.read_text(encoding="utf-8").lower()
    assert "clamav" not in compose
    assert "clamd" not in compose
    assert ":3310" not in compose
    assert "clamav_signatures" not in compose


def test_api_and_worker_do_not_wait_for_a_resident_scanner() -> None:
    compose = COMPOSE.read_text(encoding="utf-8")
    api = _service_block(compose, "api", "worker")
    worker = _service_block(compose, "worker", "web")
    for service in (api, worker):
        assert "clamav:" not in service.lower()
        assert "agent-runtime:" in service
        assert "condition: service_healthy" in service


def test_api_and_worker_database_credentials_are_separated() -> None:
    compose = COMPOSE.read_text(encoding="utf-8")
    api = _service_block(compose, "api", "worker")
    worker = _service_block(compose, "worker", "web")
    worker_environment = (ROOT / "infrastructure/env.worker.production.example").read_text(
        encoding="utf-8"
    )

    assert "ADVERTIFIED_WORKER_ENV_FILE" not in api
    assert "ADVERTIFIED_WORKER_ENV_FILE" in worker
    assert "ConnectionStrings__WorkerSchedulerDatabase" not in ENVIRONMENT.read_text(encoding="utf-8")
    active_worker_lines = [
        line for line in worker_environment.splitlines()
        if line.strip() and not line.startswith("#")
    ]
    assert active_worker_lines == [
        "ConnectionStrings__WorkerSchedulerDatabase=REPLACE_WITH_WORKER_DATABASE_CONNECTION"
    ]


def test_production_file_protection_uses_aws_s3_external_verdicts() -> None:
    environment = ENVIRONMENT.read_text(encoding="utf-8")
    assert "InventoryProtection__ObjectStoreMode=AwsS3" in environment
    assert "InventoryProtection__ScannerMode=ExternalVerdict" in environment
    assert "InventoryProtection__Bucket=REPLACE_WITH_PRIVATE_S3_BUCKET" in environment
    assert "InventoryProtection__AwsRegion=af-south-1" in environment
    assert "InventoryProtection__ScannerMode=Deterministic" not in environment
    assert "clamav" not in environment.lower()
    assert "3310" not in environment


def test_production_containers_ship_logs_to_the_monitoring_stack_group() -> None:
    compose = COMPOSE.read_text(encoding="utf-8")
    logging = compose.split("x-production-logging: &production-logging", 1)[1].split(
        "\nx-service-hardening:", 1
    )[0]
    assert "driver: awslogs" in logging
    assert "awslogs-region: af-south-1" in logging
    assert "awslogs-group: /advertified/production/application" in logging
    assert "awslogs-stream-prefix: advertified" in logging
    assert "logging: *production-logging" in compose
    assert "driver: json-file" not in compose

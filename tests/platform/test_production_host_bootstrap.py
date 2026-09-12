"""Guardrails for reproducible Advertified production EC2 host bootstrap."""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PRODUCTION = ROOT / "infrastructure" / "production"


def read(name: str) -> str:
    return (PRODUCTION / name).read_text(encoding="utf-8")


def test_common_bootstrap_is_bounded_to_supported_os_and_real_metrics() -> None:
    script = read("bootstrap-host-common.sh")
    assert 'ID:-} != "ubuntu"' in script
    assert 'VERSION_ID:-} != "24.04"' in script
    assert "apt-get install --yes --no-install-recommends" in script
    assert "docker.io" in script
    assert "docker-compose-v2" in script
    assert "awscli" in script
    assert "X-aws-ec2-metadata-token-ttl-seconds" in script
    assert "HostDiskUsedPercent" in script
    assert "DatabaseLogicalBackupAgeSeconds" in script
    assert "OnUnitActiveSec=5min" in script
    assert ":latest" not in script.lower()


def test_application_bootstrap_prepares_protected_release_and_key_directories() -> None:
    script = read("bootstrap-application-host.sh")
    assert "bootstrap-host-common.sh" in script
    assert "ADVERTIFIED_HOST_ROLE=application" in script
    assert "/var/lib/advertified/data-protection-keys" in script
    assert "/opt/advertified/releases" in script
    assert "docker run" not in script
    assert "Password=" not in script


def test_database_bootstrap_mounts_attached_ebs_by_identity_and_uuid() -> None:
    script = read("bootstrap-database-host.sh")
    assert "ADVERTIFIED_DATABASE_VOLUME_ID" in script
    assert "More than one unmounted data disk is present" in script
    assert "lsblk -dn -o NAME,SERIAL" in script
    assert "blkid -s UUID" in script
    assert "UUID=%s /var/lib/advertified/postgres" in script
    assert "mountpoint -q /var/lib/advertified/postgres" in script
    assert "advertified-database-backup.timer" in script
    assert "advertified-database-restore" in script
    assert "advertified-provision-database-logins" in script
    assert "02-provision-database-roles.sql" in script
    assert "OnCalendar=*-*-* 01:15:00 UTC" in script
    assert "ADVERTIFIED_DATABASE_SECRET_ARN" in script
    assert "ADVERTIFIED_BACKUP_BUCKET" in script


def test_database_activation_is_digest_only_private_and_does_not_put_password_in_docker_env() -> None:
    script = read("activate-production-database.sh")
    assert "@sha256:[0-9a-f]{64}" in script
    assert "secretsmanager get-secret-value" in script
    assert "'.admin.username'" in script
    assert "'.admin.password'" in script
    assert "advertified-provision-database-logins" in script
    assert "ecr get-login-password" in script
    assert "POSTGRES_PASSWORD_FILE=/run/secrets/postgres_password" in script
    assert "--volume /run/advertified-postgres/password:/run/secrets/postgres_password:ro" in script
    assert '--publish "${PRIVATE_IP}:5432:5432"' in script
    assert "POSTGRES_PASSWORD=${" not in script
    assert "advertified-healthcheck" in script


def test_database_login_provisioner_enforces_distinct_least_privilege_roles() -> None:
    script = read("provision-production-database-logins.sh")
    assert "'.admin.username'" in script
    assert "'.api.username'" in script
    assert "'.worker.username'" in script
    assert "'.migrator.username'" in script
    assert "API, worker and migrator database logins must be distinct" in script
    assert "advertified_app" in script
    assert "advertified_worker" in script
    assert "advertified_migrator" in script
    assert '--dbname "${DB_NAME}"' in script
    assert "NOSUPERUSER" in script
    assert "NOBYPASSRLS" in script


def test_logical_backup_is_custom_format_hashed_encrypted_and_observable() -> None:
    script = read("run-production-database-backup.sh")
    assert "pg_dump" in script
    assert "--format=custom" in script
    assert "--no-owner" in script
    assert "sha256sum" in script
    assert "--sse AES256" in script
    assert "DatabaseLogicalBackupFailures" in script
    assert "/var/lib/advertified/backup/last-success-utc" in script
    assert "s3://${ADVERTIFIED_BACKUP_BUCKET}/${KEY}.sha256" in script


def test_restore_is_checksum_verified_isolated_and_non_destructive_by_default() -> None:
    script = read("restore-production-database-backup.sh")
    assert "ISOLATED_REPLACEMENT_DATA_HOST" in script
    assert "sha256sum" in script
    assert "Backup checksum verification failed" in script
    assert "Restore target already contains Advertified commercial tables" in script
    assert "pg_restore" in script
    assert "--exit-on-error" in script
    assert "postgis" in script and "vector" in script
    assert 'advertified.database-restore-receipt.v1' in script
    assert "DROP DATABASE" not in script


def test_bootstrap_scripts_do_not_embed_credentials_or_mutable_application_images() -> None:
    combined = "\n".join(
        read(name)
        for name in (
            "bootstrap-host-common.sh",
            "bootstrap-application-host.sh",
            "bootstrap-database-host.sh",
            "activate-production-database.sh",
            "run-production-database-backup.sh",
            "restore-production-database-backup.sh",
            "provision-production-database-logins.sh",
        )
    )
    for forbidden in ("AKIA", "SecretAccessKey", "REPLACE_WITH_SECRET", ":latest"):
        assert forbidden not in combined

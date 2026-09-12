"""Embed reviewed production host bootstrap scripts into EC2 UserData."""
from __future__ import annotations

import base64
import gzip
import hashlib
import io
import json
import tarfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PRODUCTION = ROOT / "infrastructure" / "production"
STACK = PRODUCTION / "launch-stack.json"
USER_DATA_LIMIT = 16 * 1024


def source_path(name: str) -> Path:
    if name == "02-provision-database-roles.sql":
        return ROOT / "infrastructure" / "init-scripts" / name
    return PRODUCTION / name


def source(name: str) -> str:
    return source_path(name).read_text(encoding="utf-8").replace("\r\n", "\n")


def sha256(text: str) -> str:
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def bundle(names: list[str]) -> str:
    buffer = io.BytesIO()
    with gzip.GzipFile(fileobj=buffer, mode="wb", mtime=0) as compressed:
        with tarfile.open(fileobj=compressed, mode="w") as archive:
            for name in sorted(names):
                payload = source(name).encode("utf-8")
                info = tarfile.TarInfo(name=name)
                info.size = len(payload)
                info.mode = 0o644 if name.endswith(".sql") else 0o755
                info.uid = 0
                info.gid = 0
                info.uname = "root"
                info.gname = "root"
                info.mtime = 0
                archive.addfile(info, io.BytesIO(payload))
    return base64.b64encode(buffer.getvalue()).decode("ascii")


def user_data(parts: list[object]) -> dict:
    literal_size = sum(len(item.encode("utf-8")) for item in parts if isinstance(item, str))
    if literal_size >= USER_DATA_LIMIT:
        raise ValueError(
            f"Rendered EC2 UserData source is {literal_size} bytes; limit is {USER_DATA_LIMIT}."
        )
    return {"Fn::Base64": {"Fn::Join": ["", parts]}}


def bootstrap_parts(bundle_text: str, command: str, extra: list[object] | None = None) -> list[object]:
    parts: list[object] = [
        "#!/usr/bin/env bash\nset -euo pipefail\n",
        "install -d -m 0755 /opt/advertified/bootstrap\n",
        "printf '%s' '",
        bundle_text,
        "' | base64 --decode | tar -xz -C /opt/advertified/bootstrap\n",
        "export ADVERTIFIED_AWS_REGION='",
        {"Ref": "AWS::Region"},
        "'\n",
    ]
    if extra:
        parts.extend(extra)
    parts.append(command)
    return parts


def main() -> int:
    template = json.loads(STACK.read_text(encoding="utf-8"))
    scripts = {
        name: source(name)
        for name in (
            "bootstrap-host-common.sh",
            "bootstrap-application-host.sh",
            "bootstrap-database-host.sh",
            "activate-production-database.sh",
            "run-production-database-backup.sh",
            "restore-production-database-backup.sh",
            "provision-production-database-logins.sh",
            "02-provision-database-roles.sql",
        )
    }

    app_bundle = bundle(["bootstrap-host-common.sh", "bootstrap-application-host.sh"])
    data_bundle = bundle([
        "bootstrap-host-common.sh",
        "bootstrap-database-host.sh",
        "activate-production-database.sh",
        "run-production-database-backup.sh",
        "restore-production-database-backup.sh",
        "provision-production-database-logins.sh",
        "02-provision-database-roles.sql",
    ])
    app_parts = bootstrap_parts(
        app_bundle,
        "/opt/advertified/bootstrap/bootstrap-application-host.sh\n",
    )
    data_parts = bootstrap_parts(
        data_bundle,
        "/opt/advertified/bootstrap/bootstrap-database-host.sh\n",
        [
            "export ADVERTIFIED_BACKUP_BUCKET='",
            {"Ref": "BackupBucket"},
            "'\nexport ADVERTIFIED_DATABASE_SECRET_ARN='",
            {"Ref": "DatabaseSecretsArn"},
            "'\n",
        ],
    )

    template["Parameters"]["AppAmiId"]["Description"] = (
        "Ubuntu Server 24.04 LTS x86_64 AMI; UserData fails closed on another OS."
    )
    template["Parameters"]["DataAmiId"]["Description"] = (
        "Ubuntu Server 24.04 LTS x86_64 AMI; UserData fails closed on another OS."
    )
    template["Resources"]["ApplicationHost"]["Properties"]["UserData"] = user_data(app_parts)
    template["Resources"]["DatabaseHost"]["Properties"]["UserData"] = user_data(data_parts)
    template["Metadata"] = {
        "AdvertifiedBootstrap": {
            "CommonSha256": sha256(scripts["bootstrap-host-common.sh"]),
            "ApplicationSha256": sha256(scripts["bootstrap-application-host.sh"]),
            "DatabaseSha256": sha256(scripts["bootstrap-database-host.sh"]),
            "DatabaseActivationSha256": sha256(scripts["activate-production-database.sh"]),
            "DatabaseBackupSha256": sha256(scripts["run-production-database-backup.sh"]),
            "DatabaseRestoreSha256": sha256(scripts["restore-production-database-backup.sh"]),
            "DatabaseLoginProvisionerSha256": sha256(scripts["provision-production-database-logins.sh"]),
            "DatabaseGroupRolesSqlSha256": sha256(scripts["02-provision-database-roles.sql"]),
        }
    }
    STACK.write_text(json.dumps(template, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(STACK)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

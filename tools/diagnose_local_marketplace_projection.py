"""Run the reviewed-inventory Marketplace projection against an inspected local Postgres container.

This diagnostic is deliberately limited to non-production Advertified Compose PostgreSQL containers.
It exists to surface the exact PostgreSQL error when a real reviewed corpus no longer matches the
Marketplace snapshot contract. It is not a production deployment or publication tool.
"""
from __future__ import annotations

import argparse
import json
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PROJECTION = ROOT / "infrastructure" / "development" / "publish-current-inventory-to-marketplace.sql"


def inspect_container(name: str) -> dict:
    result = subprocess.run(
        ["docker", "inspect", name], cwd=ROOT, capture_output=True, text=True, check=False
    )
    if result.returncode != 0:
        raise RuntimeError(result.stderr.strip() or f"Container not found: {name}")
    records = json.loads(result.stdout)
    if len(records) != 1:
        raise RuntimeError("Expected exactly one inspected container.")
    record = records[0]
    labels = record.get("Config", {}).get("Labels") or {}
    project = str(labels.get("com.docker.compose.project", ""))
    service = str(labels.get("com.docker.compose.service", ""))
    if not project.startswith("advertified") or "production" in project.lower():
        raise RuntimeError("Refusing to run against a production or unrelated Compose project.")
    if service != "postgres":
        raise RuntimeError("The selected container is not the Advertified PostgreSQL service.")
    if record.get("HostConfig", {}).get("Privileged") is True:
        raise RuntimeError("Refusing to run against a privileged container.")
    if record.get("State", {}).get("Running") is not True:
        raise RuntimeError("The selected PostgreSQL container is not running.")
    return record


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--container",
        required=True,
        help="Exact inspected non-production Advertified PostgreSQL container name.",
    )
    args = parser.parse_args()
    inspect_container(args.container)
    sql = PROJECTION.read_text(encoding="utf-8").replace("\\set ON_ERROR_STOP on", "")
    result = subprocess.run(
        [
            "docker", "exec", "--user", "postgres", "--interactive", args.container,
            "psql", "--set", "ON_ERROR_STOP=1", "--username", "advertified",
            "--dbname", "advertified",
        ],
        cwd=ROOT,
        input=sql,
        capture_output=True,
        text=True,
        check=False,
    )
    if result.stdout:
        print(result.stdout, end="")
    if result.stderr:
        print(result.stderr, end="")
    return result.returncode


if __name__ == "__main__":
    raise SystemExit(main())

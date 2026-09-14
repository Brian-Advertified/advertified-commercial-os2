"""Read a bounded slice of the canonical local Advertified API logs safely."""
from __future__ import annotations

import argparse
import json
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CONTAINER = "advertified-os2-dev-api-1"
PROJECT = "advertified-os2-dev"
SERVICE = "api"
SECRET = re.compile(r"(?i)(password|secret|token|authorization|servicekey)(\s*[=:]\s*)([^\s,;]+)")
CONTROL = re.compile(r"[\x00-\x08\x0b\x0c\x0e-\x1f\x7f]")
ANSI = re.compile(r"\x1b\[[0-?]*[ -/]*[@-~]")


def inspect_container() -> None:
    result = subprocess.run(
        ["docker", "inspect", CONTAINER], cwd=ROOT, capture_output=True, check=False)
    if result.returncode != 0:
        raise RuntimeError("The canonical Advertified development API container is unavailable.")
    record = json.loads(result.stdout.decode("utf-8"))[0]
    labels = record.get("Config", {}).get("Labels", {})
    mounts = record.get("Mounts", [])
    unsafe_mount = any(
        "docker.sock" in str(item.get("Source", "")).lower()
        or "docker.sock" in str(item.get("Destination", "")).lower()
        for item in mounts
    )
    if not (
        record.get("Name") == f"/{CONTAINER}"
        and record.get("State", {}).get("Running") is True
        and labels.get("com.docker.compose.project") == PROJECT
        and labels.get("com.docker.compose.service") == SERVICE
        and record.get("HostConfig", {}).get("Privileged") is not True
        and record.get("HostConfig", {}).get("NetworkMode") != "host"
        and not unsafe_mount
    ):
        raise RuntimeError("Refusing to inspect logs outside the canonical non-production API container.")


def redact(value: str) -> str:
    value = ANSI.sub("", value)
    value = CONTROL.sub("?", value)
    return SECRET.sub(lambda match: f"{match.group(1)}{match.group(2)}[REDACTED]", value)


def read_logs(tail: int) -> list[str]:
    result = subprocess.run(
        ["docker", "logs", "--tail", str(tail), CONTAINER],
        cwd=ROOT, capture_output=True, check=False)
    combined = result.stdout + result.stderr
    if result.returncode != 0:
        raise RuntimeError("Unable to read the canonical local API logs.")
    return redact(combined.decode("utf-8", errors="replace")).splitlines()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--correlation")
    parser.add_argument("--tail", type=int, default=200)
    parser.add_argument("--last", action="store_true")
    args = parser.parse_args()
    if args.tail < 20 or args.tail > 2000:
        raise ValueError("tail must be between 20 and 2000")
    inspect_container()
    lines = read_logs(args.tail)
    if not args.correlation:
        print("\n".join(lines))
        return 0
    matches = [index for index, line in enumerate(lines) if args.correlation in line]
    if not matches:
        print("Correlation was not found in the bounded local API log window.")
        return 2
    if args.last:
        start = max(0, matches[-1] - 40)
        end = min(len(lines), matches[-1] + 41)
    else:
        start = max(0, min(matches) - 30)
        end = min(len(lines), max(matches) + 31)
    print("\n".join(lines[start:end]))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

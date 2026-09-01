"""Immutable third-party inputs for CI and local infrastructure evidence."""

from __future__ import annotations

import re
import subprocess
from pathlib import Path

import pytest


REPO_ROOT = Path(__file__).resolve().parents[2]
FULL_COMMIT = re.compile(r"[0-9a-f]{40}")
IMAGE_DIGEST = re.compile(r"sha256:[0-9a-f]{64}")
USES_LINE = re.compile(r"^(\s*)-\s+uses:\s*([^\s#]+)")
LOCAL_BUILD_REPOSITORIES = frozenset({"advertified/postgres-dev"})


def immutable_action_reference(reference: str) -> bool:
    if reference.startswith("./"):
        return True
    if reference.startswith("docker://"):
        return immutable_image_reference(reference.removeprefix("docker://"))
    if "@" not in reference:
        return False
    action, revision = reference.rsplit("@", 1)
    return "/" in action and FULL_COMMIT.fullmatch(revision) is not None


def action_reference_violations(text: str) -> list[str]:
    violations = []
    for line_number, line in enumerate(text.splitlines(), start=1):
        match = USES_LINE.match(line)
        if match and not immutable_action_reference(match.group(2)):
            violations.append(f"line {line_number}: mutable action {match.group(2)}")
    return violations


def checkout_credential_violations(text: str) -> list[str]:
    lines = text.splitlines()
    violations = []
    for index, line in enumerate(lines):
        match = USES_LINE.match(line)
        if not match or not match.group(2).startswith("actions/checkout@"):
            continue
        block = step_block(lines, index, len(match.group(1)))
        if not any(re.match(r"^\s+persist-credentials:\s*false\s*$", item) for item in block):
            violations.append(f"line {index + 1}: checkout persists credentials")
    return violations


def step_block(lines: list[str], start: int, indentation: int) -> list[str]:
    marker = " " * indentation + "- "
    block = []
    for line in lines[start + 1 :]:
        if line.startswith(marker):
            break
        block.append(line)
    return block


def immutable_image_reference(reference: str) -> bool:
    if reference.count("@") != 1:
        return False
    versioned_name, digest = reference.rsplit("@", 1)
    image_name = versioned_name.rsplit("/", 1)[-1]
    return ":" in image_name and IMAGE_DIGEST.fullmatch(digest) is not None


def image_repository(reference: str) -> str:
    versioned_name = reference.split("@", 1)[0]
    slash = versioned_name.rfind("/")
    colon = versioned_name.find(":", slash + 1)
    return versioned_name if colon < 0 else versioned_name[:colon]


def compose_services(text: str) -> dict[str, dict[str, str | bool]]:
    services: dict[str, dict[str, str | bool]] = {}
    in_services = False
    current: dict[str, str | bool] | None = None
    for line in text.splitlines():
        if line == "services:":
            in_services = True
            continue
        if in_services and line and not line[0].isspace():
            break
        service = re.match(r"^  ([a-zA-Z0-9_-]+):\s*$", line)
        if service:
            current = services.setdefault(service.group(1), {"build": False})
            continue
        if current is None:
            continue
        if re.match(r"^    build:\s*", line):
            current["build"] = True
        image = re.match(r"^    image:\s*([^\s#]+)", line)
        if image:
            current["image"] = image.group(1).strip("\"'")
    return services


def compose_image_violations(text: str) -> list[str]:
    violations = []
    for service, configuration in compose_services(text).items():
        reference = configuration.get("image")
        if not isinstance(reference, str):
            continue
        repository = image_repository(reference)
        if repository in LOCAL_BUILD_REPOSITORIES:
            if configuration["build"] is not True:
                violations.append(f"{service}: local image is not build-backed")
            continue
        if not immutable_image_reference(reference):
            violations.append(f"{service}: mutable image {reference}")
    return violations


def dockerfile_from_violations(text: str) -> list[str]:
    violations = []
    aliases: set[str] = set()
    for line_number, line in enumerate(text.splitlines(), start=1):
        tokens = line.strip().split()
        if not tokens or tokens[0].upper() != "FROM":
            continue
        values = [token for token in tokens[1:] if not token.startswith("--")]
        if not values:
            violations.append(f"line {line_number}: FROM has no image")
            continue
        reference = values[0]
        if reference.lower() not in aliases and reference.lower() != "scratch":
            if not immutable_image_reference(reference):
                violations.append(f"line {line_number}: mutable base {reference}")
        if len(values) >= 3 and values[-2].upper() == "AS":
            aliases.add(values[-1].lower())
    return violations


def repository_dockerfiles() -> list[Path]:
    listed = subprocess.run(
        ["git", "ls-files", "--cached", "--others", "--exclude-standard"],
        cwd=REPO_ROOT,
        check=True,
        capture_output=True,
        text=True,
    ).stdout.splitlines()
    return [REPO_ROOT / item for item in listed if Path(item).name.startswith("Dockerfile")]


def repository_build_input_violations() -> list[str]:
    violations = []
    workflow_root = REPO_ROOT / ".github" / "workflows"
    workflows = sorted((*workflow_root.glob("*.yml"), *workflow_root.glob("*.yaml")))
    for path in workflows:
        text = path.read_text(encoding="utf-8")
        for issue in action_reference_violations(text) + checkout_credential_violations(text):
            violations.append(f"{path.relative_to(REPO_ROOT).as_posix()}: {issue}")
    compose_path = REPO_ROOT / "infrastructure" / "docker-compose.yml"
    for issue in compose_image_violations(compose_path.read_text(encoding="utf-8")):
        violations.append(f"infrastructure/docker-compose.yml: {issue}")
    for path in repository_dockerfiles():
        for issue in dockerfile_from_violations(path.read_text(encoding="utf-8")):
            violations.append(f"{path.relative_to(REPO_ROOT).as_posix()}: {issue}")
    return violations


def test_valid_immutable_workflow_fixture() -> None:
    workflow = """
      - uses: actions/checkout@11d5960a326750d5838078e36cf38b85af677262 # v4
        with:
          persist-credentials: false
      - uses: ./local-action
    """

    assert not action_reference_violations(workflow)
    assert not checkout_credential_violations(workflow)


@pytest.mark.parametrize(
    "reference",
    ["actions/checkout@v4", "actions/checkout@abc123", "actions/checkout"],
)
def test_action_detector_rejects_mutable_or_malformed_reference(reference: str) -> None:
    assert action_reference_violations(f"      - uses: {reference}\n")


def test_checkout_detector_rejects_persisted_credentials() -> None:
    workflow = """
      - uses: actions/checkout@11d5960a326750d5838078e36cf38b85af677262
      - run: python -m pytest
    """

    assert checkout_credential_violations(workflow)


def test_valid_immutable_container_fixtures() -> None:
    compose = """
services:
  postgres:
    build:
      context: ./postgres
    image: advertified/postgres-dev:16
  redis:
    image: redis:7.4.2-alpine@sha256:02419de7eddf55aa5bcf49efb74e88fa8d931b4d77c07eff8a6b2144472b6952
"""
    dockerfile = """
FROM example/runtime:1@sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa AS runtime
FROM runtime AS final
"""

    assert not compose_image_violations(compose)
    assert not dockerfile_from_violations(dockerfile)


def test_container_detectors_reject_controlled_violations() -> None:
    mutable_compose = """
services:
  postgres:
    image: advertified/postgres-dev:16
  redis:
    image: redis:7.4.2-alpine
  minio:
    image: minio/minio:release@sha256:1234
"""
    malformed_dockerfile = "FROM example/runtime:1@sha256:1234\n"

    assert len(compose_image_violations(mutable_compose)) == 3
    assert dockerfile_from_violations(malformed_dockerfile)


def test_repository_build_inputs_are_immutable() -> None:
    violations = repository_build_input_violations()

    assert not violations, "\n".join(violations)

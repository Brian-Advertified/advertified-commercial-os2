"""Reproducible, least-privilege application packaging guardrails."""

from __future__ import annotations

import re
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[2]
PROJECT_LOCKS = (
    "api/packages.lock.json",
    "api/src/Advertified.Commercial.Application/packages.lock.json",
    "api/src/Advertified.Commercial.DatabaseMigrator/packages.lock.json",
    "api/src/Advertified.Commercial.Domain/packages.lock.json",
    "api/src/Advertified.Commercial.Infrastructure/packages.lock.json",
    "api/tests/Advertified.Commercial.Api.Tests/packages.lock.json",
)


def read(relative_path: str) -> str:
    return (REPO_ROOT / relative_path).read_text(encoding="utf-8")


def final_stage_has_user(dockerfile: str) -> bool:
    from_offsets = [match.start() for match in re.finditer(r"(?im)^FROM\s+", dockerfile)]
    return bool(from_offsets) and re.search(
        r"(?im)^USER\s+[^\s#]+", dockerfile[from_offsets[-1] :]
    ) is not None


def named_stage_has_user(dockerfile: str, stage_name: str) -> bool:
    stages = list(re.finditer(r"(?im)^FROM\s+.*?\s+AS\s+([a-z0-9_-]+)\s*$", dockerfile))
    for index, stage in enumerate(stages):
        if stage.group(1).lower() != stage_name.lower():
            continue
        end = stages[index + 1].start() if index + 1 < len(stages) else len(dockerfile)
        return re.search(r"(?im)^USER\s+[^\s#]+", dockerfile[stage.start() : end]) is not None
    return False


def test_application_packaging_inputs_exist() -> None:
    required = (
        ".dockerignore",
        "api/Dockerfile",
        "agent-runtime/Dockerfile",
        "web/Dockerfile",
        "ADVERTIFIED.md",
        *PROJECT_LOCKS,
    )

    missing = [item for item in required if not (REPO_ROOT / item).is_file()]

    assert not missing, f"Missing packaging inputs: {missing}"


def test_dotnet_projects_enforce_locked_restore() -> None:
    build_properties = read("Directory.Build.props")
    sdk_selection = read("global.json")
    api_project = read("api/Advertified.Commercial.Api.csproj")

    assert "<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>" in build_properties
    assert "<RestoreLockedMode>true</RestoreLockedMode>" in build_properties
    assert '"version": "10.0.400"' in sdk_selection
    assert '"rollForward": "disable"' in sdk_selection
    assert (
        '<Content Update="appsettings.Development.json" '
        'CopyToPublishDirectory="Never" />'
    ) in api_project
    assert all((REPO_ROOT / item).stat().st_size > 0 for item in PROJECT_LOCKS)


def test_application_images_install_only_from_dependency_locks() -> None:
    api = read("api/Dockerfile")
    runtime = read("agent-runtime/Dockerfile")
    web = read("web/Dockerfile")

    assert "--locked-mode" in api
    assert "--require-hashes" in runtime and "requirements.lock" in runtime
    assert "python:3.12.14-slim-trixie@sha256:" in runtime
    assert "dpkg --purge --force-remove-essential gzip" in runtime
    assert re.search(r"(?im)^RUN\s+npm\s+ci(?:\s|$)", web)
    assert "npm install" not in web


def test_application_final_images_are_non_root_and_process_specific() -> None:
    dockerfiles = {
        "api": read("api/Dockerfile"),
        "agent-runtime": read("agent-runtime/Dockerfile"),
        "web": read("web/Dockerfile"),
    }

    root_images = [
        name
        for name in ("agent-runtime", "web")
        if not final_stage_has_user(dockerfiles[name])
    ]
    assert not root_images, f"Final image stages without an explicit USER: {root_images}"
    assert named_stage_has_user(dockerfiles["api"], "api")
    assert named_stage_has_user(dockerfiles["api"], "migrator")
    assert "worker" not in dockerfiles["api"].lower()
    assert "setcap -r /usr/bin/caddy" in dockerfiles["web"]
    assert "chmod 0700 /config/caddy /data/caddy" in dockerfiles["web"]


def test_named_stage_user_detector_rejects_one_root_target() -> None:
    fixture = """
FROM example/build:1@sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa AS build
FROM build AS api
ENTRYPOINT ["api"]
FROM build AS migrator
USER app
ENTRYPOINT ["migrator"]
"""

    assert not named_stage_has_user(fixture, "api")
    assert named_stage_has_user(fixture, "migrator")


def test_docker_context_excludes_local_and_generated_state() -> None:
    exclusions = set(read(".dockerignore").splitlines())
    required = {
        ".git",
        ".artifacts",
        ".env",
        ".env.*",
        "**/.env",
        "**/.env.*",
        "**/credentials",
        "**/credentials/**",
        "**/secrets",
        "**/secrets/**",
        "**/*.creds",
        "**/*.crt",
        "**/*.key",
        "**/*.p12",
        "**/*.pem",
        "**/*.pfx",
        "**/bin",
        "**/obj",
        "**/node_modules",
    }

    assert required <= exclusions


def test_ci_selects_exact_application_toolchains_and_locked_restore() -> None:
    workflow = read(".github/workflows/ci.yml")

    assert 'python-version: "3.12.14"' in workflow
    assert 'node-version: "22.23.2"' in workflow
    assert 'dotnet-version: "10.0.400"' in workflow
    assert "--locked-mode" in workflow


def test_ci_builds_and_smokes_every_application_image_without_push() -> None:
    workflow = read(".github/workflows/ci.yml")
    required_builds = (
        "docker build --file api/Dockerfile --target api",
        "docker build --file api/Dockerfile --target migrator",
        "docker build --file agent-runtime/Dockerfile",
        "docker build --file web/Dockerfile",
    )

    assert "application-packaging:" in workflow
    assert all(command in workflow for command in required_builds)
    assert "docker push" not in workflow
    assert "No database change was made." in workflow
    assert "appsettings.Development.json" in workflow


def test_web_cache_policy_only_marks_fingerprinted_bundles_immutable() -> None:
    caddy = read("web/Caddyfile")
    workflow = read(".github/workflows/ci.yml")

    assert "@fingerprintedAssets path_regexp fingerprinted" in caddy
    assert "^/assets/[^/]+-[A-Za-z0-9_-]{8}\\.(css|js)$" in caddy
    assert '@publicAssets path /assets/*' in caddy
    assert 'header Cache-Control "no-cache"' in caddy
    assert "assets/media-inventory/radio.jpg" in workflow
    assert "Unversioned public assets must not be cached as immutable." in workflow


def test_ci_generates_retains_and_scans_final_image_sboms() -> None:
    workflow = read(".github/workflows/ci.yml")
    policy = read("ADVERTIFIED.md")
    syft = (
        "ghcr.io/anchore/syft:v1.50.0@sha256:"
        "1288ea4c8b38767b4e620c1e312c8cb26b6e887a99b4f07ab6cd19fc6f225026"
    )
    trivy = (
        "ghcr.io/aquasecurity/trivy:0.73.0@sha256:"
        "7cced7cae583819fc7806d4cbc0dbbc7cad18b99f7d3e235192e6da8c091045c"
    )

    assert syft in workflow
    assert trivy in workflow
    assert "aquasecurity/trivy:0.69.4" not in workflow
    assert "--severity HIGH,CRITICAL" in workflow
    assert "--exit-code 1" in workflow
    assert "--db-repository ghcr.io/aquasecurity/trivy-db:2" in workflow
    assert "--timeout 20m" in workflow
    assert "cyclonedx-json" in workflow
    assert "actions/upload-artifact@ea165f8d65b6e75b540449e92b4886f43607fa02" in workflow
    assert "build provenance/SBOM/dependency and secret scans" in policy
    assert "One unresolved release-blocking check remains a NO-GO" in policy


def test_ci_runs_a_pinned_blocking_secret_scan_and_retains_the_report() -> None:
    workflow = read(".github/workflows/ci.yml")
    gitleaks = (
        "ghcr.io/gitleaks/gitleaks:v8.30.0@sha256:"
        "691af3c7c5a48b16f187ce3446d5f194838f91238f27270ed36eef6359a574d9"
    )

    assert gitleaks in workflow
    assert 'git archive --format=tar HEAD' in workflow
    assert '"$GITLEAKS_IMAGE" dir /source' in workflow
    assert "--redact" in workflow
    assert "--report-path /out/gitleaks.json" in workflow
    assert "source-secret-scan-${{ github.sha }}" in workflow

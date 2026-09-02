"""Executable repository and architecture guardrails."""

from __future__ import annotations

import json
import hashlib
import re
import subprocess
from pathlib import Path

from tests.architecture.architecture_rules import python_import_violations

REPO_ROOT = Path(__file__).resolve().parents[2]
GENERATED_PARTS = {
    ".git",
    ".artifacts",
    ".pytest_cache",
    ".venv",
    "__pycache__",
    "bin",
    "dist",
    "node_modules",
    "obj",
    "tmp",
    "temp",
    "Generated",
    "generated",
}
SOURCE_SUFFIXES = {".cs", ".css", ".js", ".jsx", ".py", ".ts", ".tsx"}
FORBIDDEN_AGENT_PACKAGES = {
    "asyncpg",
    "langchain",
    "psycopg",
    "psycopg2-binary",
    "sqlalchemy",
}
APPROVED_AGENT_PROVIDER_PACKAGES = {"botocore"}
GOVERNED_LITERAL_OVERLAP_ALLOWLIST = {
    "api/src/Advertified.Commercial.Application/Opportunity/AgentProviderMetadata.cs": {"LIVE"},
    "api/src/Advertified.Commercial.Infrastructure/Inventory/InventoryCandidateNormalizer.cs": {
        "BLACKOUT", "NOT_AVAILABLE", "impressions", "listeners", "reach",
    },
}
from tests.architecture.master_data_contract import (
    REQUIRED_MASTER_COLLECTIONS,
    REQUIRED_PERMISSION_ROLES,
)


def source_files(root: Path = REPO_ROOT) -> list[Path]:
    return [
        path
        for path in root.rglob("*")
        if path.is_file()
        and path.suffix in SOURCE_SUFFIXES
        and not GENERATED_PARTS.intersection(path.parts)
        and not is_generated_source(path)
    ]


def is_generated_source(path: Path) -> bool:
    first_line = path.read_text(encoding="utf-8").splitlines()[:1]
    return bool(first_line) and (
        "auto-generated" in first_line[0].lower()
        or "generated from shared/contracts/master-data.json" in first_line[0].lower()
    )


def relative(path: Path) -> str:
    return path.relative_to(REPO_ROOT).as_posix()


def test_required_baseline_files_exist() -> None:
    required = [
        "AGENTS.md",
        "web/package-lock.json",
        "api/Advertified.Commercial.Api.csproj",
        "agent-runtime/requirements-dev.txt",
        "infrastructure/postgres/Dockerfile",
        "ADVERTIFIED.md",
    ]

    missing = [path for path in required if not (REPO_ROOT / path).is_file()]

    assert not missing, f"Missing baseline files: {missing}"


def test_authored_source_files_do_not_exceed_400_lines() -> None:
    violations = {
        relative(path): len(path.read_text(encoding="utf-8").splitlines())
        for path in source_files()
        if len(path.read_text(encoding="utf-8").splitlines()) > 400
    }

    assert not violations, f"Source files over 400 lines: {violations}"


def test_ooh_only_is_not_a_parallel_application_workflow() -> None:
    rapid_ooh = re.compile(r"rapid[-_]?ooh", re.IGNORECASE)
    forbidden_paths = [
        relative(path)
        for path in source_files()
        if rapid_ooh.search(relative(path))
    ]
    forbidden_symbols = []
    patterns = (
        rapid_ooh,
        re.compile(r"campaign[-_]?route", re.IGNORECASE),
    )
    for path in source_files():
        if path.resolve() == Path(__file__).resolve():
            continue
        content = path.read_text(encoding="utf-8")
        if any(pattern.search(content) for pattern in patterns):
            forbidden_symbols.append(relative(path))

    assert not forbidden_paths, (
        "OOH-only must use canonical planning, not a RapidOoh source tree: "
        f"{forbidden_paths}"
    )
    assert not forbidden_symbols, (
        "OOH-only must not expose a parallel RapidOoh workflow: "
        f"{forbidden_symbols}"
    )


def test_web_has_no_server_or_persistence_imports() -> None:
    forbidden = re.compile(
        r"""(?:from\s+|import\s*\()\s*["'](?:node:fs|fs|pg|postgres|prisma|"""
        r"""@aws-sdk|.*(?:server|persistence|database).*)["']""",
        re.IGNORECASE,
    )
    violations = []

    for path in (REPO_ROOT / "web" / "src").rglob("*"):
        if path.suffix in {".ts", ".tsx"} and forbidden.search(path.read_text(encoding="utf-8")):
            violations.append(relative(path))

    assert not violations, f"Web boundary imports found: {violations}"


def test_browser_tokens_and_notification_adapter_stay_inside_their_boundaries() -> None:
    toast_violations = []
    browser_token_violations = []
    forbidden_tokens = re.compile(r"(?:localStorage|accessToken|refreshToken|bearerToken)")

    for path in (REPO_ROOT / "web" / "src").rglob("*"):
        if path.suffix not in {".ts", ".tsx"}:
            continue
        content = path.read_text(encoding="utf-8")
        if "react-toastify" in content and "notifications" not in path.parts:
            toast_violations.append(relative(path))
        if forbidden_tokens.search(content):
            browser_token_violations.append(relative(path))

    assert not toast_violations, f"Toast adapter leaked into components: {toast_violations}"
    assert not browser_token_violations, (
        f"Browser token storage or fields found: {browser_token_violations}"
    )


def test_commercial_api_contains_no_model_prompts() -> None:
    prompt_terms = re.compile(
        r"system\s+prompt|user\s+prompt|prompt\s+template|completion\s+prompt",
        re.IGNORECASE,
    )
    violations = []

    for path in (REPO_ROOT / "api").rglob("*.cs"):
        if not GENERATED_PARTS.intersection(path.parts) and prompt_terms.search(
            path.read_text(encoding="utf-8")
        ):
            violations.append(relative(path))

    assert not violations, f"Model prompt content found in Commercial API: {violations}"


def test_agent_runtime_keeps_database_and_provider_sdks_inside_owned_boundaries() -> None:
    violations = python_import_violations(REPO_ROOT / "agent-runtime")

    requirements = (REPO_ROOT / "agent-runtime" / "requirements.txt").read_text(
        encoding="utf-8"
    )
    packages = {
        line.split("==", 1)[0].split("[", 1)[0].lower()
        for line in requirements.splitlines()
        if line and not line.startswith(("#", "-"))
    }
    forbidden_packages = sorted(packages.intersection(FORBIDDEN_AGENT_PACKAGES))

    assert not violations, f"Forbidden agent imports: {violations}"
    assert not forbidden_packages, f"Forbidden runtime packages: {forbidden_packages}"
    assert APPROVED_AGENT_PROVIDER_PACKAGES <= packages


def test_master_data_registry_is_coherent() -> None:
    path = REPO_ROOT / "shared" / "contracts" / "master-data.json"
    registry = json.loads(path.read_text(encoding="utf-8"))
    collections = registry["collections"]

    assert set(collections) == REQUIRED_MASTER_COLLECTIONS
    assert (REPO_ROOT / "shared" / "schemas" / "master-data-registry.schema.json").is_file()

    for name, records in collections.items():
        codes = [record["code"] for record in records]
        orders = [record["sortOrder"] for record in records]
        assert records, f"{name} has no records"
        assert len(codes) == len(set(codes)), f"{name} contains duplicate codes"
        assert len(orders) == len(set(orders)), f"{name} contains duplicate sort orders"
        assert all(record["displayLabel"].strip() for record in records)
        assert all(isinstance(record["isActive"], bool) for record in records)

    assert "BUDIENT_MISMATCH" not in path.read_text(encoding="utf-8")

    permissions = collections["permissions"]
    assert {item["code"] for item in permissions} == set(REQUIRED_PERMISSION_ROLES)
    for permission in permissions:
        roles = permission.get("metadata", {}).get("roles", [])
        assert set(roles) == REQUIRED_PERMISSION_ROLES[permission["code"]]
        assert len(roles) == len(set(roles))
        assert set(roles) <= {item["code"] for item in collections["roles"]}


def test_generated_master_data_projections_match_the_canonical_registry() -> None:
    source = (REPO_ROOT / "shared" / "contracts" / "master-data.json").read_bytes()
    source_hash = hashlib.sha256(source).hexdigest()
    projections = [
        "api/src/Advertified.Commercial.Domain/Generated/MasterDataCodes.g.cs",
        "web/src/generated/master-data-codes.ts",
        "agent-runtime/master_data_codes.py",
    ]
    for projection in projections:
        content = (REPO_ROOT / projection).read_text(encoding="utf-8")
        assert f"source-sha256: {source_hash}" in content, projection


def test_governed_codes_are_not_inline_application_literals() -> None:
    registry = json.loads(
        (REPO_ROOT / "shared" / "contracts" / "master-data.json").read_text(encoding="utf-8")
    )
    governed_codes = {
        record["code"]
        for records in registry["collections"].values()
        for record in records
    }
    literal_pattern = re.compile(r"""(?P<quote>["'])(?P<value>[^"'\\]+)(?P=quote)""")
    violations = []

    for path in source_files():
        path_text = relative(path).lower()
        if (
            "test" in path_text
            or "e2e" in path.parts
            or "/migrations/" in path_text
        ):
            continue
        literals = {
            match.group("value")
            for match in literal_pattern.finditer(path.read_text(encoding="utf-8"))
        }
        path_name = relative(path)
        allowed_overlap = GOVERNED_LITERAL_OVERLAP_ALLOWLIST.get(path_name, set())
        found = sorted(literals.intersection(governed_codes) - allowed_overlap)
        if found:
            violations.append(f"{path_name}: {found}")

    assert not violations, f"Governed master-data codes used inline: {violations}"


def test_tracked_and_pending_files_contain_no_known_secret_shapes() -> None:
    listed = subprocess.run(
        ["git", "ls-files", "--cached", "--others", "--exclude-standard"],
        cwd=REPO_ROOT,
        check=True,
        capture_output=True,
        text=True,
    ).stdout.splitlines()
    patterns = {
        "aws-access-key": re.compile(r"AKIA[0-9A-Z]{16}"),
        "github-token": re.compile(r"gh[pousr]_[A-Za-z0-9]{36,}"),
        "openai-key": re.compile(r"sk-(?:proj-)?[A-Za-z0-9_-]{20,}"),
        "private-key": re.compile(r"-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----"),
        "slack-token": re.compile(r"xox[baprs]-[A-Za-z0-9-]{20,}"),
    }
    violations = []

    for item in listed:
        path = REPO_ROOT / item
        if not path.is_file() or GENERATED_PARTS.intersection(path.parts):
            continue
        if relative(path) == "tests/architecture/test_boundaries.py":
            continue
        try:
            content = path.read_text(encoding="utf-8")
        except UnicodeDecodeError:
            continue
        for label, pattern in patterns.items():
            if pattern.search(content):
                violations.append(f"{relative(path)}: {label}")

    assert not violations, f"Potential committed secrets: {violations}"


def test_live_provider_and_cost_defaults_are_closed() -> None:
    environment = (REPO_ROOT / "infrastructure" / "env.example").read_text(encoding="utf-8")
    runtime = (REPO_ROOT / "agent-runtime" / "main.py").read_text(encoding="utf-8")

    assert "ADVERTIFIED_AGENT_RUNTIME_MODE=disabled" in environment
    assert "ADVERTIFIED_BEDROCK_MODEL_ALLOWLIST=" in environment
    assert "ADVERTIFIED_BEDROCK_PRICING_JSON={}" in environment
    assert "AI_COST_CAP_MINOR=0" in environment
    assert 'DISABLED_MODE = "disabled"' in runtime
    assert "os.environ.get(RUNTIME_MODE_KEY, DISABLED_MODE)" in runtime


def test_ci_has_no_placeholder_success_or_floating_main_actions() -> None:
    workflow = (REPO_ROOT / ".github" / "workflows" / "ci.yml").read_text(encoding="utf-8")

    assert "@main" not in workflow
    assert "Add integration test commands here" not in workflow
    assert "Magic string check passed" not in workflow
    assert "Container scanning..." not in workflow

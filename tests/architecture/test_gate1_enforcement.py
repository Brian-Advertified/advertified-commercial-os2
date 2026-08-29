"""Gate 1 dependency policies and controlled failing fixtures."""

import json
import tomllib
from pathlib import Path

from tests.architecture.architecture_rules import (
    accepted_adr_violations,
    authored_line_violations,
    project_contract_violations,
    python_function_length_violations,
    python_import_violations,
)

REPO_ROOT = Path(__file__).resolve().parents[2]
PROJECTS = {
    "Advertified.Commercial.Domain": (
        "api/src/Advertified.Commercial.Domain/Advertified.Commercial.Domain.csproj"
    ),
    "Advertified.Commercial.Application": (
        "api/src/Advertified.Commercial.Application/Advertified.Commercial.Application.csproj"
    ),
    "Advertified.Commercial.Infrastructure": (
        "api/src/Advertified.Commercial.Infrastructure/"
        "Advertified.Commercial.Infrastructure.csproj"
    ),
    "Advertified.Commercial.Api": "api/Advertified.Commercial.Api.csproj",
}


def test_csharp_project_dependencies_match_the_locked_direction() -> None:
    violations = []

    for project_name, relative_path in PROJECTS.items():
        project_text = (REPO_ROOT / relative_path).read_text(encoding="utf-8")
        violations.extend(project_contract_violations(project_name, project_text))

    assert not violations, "\n".join(violations)


def test_language_analyzers_enforce_complexity_and_function_size() -> None:
    build_properties = (REPO_ROOT / "Directory.Build.props").read_text(encoding="utf-8")
    editor_config = (REPO_ROOT / ".editorconfig").read_text(encoding="utf-8")
    python_config = tomllib.loads(
        (REPO_ROOT / "agent-runtime" / "pyproject.toml").read_text(encoding="utf-8")
    )
    web_config = json.loads(
        (REPO_ROOT / "web" / ".oxlintrc.json").read_text(encoding="utf-8")
    )

    assert "<AnalysisLevel>10.0-recommended</AnalysisLevel>" in build_properties
    assert "<LangVersion>14.0</LangVersion>" in build_properties
    assert "dotnet_diagnostic.CA1502.severity = error" in editor_config
    assert "dotnet_code_quality.CA1502.threshold = 10" in editor_config
    assert python_config["tool"]["ruff"]["target-version"] == "py312"
    assert "C90" in python_config["tool"]["ruff"]["lint"]["select"]
    assert python_config["tool"]["ruff"]["lint"]["mccabe"]["max-complexity"] == 10
    assert web_config["rules"]["eslint/complexity"] == ["error", {"max": 10}]
    assert web_config["rules"]["eslint/max-lines-per-function"][1]["max"] == 60


def test_gate_evidence_manifests_match_the_closed_schema_contract() -> None:
    evidence_root = REPO_ROOT / "docs" / "evidence"
    schema = json.loads(
        (evidence_root / "manifest.schema.json").read_text(encoding="utf-8")
    )
    manifests = [
        json.loads((evidence_root / "manifest.template.json").read_text(encoding="utf-8")),
        json.loads((evidence_root / "gate-1" / "manifest.json").read_text(encoding="utf-8")),
        json.loads((evidence_root / "gate-2" / "manifest.json").read_text(encoding="utf-8")),
        json.loads((evidence_root / "gate-3" / "manifest.json").read_text(encoding="utf-8")),
    ]
    allowed_fields = set(schema["properties"])
    required_fields = set(schema["required"])
    check_schema = schema["properties"]["checks"]["items"]
    allowed_check_fields = set(check_schema["properties"])
    required_check_fields = set(check_schema["required"])
    allowed_outcomes = set(check_schema["properties"]["outcome"]["enum"])
    review_schema = schema["properties"]["ownerReview"]
    allowed_review_statuses = set(
        review_schema["properties"]["status"]["enum"]
    )

    for manifest in manifests:
        assert required_fields <= set(manifest)
        assert set(manifest) <= allowed_fields
        assert manifest["schemaVersion"] == "1.0.0"
        for check in manifest["checks"]:
            assert required_check_fields <= set(check)
            assert set(check) <= allowed_check_fields
            assert check["outcome"] in allowed_outcomes

    for expected_gate, gate_manifest in enumerate(manifests[1:], start=1):
        assert gate_manifest["gate"] == expected_gate
        assert gate_manifest["liveProviderUsed"] is False
        assert gate_manifest["productionResourcesUsed"] is False
        assert gate_manifest["incrementalAiCostMinor"] == 0
        review = gate_manifest["ownerReview"]
        assert review["status"] in allowed_review_statuses
        if review["status"] == "APPROVED":
            assert review["owner"]
            assert review["decisionDate"]


def test_accepted_adrs_require_actual_people_and_dates() -> None:
    violations = []

    for path in (REPO_ROOT / "docs" / "adr").glob("[0-9][0-9][0-9][0-9]-*.md"):
        if path.name == "0000-adr-template.md":
            continue
        violations.extend(accepted_adr_violations(path))

    assert not violations, "\n".join(violations)


def test_python_runtime_functions_respect_the_hard_size_limit() -> None:
    violations = python_function_length_violations(REPO_ROOT / "agent-runtime")

    assert not violations, f"Python functions over 60 authored lines: {violations}"


def test_dependency_detector_rejects_infrastructure_in_the_domain() -> None:
    violating_project = """
    <Project Sdk="Microsoft.NET.Sdk">
      <ItemGroup>
        <PackageReference Include="Npgsql" Version="8.0.9" />
      </ItemGroup>
    </Project>
    """

    assert project_contract_violations(
        "Advertified.Commercial.Domain",
        violating_project,
    )


def test_adr_detector_rejects_placeholder_acceptance(tmp_path: Path) -> None:
    adr = tmp_path / "0001-invalid.md"
    adr.write_text(
        "# ADR\n\n## Status\n\nAccepted\n\n"
        "Owner: UNASSIGNED\n\nDecision date: pending\n",
        encoding="utf-8",
    )

    assert accepted_adr_violations(adr)


def test_python_boundary_detector_rejects_database_import(tmp_path: Path) -> None:
    runtime = tmp_path / "runtime"
    runtime.mkdir()
    (runtime / "unsafe.py").write_text("import psycopg\n", encoding="utf-8")

    assert python_import_violations(runtime)


def test_function_size_detector_rejects_controlled_violation(tmp_path: Path) -> None:
    runtime = tmp_path / "runtime"
    runtime.mkdir()
    statements = "\n".join(f"    value_{index} = {index}" for index in range(61))
    (runtime / "too_large.py").write_text(
        f"def too_large():\n{statements}\n",
        encoding="utf-8",
    )

    assert python_function_length_violations(runtime)


def test_file_size_detector_rejects_controlled_violation(tmp_path: Path) -> None:
    source = tmp_path / "too_large.py"
    source.write_text("value = 1\n" * 401, encoding="utf-8")

    assert authored_line_violations([source])

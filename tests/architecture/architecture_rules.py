"""Reusable architecture detectors with independently testable failure behavior."""

from __future__ import annotations

import ast
import re
import xml.etree.ElementTree as element_tree
from dataclasses import dataclass
from pathlib import Path

GENERATED_PARTS = {"__pycache__", "bin", "node_modules", "obj"}
FORBIDDEN_RUNTIME_IMPORTS = {"asyncpg", "boto3", "psycopg", "psycopg2", "sqlalchemy"}
PROJECT_POLICIES = {
    "Advertified.Commercial.Domain": (
        frozenset(),
        frozenset(),
    ),
    "Advertified.Commercial.Application": (
        frozenset({"Advertified.Commercial.Domain"}),
        frozenset(),
    ),
    "Advertified.Commercial.Infrastructure": (
        frozenset(
            {
                "Advertified.Commercial.Application",
                "Advertified.Commercial.Domain",
            }
        ),
        frozenset(
            {
                "Microsoft.EntityFrameworkCore",
                "Microsoft.EntityFrameworkCore.Relational",
                "Npgsql.EntityFrameworkCore.PostgreSQL",
            }
        ),
    ),
    "Advertified.Commercial.Api": (
        frozenset(
            {
                "Advertified.Commercial.Application",
                "Advertified.Commercial.Infrastructure",
            }
        ),
        frozenset({"Swashbuckle.AspNetCore"}),
    ),
}


@dataclass(frozen=True)
class ProjectContract:
    references: frozenset[str]
    packages: frozenset[str]


def read_project_contract(project_text: str) -> ProjectContract:
    root = element_tree.fromstring(project_text)
    references = frozenset(
        Path(item.attrib["Include"].replace("\\", "/")).stem
        for item in root.findall(".//ProjectReference")
    )
    packages = frozenset(
        item.attrib["Include"]
        for item in root.findall(".//PackageReference")
    )
    return ProjectContract(references, packages)


def project_contract_violations(project_name: str, project_text: str) -> list[str]:
    allowed_references, allowed_packages = PROJECT_POLICIES[project_name]
    contract = read_project_contract(project_text)
    violations = []

    if contract.references != allowed_references:
        violations.append(
            f"{project_name} references {sorted(contract.references)}; "
            f"expected {sorted(allowed_references)}"
        )
    if contract.packages != allowed_packages:
        violations.append(
            f"{project_name} packages {sorted(contract.packages)}; "
            f"expected {sorted(allowed_packages)}"
        )

    return violations


def python_import_violations(root: Path) -> list[str]:
    violations = []

    for path in root.rglob("*.py"):
        if GENERATED_PARTS.intersection(path.parts):
            continue
        tree = ast.parse(path.read_text(encoding="utf-8"), filename=str(path))
        imports = {
            imported_name
            for node in ast.walk(tree)
            for imported_name in _node_imports(node)
        }
        forbidden = sorted(imports.intersection(FORBIDDEN_RUNTIME_IMPORTS))
        if forbidden:
            violations.append(f"{path.as_posix()}: {forbidden}")

    return violations


def python_function_length_violations(
    root: Path,
    maximum_lines: int = 60,
) -> list[str]:
    violations = []

    for path in root.rglob("*.py"):
        if GENERATED_PARTS.intersection(path.parts):
            continue
        source_lines = path.read_text(encoding="utf-8").splitlines()
        tree = ast.parse("\n".join(source_lines), filename=str(path))

        for node in ast.walk(tree):
            if not isinstance(node, (ast.AsyncFunctionDef, ast.FunctionDef)):
                continue
            relevant = source_lines[node.lineno - 1 : node.end_lineno]
            authored_count = sum(
                bool(line.strip()) and not line.lstrip().startswith("#")
                for line in relevant
            )
            if authored_count > maximum_lines:
                violations.append(
                    f"{path.as_posix()}::{node.name} has {authored_count} authored lines"
                )

    return violations


def authored_line_violations(
    paths: list[Path],
    maximum_lines: int = 400,
) -> list[str]:
    return [
        f"{path.as_posix()} has {len(path.read_text(encoding='utf-8').splitlines())} lines"
        for path in paths
        if len(path.read_text(encoding="utf-8").splitlines()) > maximum_lines
    ]


def accepted_adr_violations(path: Path) -> list[str]:
    text = path.read_text(encoding="utf-8")
    status_match = re.search(
        r"^## Status\s*$\s*^(.+)$",
        text,
        flags=re.MULTILINE,
    )
    if status_match is None:
        return [f"{path.as_posix()} has no status"]

    status = status_match.group(1).strip()
    if not status.startswith("Accepted"):
        return []

    violations = []
    if "UNASSIGNED" in text or "TBD" in text:
        violations.append(f"{path.as_posix()} is Accepted with an unassigned decision")
    if not re.search(r"Decision date:\s*20[0-9]{2}-[0-9]{2}-[0-9]{2}", text):
        violations.append(f"{path.as_posix()} is Accepted without an ISO decision date")
    return violations


def _node_imports(node: ast.AST) -> set[str]:
    if isinstance(node, ast.Import):
        return {alias.name.split(".")[0] for alias in node.names}
    if isinstance(node, ast.ImportFrom):
        return {(node.module or "").split(".")[0]}
    return set()

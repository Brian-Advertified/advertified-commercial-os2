"""Bind inventory evidence to the exact local implementation under evaluation."""

from __future__ import annotations

import hashlib
import subprocess
from pathlib import Path
from typing import Any


REFERENCE_GLOBS = (
    "tools/inventory_reference_*.py",
    "tools/reconcile_and_report_inventory.py",
    "tools/render_inventory_visual_evidence.py",
    "tools/inspect_inventory_workbooks.py",
)
APPLICATION_GLOBS = (
    "api/src/Advertified.Commercial.Infrastructure/Inventory/InventoryCandidateAdmissionPolicy.cs",
    "api/src/Advertified.Commercial.Infrastructure/Inventory/InventoryCandidateAliases.cs",
    "api/src/Advertified.Commercial.Infrastructure/Inventory/InventoryCandidateStructuredNormalizer.cs",
    "api/src/Advertified.Commercial.Infrastructure/Inventory/InventoryCommands.Execute.cs",
    "api/src/Advertified.Commercial.Infrastructure/Inventory/InventoryExtractionCompletionPolicy.cs",
    "api/src/Advertified.Commercial.Infrastructure/Inventory/InventoryExtractionOptions.cs",
    "api/src/Advertified.Commercial.Infrastructure/Inventory/InventoryExtractionAttemptProcessor.cs",
    "api/src/Advertified.Commercial.Infrastructure/Inventory/InventoryMoneyParser.cs",
    "api/src/Advertified.Commercial.Infrastructure/Inventory/InventoryProcessingOptions.cs",
    "api/src/Advertified.Commercial.Infrastructure/Inventory/InventoryProjectionVerificationService.cs",
    "api/src/Advertified.Commercial.Infrastructure/Inventory/InventoryProjectionVersion.cs",
    "api/src/Advertified.Commercial.Infrastructure/Inventory/NativeOffice*.cs",
    "api/src/Advertified.Commercial.Infrastructure/Inventory/NativePresentationProjection.cs",
    "api/src/Advertified.Commercial.Infrastructure/Inventory/NativeSpreadsheetProjection.cs",
    "api/Background/InventoryExtractionDispatcher.cs",
    "api/InventoryExtractionRegistration.cs",
    "api/Endpoints/InventoryProjectionVerificationEndpoints.cs",
    "api/tests/Advertified.Commercial.Api.Tests/InventoryCandidateAdmissionPolicyTests.cs",
    "api/tests/Advertified.Commercial.Api.Tests/InventoryProjectionVerificationBoundaryTests.cs",
    "infrastructure/docker-compose.app.yml",
    "artifacts/inventory-corpus/docker-compose.override.yml",
)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def matched_files(repository: Path, patterns: tuple[str, ...]) -> list[Path]:
    paths: set[Path] = set()
    for pattern in patterns:
        paths.update(path for path in repository.glob(pattern) if path.is_file())
    paths.add(Path(__file__).resolve())
    return sorted(paths)


def git_head(repository: Path) -> str:
    return subprocess.run(
        ["git", "rev-parse", "HEAD"], cwd=repository, check=True,
        capture_output=True, text=True,
    ).stdout.strip()


def binding(repository: Path, patterns: tuple[str, ...]) -> dict[str, Any]:
    return {
        "git_head": git_head(repository),
        "working_tree_note": "File hashes bind relevant tracked and untracked local revisions; the tree is intentionally unstaged.",
        "files": [
            {
                "path": path.relative_to(repository).as_posix(),
                "sha256": sha256(path),
            }
            for path in matched_files(repository, patterns)
        ],
        "repeatable_commands": [
            "python tools/render_inventory_visual_evidence.py --manifest tools/inventory_source_manifest.json --output artifacts/inventory-corpus/verification/visual --dpi 150",
            "python tools/inspect_inventory_workbooks.py --manifest tools/inventory_source_manifest.json --output artifacts/inventory-corpus/verification/inventory_workbook_structure.json --media-dir artifacts/inventory-corpus/verification/workbook-media",
            "python tools/reconcile_and_report_inventory.py --manifest tools/inventory_source_manifest.json --render-manifest artifacts/inventory-corpus/verification/visual/render_manifest.json --workbooks artifacts/inventory-corpus/verification/inventory_workbook_structure.json --source-maps artifacts/inventory-corpus/semantic-v1 --output artifacts/inventory-corpus/verification/inventory_physical_reference_ledger.json",
        ],
    }


def application_binding(repository: Path) -> dict[str, Any]:
    """Bind canonical outputs only to code/config that can affect projection."""
    return binding(repository, APPLICATION_GLOBS)


def verification_binding(repository: Path) -> dict[str, Any]:
    """Bind the final ledger to both application and independent-review code."""
    return binding(repository, REFERENCE_GLOBS + APPLICATION_GLOBS)

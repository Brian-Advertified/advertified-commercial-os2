"""Exercise the actual PowerShell launcher with local fake Docker/Python commands."""

import hashlib
import json
import os
import shutil
import subprocess
import tempfile
from pathlib import Path
from uuid import uuid4

import pytest

ROOT = Path(__file__).resolve().parents[2]


@pytest.mark.parametrize("reservation_accepted", [True, False])
def test_launcher_dispatches_only_after_canonical_reservation(reservation_accepted):
    shell = shutil.which("powershell") or shutil.which("pwsh")
    if not shell:
        pytest.skip("The Windows local-certification launcher requires PowerShell.")
    with tempfile.TemporaryDirectory(prefix="launcher-fixture-", dir=ROOT / "artifacts") as directory:
        fixture = Path(directory).resolve()
        assert fixture.is_relative_to((ROOT / "artifacts").resolve())
        source = fixture / "synthetic-usage.json"
        source.write_text('{"syntheticFixture": true, "maximumCostUsdMicros": 123000}')
        receipt = fixture / "prior.json"
        receipt.write_text(json.dumps({
            "schemaVersion": "advertified.prior-live-cost-reconciliation.v1",
            "reconciliationId": str(uuid4()), "reconciliationStepId": str(uuid4()),
            "reconciledThroughUtc": "2026-09-12T00:00:00Z",
            "unreservedMaximumCostUsdMicros": 123000,
            "sourceReceipts": [{
                "path": source.relative_to(ROOT).as_posix(),
                "sha256": hashlib.sha256(source.read_bytes()).hexdigest(),
            }],
        }))
        evidence_name = "launcher-fixture-" + uuid4().hex
        output = ROOT / "artifacts/backend-production-completion" / evidence_name
        wrapper = fixture / "fake-launch.ps1"
        wrapper.write_text(_wrapper(
            receipt.relative_to(ROOT).as_posix(), evidence_name, reservation_accepted,
        ))
        try:
            result = subprocess.run(
                [shell, "-NoProfile", "-File", str(wrapper)],
                cwd=ROOT, text=True, capture_output=True, timeout=30,
                # Let the child edition establish its own modules instead of inheriting PS7 paths.
                env={key: value for key, value in os.environ.items() if key.casefold() != "psmodulepath"},
            )
            if not reservation_accepted:
                assert result.returncode != 0
                assert "Canonical reservation failed" in result.stderr
                assert not output.exists()
                return
            assert result.returncode == 0, result.stdout + result.stderr
            assert "FAKE_PROVIDER_DISPATCH" in result.stdout
            permit = json.loads((output / "live-permit.json").read_text(encoding="utf-8-sig"))
            assert permit["maximumCostUsdMicros"] == 40000
            assert permit["maximumCalls"] == 2
            assert permit["costCapMinor"] == 2
        finally:
            # Only this invocation's generated fixture directory may be removed.
            if output.exists():
                assert output.resolve().parent == (ROOT / "artifacts/backend-production-completion").resolve()
                assert output.name == evidence_name and output.name.startswith("launcher-fixture-")
                shutil.rmtree(output)


def _wrapper(receipt, evidence_name, accepted):
    exit_code = 0 if accepted else 1
    return f"""
$ErrorActionPreference = 'Stop'
$global:LASTEXITCODE = 0
function docker {{
    if ($args[0] -eq 'inspect') {{
        $global:LASTEXITCODE = 0
        return '{{"com.docker.compose.project":"advertified-os2-dev"}}'
    }}
    $sql = $input | Out-String
    if ($sql -notmatch 'governance.reserve_ai_monthly_budget' -or $sql -notmatch '123000') {{
        throw 'Canonical reservation SQL was not dispatched.'
    }}
    $global:LASTEXITCODE = {exit_code}
}}
function python {{
    $permit = Get-Content -LiteralPath $env:ADVERTIFIED_BUSINESS_SCENARIO_BUDGET_PERMIT -Raw |
        ConvertFrom-Json
    if (-not $permit.canonicalReservationAccepted) {{ throw 'Unreserved dispatch.' }}
    Write-Output 'FAKE_PROVIDER_DISPATCH'
    $global:LASTEXITCODE = 0
}}
& ./tools/run-business-scenarios-live.ps1 -PriorCostReceipt '{receipt}' -EvidenceName '{evidence_name}' -MaximumCalls 2 -Tests business_scenarios/test_mukuru_location_research_live.py
"""

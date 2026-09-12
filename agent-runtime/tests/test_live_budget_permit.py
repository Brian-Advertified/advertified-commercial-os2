"""One shared canonical reservation bounds all certification processes."""

import json
from concurrent.futures import ThreadPoolExecutor
from datetime import UTC, datetime, timedelta
from types import SimpleNamespace
from uuid import UUID

import pytest

from bedrock_failure import BedrockProviderError
from business_scenarios import live_budget_permit as budget

TENANT = UUID("11111111-1111-1111-1111-111111111111")
MODEL = "amazon.nova-lite-v1:0"


def invocation():
    return SimpleNamespace(
        tenant_id=TENANT, run_id=UUID(int=10), step_id=UUID(int=11),
        provider_policy=SimpleNamespace(
            model=MODEL, provider="bedrock", allow_live=True, cost_cap_minor=2, max_attempts=1,
        ),
    )


def permit_file(tmp_path, monkeypatch, **changes):
    permit = {
        "schemaVersion": budget.PERMIT_SCHEMA, "canonicalReservationAccepted": True,
        "reservationRunId": str(UUID(int=1)), "reservationStepId": str(UUID(int=2)),
        "tenantId": str(TENANT), "model": MODEL, "maximumCalls": 2, "costCapMinor": 2,
        "maximumCostUsdMicros": 40_000,
        "expiresAtUtc": (datetime.now(UTC) + timedelta(minutes=5)).isoformat(),
    }
    permit.update(changes)
    path = tmp_path / "permit.json"
    path.write_text(json.dumps(permit), encoding="utf-8")
    monkeypatch.setattr(budget, "CLAIMS_ROOT", tmp_path / "claims")
    monkeypatch.setenv(budget.PERMIT_ENV, str(path))
    return path


def test_missing_permit_blocks_before_a_provider_can_be_called(monkeypatch):
    monkeypatch.delenv(budget.PERMIT_ENV, raising=False)
    with pytest.raises(BedrockProviderError, match="requires a canonical"):
        budget.claim_live_call(invocation(), MODEL)


@pytest.mark.parametrize("changes", [
    {"canonicalReservationAccepted": False},
    {"model": "unreserved-model"},
    {"tenantId": str(UUID(int=3))},
    {"maximumCostUsdMicros": 39_999},
    {"maximumCalls": True},
    {"expiresAtUtc": "2020-01-01T00:00:00+00:00"},
])
def test_invalid_permit_never_consumes_a_dispatch_slot(tmp_path, monkeypatch, changes):
    permit_file(tmp_path, monkeypatch, **changes)
    with pytest.raises(BedrockProviderError, match="invalid"):
        budget.claim_live_call(invocation(), MODEL)
    assert not (tmp_path / "claims").exists()


@pytest.mark.parametrize("field,value", [("cost_cap_minor", 3), ("max_attempts", 2)])
def test_invocation_cannot_expand_its_reserved_cost_or_retries(tmp_path, monkeypatch, field, value):
    permit_file(tmp_path, monkeypatch)
    request = invocation()
    setattr(request.provider_policy, field, value)
    with pytest.raises(BedrockProviderError, match="invalid"):
        budget.claim_live_call(request, MODEL)


def test_concurrent_dispatch_and_copied_permit_share_the_same_two_slots(tmp_path, monkeypatch):
    path = permit_file(tmp_path, monkeypatch)

    def claim():
        try:
            return budget.claim_live_call(invocation(), MODEL)
        except BedrockProviderError:
            return None

    with ThreadPoolExecutor(max_workers=8) as pool:
        results = list(pool.map(lambda _: claim(), range(8)))
    accepted = [value for value in results if value is not None]
    assert len(accepted) == len(set(accepted)) == 2
    # An uncertain provider failure leaves the slot consumed across processes/restarts.
    copy = tmp_path / "copy.json"
    copy.write_bytes(path.read_bytes())
    monkeypatch.setenv(budget.PERMIT_ENV, str(copy))
    with pytest.raises(BedrockProviderError, match="exhausted"):
        budget.claim_live_call(invocation(), MODEL)


def test_business_scenario_fixture_blocks_actual_dispatch_without_permit(monkeypatch):
    import bedrock_provider
    from business_scenarios.conftest import enforce_shared_live_budget

    monkeypatch.delenv(budget.PERMIT_ENV, raising=False)
    called = []
    client = SimpleNamespace(converse=lambda **kwargs: called.append(kwargs))
    enforce_shared_live_budget.__wrapped__(monkeypatch)
    with pytest.raises(BedrockProviderError, match="requires a canonical"):
        bedrock_provider._converse(
            client, None, invocation(), MODEL, {}, [], [], 128,
        )
    assert called == []

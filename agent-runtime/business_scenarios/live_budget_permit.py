"""Consume one-shot local certification permits; C# database policy owns the budget."""

from __future__ import annotations

import json
import os
from datetime import UTC, datetime
from pathlib import Path
from uuid import UUID

from bedrock_failure import BedrockProviderError

PERMIT_ENV = "ADVERTIFIED_BUSINESS_SCENARIO_BUDGET_PERMIT"
PERMIT_SCHEMA = "advertified.live-certification-permit.v1"
CLAIMS_ROOT = Path(__file__).resolve().parents[2] / "artifacts/live-certification-claims"


def claim_live_call(invocation, model: str) -> Path:
    configured = os.environ.get(PERMIT_ENV)
    if not configured:
        raise BedrockProviderError(
            "Live certification requires a canonical shared-budget reservation. "
            "Run tools/run-business-scenarios-live.ps1 with reconciled prior spending."
        )
    try:
        path = Path(configured).resolve(strict=True)
        permit = json.loads(path.read_text(encoding="utf-8-sig"))
        _validate_permit(permit, invocation, model)
    except (KeyError, TypeError, ValueError, OSError) as error:
        raise BedrockProviderError("Live certification budget permit is invalid.") from error
    return _claim_slot(path, permit, invocation)


def _validate_permit(permit: dict, invocation, model: str) -> None:
    if permit["schemaVersion"] != PERMIT_SCHEMA or permit["canonicalReservationAccepted"] is not True:
        raise ValueError("No accepted canonical reservation.")
    UUID(permit["reservationRunId"])
    UUID(permit["reservationStepId"])
    maximum_calls = permit["maximumCalls"]
    cap_minor = permit["costCapMinor"]
    maximum = permit["maximumCostUsdMicros"]
    if any(type(value) is not int or value <= 0 for value in (maximum_calls, cap_minor, maximum)):
        raise ValueError("Invalid reservation bounds.")
    if maximum != maximum_calls * cap_minor * 10_000 or maximum > 5_000_000:
        raise ValueError("Reservation allocation does not reconcile.")
    expiry = datetime.fromisoformat(permit["expiresAtUtc"])
    if expiry.tzinfo is None or expiry <= datetime.now(UTC):
        raise ValueError("Permit expired.")
    policy = invocation.provider_policy
    if (model != permit["model"] or policy.model != model
            or policy.provider != "bedrock" or not policy.allow_live
            or policy.cost_cap_minor > cap_minor or policy.max_attempts != 1):
        raise ValueError("Invocation differs from its reservation.")
    if str(invocation.tenant_id) != permit["tenantId"]:
        raise ValueError("Invocation tenant differs from its reservation.")


def _claim_slot(path: Path, permit: dict, invocation) -> Path:
    claims = CLAIMS_ROOT / f"{UUID(permit['reservationRunId'])}-{UUID(permit['reservationStepId'])}"
    claims.mkdir(parents=True, exist_ok=True)
    for slot in range(permit["maximumCalls"]):
        claim = claims / f"{slot:04d}.json"
        try:
            # Atomic exclusive creation fences concurrent processes and survives crashes.
            with claim.open("x", encoding="utf-8") as stream:
                json.dump({
                    "reservationRunId": permit["reservationRunId"],
                    "reservationStepId": permit["reservationStepId"],
                    "invocationRunId": str(invocation.run_id),
                    "invocationStepId": str(invocation.step_id),
                    "claimedAtUtc": datetime.now(UTC).isoformat(),
                    "maximumCostUsdMicros": permit["costCapMinor"] * 10_000,
                }, stream)
                stream.flush()
                os.fsync(stream.fileno())
            return claim
        except FileExistsError:
            continue
    raise BedrockProviderError("Live certification shared-budget reservation is exhausted.")


def retain_provider_receipt(claim: Path, response: dict) -> None:
    receipt = {
        "usage": response.get("usage"),
        "requestId": response.get("ResponseMetadata", {}).get("RequestId"),
        "stopReason": response.get("stopReason"),
        "receivedAtUtc": datetime.now(UTC).isoformat(),
        "reservationReleased": False,
    }
    # Invalid/rejected output and uncertain failures never release the reservation.
    claim.with_suffix(".response.json").write_text(
        json.dumps(receipt, indent=2) + "\n", encoding="utf-8"
    )

"""Prepare one-attempt replacement packets for failed Bedrock responses."""

from __future__ import annotations

import hashlib
import json
import re
from datetime import UTC, datetime
from pathlib import Path
from typing import Any

ROOT = Path(__file__).resolve().parents[1]
CORPUS = ROOT / "artifacts" / "inventory-corpus"
ROOT_DIR = CORPUS / "bedrock-production-certification"
PLAN_PATH = ROOT_DIR / "plan.json"
LEDGER_PATH = ROOT_DIR / "usage-ledger.json"
REQUEST_ROOT = ROOT_DIR / "requests"
PREFLIGHT_MARKER = ROOT_DIR / "PREFLIGHT_APPROVED.json"
CANARY_MARKER = ROOT_DIR / "DMS_CANARY_PASSED.json"
COMPLETE_MARKER = ROOT_DIR / "ALL_BEDROCK_RESPONSES_CERTIFIED.json"
DMS_HASH = "2e2bb6e6a70bbd8c54b6d03deb3643d72126e1040752d7ef0e05c9ec456a25b5"
PROGRAMME_LIMIT_USD_MICROS = 5_000_000
HISTORICAL_RESERVE_USD_MICROS = 188_122
MAX_REPAIR_ROUNDS = 2


def main() -> int:
    plan = read_json(PLAN_PATH)
    ledger = read_json(LEDGER_PATH) if LEDGER_PATH.exists() else {"entries": []}
    completed = {
        str(item.get("packetId"))
        for item in ledger.get("entries") or []
        if item.get("status") == "COMPLETED" and item.get("validationPassed")
    }
    failed_by_id = {
        str(item.get("packetId")): item
        for item in ledger.get("entries") or []
        if not (item.get("status") == "COMPLETED" and item.get("validationPassed"))
    }
    replacements = []
    new_packets = []
    dms_replaced = False
    for summary in plan.get("packets") or []:
        packet_id = str(summary["packetId"])
        if packet_id in completed:
            new_packets.append(summary)
            continue
        failure = failed_by_id.get(packet_id)
        if failure is None:
            new_packets.append(summary)
            continue
        packet = read_json(ROOT / summary["requestPath"])
        repair_round = repair_round_for(packet) + 1
        if repair_round > MAX_REPAIR_ROUNDS:
            raise RuntimeError(
                f"Packet {packet_id} exceeded the maximum approved repair rounds."
            )
        failures = failure.get("validationFailures") or [
            str(failure.get("error") or "FAILED_TERMINAL")
        ]
        repaired = repair_packet(packet, failures, repair_round)
        path = REQUEST_ROOT / f"{repaired['sourceHash']}.repair-{repair_round}.{repaired['packetId'][:12]}.json"
        write_json(path, repaired)
        replacement = packet_summary(repaired, path)
        new_packets.append(replacement)
        replacements.append({
            "oldPacketId": packet_id,
            "newPacketId": repaired["packetId"],
            "sourceHash": repaired["sourceHash"],
            "fileName": repaired["input"]["fileName"],
            "repairRound": repair_round,
            "failuresAddressed": failures,
        })
        if repaired["sourceHash"] == DMS_HASH:
            dms_replaced = True
    if not replacements:
        print(json.dumps({
            "prepared": False,
            "reason": "NO_FAILED_PACKET_WITH_REPAIRABLE_LEDGER_ENTRY",
        }, indent=2))
        return 2

    actual = sum(int(item.get("actualCostUsdMicros") or 0) for item in ledger.get("entries") or [])
    remaining_maximum = sum(
        int(item.get("maximumCostUsdMicros") or 0)
        for item in new_packets
        if str(item["packetId"]) not in completed
    )
    programme_maximum = HISTORICAL_RESERVE_USD_MICROS + actual + remaining_maximum
    if programme_maximum > PROGRAMME_LIMIT_USD_MICROS:
        raise RuntimeError("Repair plan would exceed the total US$5 programme limit.")
    plan["packets"] = new_packets
    plan["packetCount"] = len(new_packets)
    plan["generatedAtUtc"] = datetime.now(UTC).isoformat()
    plan["repairPlan"] = {
        "preparedAtUtc": datetime.now(UTC).isoformat(),
        "replacementCount": len(replacements),
        "replacements": replacements,
    }
    plan["actualSpentUsdMicros"] = actual
    plan["remainingMaximumCostUsdMicros"] = remaining_maximum
    plan["programmeMaximumCostUsdMicros"] = programme_maximum
    plan["remainingProgrammeAllowanceUsdMicros"] = PROGRAMME_LIMIT_USD_MICROS - programme_maximum
    plan["withinProgrammeLimit"] = True
    write_json(PLAN_PATH, plan)
    write_json(PREFLIGHT_MARKER, {
        "approved": True,
        "physicalCertificationRequired": True,
        "planSha256": file_hash(PLAN_PATH),
        "programmeLimitUsdMicros": PROGRAMME_LIMIT_USD_MICROS,
        "actualSpentUsdMicros": actual,
        "remainingMaximumCostUsdMicros": remaining_maximum,
        "programmeMaximumCostUsdMicros": programme_maximum,
        "remainingProgrammeAllowanceUsdMicros": PROGRAMME_LIMIT_USD_MICROS - programme_maximum,
        "repairReplacementCount": len(replacements),
    })
    COMPLETE_MARKER.unlink(missing_ok=True)
    if dms_replaced:
        CANARY_MARKER.unlink(missing_ok=True)
    invalidate_downstream()
    print(json.dumps({
        "prepared": True,
        "replacementCount": len(replacements),
        "actualSpentUsd": actual / 1_000_000,
        "remainingMaximumCostUsd": remaining_maximum / 1_000_000,
        "programmeMaximumCostUsd": programme_maximum / 1_000_000,
        "remainingProgrammeAllowanceUsd": (
            PROGRAMME_LIMIT_USD_MICROS - programme_maximum
        ) / 1_000_000,
        "replacements": replacements,
    }, indent=2))
    return 0


def repair_packet(
    packet: dict[str, Any],
    failures: list[str],
    repair_round: int,
) -> dict[str, Any]:
    repaired = json.loads(json.dumps(packet))
    failure_text = ", ".join(str(value) for value in failures)
    repaired["promptVersion"] = re.sub(
        r"-repair-\d+$", "", str(packet["promptVersion"])
    ) + f"-repair-{repair_round}"
    repaired["reason"] = (
        str(packet["reason"])
        + f" Repair round {repair_round} addresses deterministic validation failures: "
        + failure_text
    )
    repaired["systemInstruction"] = (
        str(packet["systemInstruction"])
        + "\n\nThe previous response was rejected for: "
        + failure_text
        + ". Correct only these structural/grounding failures. Do not add facts. "
        + "Re-read every physicalRowId and ensure each appears exactly once."
    )
    material = json.dumps({
        "previousPacketId": packet["packetId"],
        "repairRound": repair_round,
        "promptVersion": repaired["promptVersion"],
        "failures": failures,
        "input": repaired["input"],
    }, sort_keys=True, separators=(",", ":"))
    repaired["packetId"] = hashlib.sha256(material.encode()).hexdigest()
    repaired["maximumAttempts"] = 1
    repaired["repairOfPacketId"] = packet["packetId"]
    repaired["repairRound"] = repair_round
    repaired["failuresAddressed"] = failures
    return repaired


def repair_round_for(packet: dict[str, Any]) -> int:
    value = packet.get("repairRound")
    if value is not None:
        return int(value)
    match = re.search(r"-repair-(\d+)$", str(packet.get("promptVersion") or ""))
    return int(match.group(1)) if match else 0


def packet_summary(packet: dict[str, Any], path: Path) -> dict[str, Any]:
    return {
        "packetId": packet["packetId"],
        "sourceHash": packet["sourceHash"],
        "fileName": packet["input"]["fileName"],
        "purpose": packet["purpose"],
        "reason": packet["reason"],
        "rowCount": len(packet["input"]["physicalRows"]),
        "estimatedInputTokens": packet["estimatedInputTokens"],
        "maximumOutputTokens": packet["maximumOutputTokens"],
        "maximumCostUsdMicros": packet["maximumCostUsdMicros"],
        "requestPath": str(path.relative_to(ROOT)),
    }


def invalidate_downstream() -> None:
    for path in (
        CORPUS / "certified-canonical-inventory" / "ALL_CANONICAL_PRODUCTS_ASSEMBLED.json",
        CORPUS / "certified-upload" / "UPLOAD_FILES_VERIFIED.json",
    ):
        path.unlink(missing_ok=True)


def file_hash(path: Path) -> str:
    return hashlib.sha256(path.resolve(strict=True).read_bytes()).hexdigest()


def read_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.resolve(strict=True).read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError(f"Expected object in {path}")
    return value


def write_json(path: Path, value: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8")


if __name__ == "__main__":
    raise SystemExit(main())

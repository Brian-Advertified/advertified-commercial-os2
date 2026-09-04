"""Verify that the 43-file Bedrock run is enabled and bounded below US$5 total."""

from __future__ import annotations

import json
from pathlib import Path

from inventory_corpus_api import InventoryApi

REPO_ROOT = Path(__file__).resolve().parents[1]
PHYSICAL_REPORT = REPO_ROOT / "artifacts" / "inventory-corpus" / "certification" / "physical-corpus-certification.json"
TENANT = "10000000-0000-0000-0000-000000000020"
NEW_RUN_CEILING_MICROS = 4_750_000
PROGRAMME_CEILING_MICROS = 5_000_000
HISTORICAL_RESERVE_MICROS = 250_000


def main() -> int:
    physical = json.loads(PHYSICAL_REPORT.resolve(strict=True).read_text(encoding="utf-8"))
    if physical.get("passed") is not True:
        raise RuntimeError("Physical corpus certification has not passed 43/43.")

    api = InventoryApi("http://127.0.0.1:5197", "http://localhost:3017", TENANT)
    api.start_session()
    preflight = api.request("GET", api.tenant_path("/inventory-semantic-preflight"))
    sources = preflight.get("sources") or []
    existing = int(preflight.get("existingCommittedCostUsdMicros") or 0)
    planned = int(preflight.get("newMaximumCostUsdMicros") or 0)
    worst = int(preflight.get("worstCaseTotalCostUsdMicros") or 0)
    blocked_sources = [
        {
            "fileName": source.get("fileName"),
            "blocker": source.get("blocker"),
            "maximumCostUsdMicros": source.get("maximumCostUsdMicros"),
            "packetCount": source.get("packetCount"),
        }
        for source in sources
        if source.get("blocker") or not source.get("safeToReproject")
    ]
    zero_packet_sources = [
        source.get("fileName")
        for source in sources
        if int(source.get("sourceItemCount") or 0) > 0
        and int(source.get("packetCount") or 0) == 0
    ]
    failures: list[str] = []
    if preflight.get("liveExecutionEnabled") is not True:
        failures.append("LIVE_EXECUTION_NOT_ENABLED")
    if len(sources) != 43:
        failures.append(f"SOURCE_COUNT:{len(sources)}")
    if existing > NEW_RUN_CEILING_MICROS:
        failures.append(f"EXISTING_ACTIVE_SCOPE_COST:{existing}")
    if planned <= 0:
        failures.append("NO_BEDROCK_PACKETS_PLANNED")
    if existing + planned > NEW_RUN_CEILING_MICROS:
        failures.append(f"NEW_RUN_CEILING_EXCEEDED:{existing + planned}")
    if worst > NEW_RUN_CEILING_MICROS:
        failures.append(f"WORST_CASE_NEW_RUN_CEILING_EXCEEDED:{worst}")
    if HISTORICAL_RESERVE_MICROS + worst > PROGRAMME_CEILING_MICROS:
        failures.append(
            f"PROGRAMME_USD5_CEILING_EXCEEDED:{HISTORICAL_RESERVE_MICROS + worst}"
        )
    if blocked_sources:
        failures.append(f"BLOCKED_SOURCES:{len(blocked_sources)}")
    if zero_packet_sources:
        failures.append(f"ZERO_PACKET_SOURCES:{len(zero_packet_sources)}")

    result = {
        "passed": not failures,
        "programmeLimitUsdMicros": PROGRAMME_CEILING_MICROS,
        "historicalReserveUsdMicros": HISTORICAL_RESERVE_MICROS,
        "newRunCeilingUsdMicros": NEW_RUN_CEILING_MICROS,
        "existingActiveScopeCostUsdMicros": existing,
        "plannedMaximumCostUsdMicros": planned,
        "worstCaseTotalCostUsdMicros": worst,
        "sourceCount": len(sources),
        "packetCount": sum(int(source.get("packetCount") or 0) for source in sources),
        "liveExecutionEnabled": preflight.get("liveExecutionEnabled"),
        "blockedSources": blocked_sources,
        "zeroPacketSources": zero_packet_sources,
        "failures": failures,
    }
    output = REPO_ROOT / "artifacts" / "inventory-corpus" / "certification" / "bedrock-preflight.json"
    output.write_text(json.dumps(result, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(json.dumps(result, indent=2))
    return 0 if not failures else 2


if __name__ == "__main__":
    raise SystemExit(main())

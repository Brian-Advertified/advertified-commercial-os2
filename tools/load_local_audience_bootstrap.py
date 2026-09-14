"""Load versioned audience reference evidence into Advertified's shared intelligence kernel."""
from __future__ import annotations

import argparse
import hashlib
import json
import subprocess
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PRODUCTION = ROOT / "data" / "production" / "audience-bootstrap.v1.json"
PROOF = ROOT / "data" / "research" / "audience-bootstrap.proof.v1.json"
CONTAINER = "advertified-os2-dev-postgres-1"
PROJECT = "advertified-os2-dev"
SERVICE = "postgres"
NAMESPACE = uuid.UUID("51ce24bd-848e-4852-a8f2-eaf0a2e4aa49")
DOMAIN = "AUDIENCE"


def canonical(value: object) -> str:
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"))


def sql_text(value: str) -> str:
    return "'" + value.replace("'", "''") + "'"


def sql_json(value: object) -> str:
    return sql_text(canonical(value)) + "::jsonb"


def inspect_container() -> None:
    result = subprocess.run(
        ["docker", "inspect", CONTAINER], cwd=ROOT, text=True, capture_output=True, check=False)
    if result.returncode != 0:
        raise RuntimeError("The exact Advertified development PostgreSQL container is unavailable.")
    record = json.loads(result.stdout)[0]
    labels = record.get("Config", {}).get("Labels", {})
    mounts = record.get("Mounts", [])
    unsafe_mount = any(
        "docker.sock" in str(item.get("Source", "")).lower() or
        "docker.sock" in str(item.get("Destination", "")).lower()
        for item in mounts)
    if not (
        record.get("Name") == f"/{CONTAINER}" and
        record.get("State", {}).get("Running") is True and
        labels.get("com.docker.compose.project") == PROJECT and
        labels.get("com.docker.compose.service") == SERVICE and
        record.get("HostConfig", {}).get("Privileged") is not True and
        record.get("HostConfig", {}).get("NetworkMode") != "host" and
        not unsafe_mount
    ):
        raise RuntimeError("Refusing to load intelligence data outside the exact non-production Advertified database.")


def load_file(path: Path) -> dict:
    payload = json.loads(path.read_text(encoding="utf-8"))
    if payload.get("schemaVersion") != "audience-intelligence-bootstrap.v1":
        raise ValueError(f"Unexpected audience bootstrap schema in {path}")
    if not isinstance(payload.get("sources"), list) or not isinstance(payload.get("observations"), list):
        raise ValueError(f"Audience bootstrap is malformed: {path}")
    return payload


def source_sql(source: dict, observations: list[dict]) -> str:
    source_key = source["key"]
    source_observations = sorted(
        (item for item in observations if item.get("source") == source_key),
        key=lambda item: item["sourceLocator"])
    fingerprint = hashlib.sha256(
        canonical({"source": source, "observations": source_observations}).encode()).hexdigest()
    source_id = str(uuid.uuid5(NAMESPACE, f"source:{source_key}:{fingerprint}"))
    metadata = {"knownCodes": source.get("knownCodes", {})}
    prepared = prepare_observations(source_id, source_observations)
    source_payload = {
        "id": source_id,
        "sourceKey": source_key,
        "contentHash": fingerprint,
        "title": source["title"],
        "publisher": source["publisher"],
        "measurementPeriod": source["measurementPeriod"],
        "sourceLocator": source["sourceLocator"],
        "licenceStatus": source["licenceStatus"],
        "promotionStatus": source["promotionStatus"],
        "usageScope": source["usageScope"],
        "methodology": source["methodology"],
        "universe": source["universe"],
        "capabilities": source.get("capabilities", []),
        "metadata": metadata,
        "qualityNotes": source.get("qualityNotes", []),
    }
    return f"""
        UPDATE governance.intelligence_sources
        SET is_current = false
        WHERE source_key = {sql_text(source_key)} AND is_current
          AND content_hash <> {sql_text(fingerprint)};

        INSERT INTO governance.intelligence_sources (
            id, source_key, content_hash, title, publisher, measurement_period,
            source_locator, licence_status, promotion_status, usage_scope,
            methodology, universe, capabilities_json, metadata_json,
            quality_notes_json, is_current, loaded_at_utc, version)
        SELECT value."id", value."sourceKey", value."contentHash", value."title",
            value."publisher", value."measurementPeriod", value."sourceLocator",
            value."licenceStatus", value."promotionStatus", value."usageScope",
            value."methodology", value."universe", value."capabilities", value."metadata",
            value."qualityNotes", true, CURRENT_TIMESTAMP, 1
        FROM jsonb_to_record({sql_json(source_payload)}) AS value(
            "id" uuid, "sourceKey" text, "contentHash" text, "title" text,
            "publisher" text, "measurementPeriod" text, "sourceLocator" text,
            "licenceStatus" text, "promotionStatus" text, "usageScope" text,
            "methodology" text, "universe" text, "capabilities" jsonb,
            "metadata" jsonb, "qualityNotes" jsonb)
        ON CONFLICT (source_key, content_hash) DO UPDATE SET is_current = true;

        INSERT INTO governance.intelligence_observations (
            id, source_id, source_locator, domain_code, geography_level, geography_code,
            geography_name, dimensions_json, metric_code, metric_value, metric_unit,
            stability_code, sensitivity_code, activation_policy, evidence_notes_json, loaded_at_utc)
        SELECT value."id", {sql_text(source_id)}::uuid, value."sourceLocator", {sql_text(DOMAIN)},
            value."geography"->>'level', value."geography"->>'code', value."geography"->>'name',
            value."dimensions", value."metricCode", value."metricValue", value."metricUnit",
            value."stability", value."sensitivity", value."activationPolicy",
            COALESCE(value."evidenceNotes", '[]'::jsonb), CURRENT_TIMESTAMP
        FROM jsonb_to_recordset({sql_json(prepared)}) AS value(
            "id" uuid, "sourceLocator" text, "geography" jsonb, "dimensions" jsonb,
            "metricCode" text, "metricValue" numeric, "metricUnit" text,
            "stability" text, "sensitivity" text, "activationPolicy" text, "evidenceNotes" jsonb)
        ON CONFLICT (source_id, source_locator, metric_code) DO NOTHING;
    """


def prepare_observations(source_id: str, observations: list[dict]) -> list[dict]:
    prepared: list[dict] = []
    for item in observations:
        sensitivity = "SENSITIVE_CONTEXT" if item["activationPolicy"] == "SENSITIVE_CONTEXT_ONLY" else "STANDARD"
        for metric_code, value, unit in (
            ("AUDIENCE_COUNT", item.get("audienceCount"), "PEOPLE"),
            ("SHARE_PERCENT", item.get("sharePercent"), "PERCENT"),
        ):
            if value is None:
                continue
            prepared.append({
                "id": str(uuid.uuid5(
                    NAMESPACE,
                    f"observation:{source_id}:{item['sourceLocator']}:{metric_code}")),
                "sourceLocator": item["sourceLocator"],
                "geography": item["geography"],
                "dimensions": item["dimensions"],
                "metricCode": metric_code,
                "metricValue": value,
                "metricUnit": unit,
                "stability": item["stability"],
                "sensitivity": sensitivity,
                "activationPolicy": item["activationPolicy"],
                "evidenceNotes": item.get("evidenceNotes", []),
            })
    return prepared


def build_sql(include_proof: bool) -> tuple[str, int, int]:
    payloads = [load_file(PRODUCTION)]
    if include_proof:
        payloads.append(load_file(PROOF))
    sources = [source for payload in payloads for source in payload["sources"]]
    observations = [item for payload in payloads for item in payload["observations"]]
    body = ["BEGIN;", "SET LOCAL statement_timeout = '120s';"]
    for source in sources:
        body.append(source_sql(source, observations))
    body.append("COMMIT;")
    return "\n".join(body), len(sources), len(observations)


def apply(sql: str) -> None:
    result = subprocess.run([
        "docker", "exec", "--user", "postgres", "--interactive", CONTAINER,
        "psql", "--set", "ON_ERROR_STOP=1", "--username", "advertified", "--dbname", "advertified",
    ], cwd=ROOT, input=sql.encode("utf-8"), text=False, capture_output=True, check=False)
    stdout = result.stdout.decode("utf-8", errors="replace").strip()
    stderr = result.stderr.decode("utf-8", errors="replace").strip()
    if result.returncode != 0:
        detail = stderr or stdout
        raise RuntimeError(f"Audience intelligence load failed safely. {detail}")
    if stdout:
        print(stdout)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--include-proof", action="store_true",
                        help="Also load proof-only/licence-pending research such as the public MAPS release.")
    args = parser.parse_args()
    inspect_container()
    sql, source_count, observation_count = build_sql(args.include_proof)
    apply(sql)
    print(f"Loaded {observation_count} audience observations from {source_count} sources into the shared intelligence kernel.")
    if args.include_proof:
        print("Proof-only sources remain tagged and are not production-promotable.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

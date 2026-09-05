"""Verify exported migration SQL in a disposable database on the existing local service."""
import argparse
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import uuid

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "agent-runtime"))
from master_data_codes import (
    Channels, Currencies, LifecycleStatuses, RateTypes, TenantTypes, VatStatuses,
)

CONTAINER = "advertified-os2-dev-postgres-1"


def execute(database: str, sql: str) -> None:
    result = subprocess.run(
        ["docker", "exec", "-i", CONTAINER, "psql", "-X", "-q", "-v",
         "ON_ERROR_STOP=1", "-U", "advertified", "-d", database],
        input=sql, encoding="utf-8", capture_output=True, check=False,
    )
    if result.returncode:
        raise RuntimeError(result.stderr)


def literal(value: str) -> str:
    return "'" + value.replace("'", "''") + "'"


def registry_sql() -> str:
    registry = json.loads((ROOT / "shared/contracts/master-data.json").read_text(encoding="utf-8"))
    statements = []
    version = literal(registry["registryVersion"])
    date = literal(registry["effectiveFrom"])
    for collection, items in registry["collections"].items():
        collection_sql = literal(collection)
        statements.append("INSERT INTO governance.master_data_collections VALUES "
                          f"({collection_sql}, {version}, {date}, now());")
        for item in items:
            values = [collection_sql, literal(item["code"]), literal(item["displayLabel"]),
                      str(item["isActive"]).lower(), str(item["sortOrder"]),
                      literal(json.dumps(item.get("metadata", {}))), date, "NULL", "now()", "now()"]
            statements.append("INSERT INTO governance.master_data_items VALUES (" + ",".join(values) + ");")
    return "\n".join(statements)


def acceptance_sql() -> str:
    tenant, actor, other, source = [str(uuid.uuid4()) for _ in range(4)]
    return f"""
        INSERT INTO commercial.tenants (id,type_code,legal_name,trading_name,slug,status_code,
            timezone,currency_code,vat_status_code,settings_json,version,created_at_utc,updated_at_utc)
        VALUES ('{tenant}','{TenantTypes.AGENCY}','Synthetic test','Synthetic test','brief-retention-test','{LifecycleStatuses.ACTIVE}',
            'Africa/Johannesburg','{Currencies.ZAR}','{VatStatuses.REGISTERED}','{{}}',1,now(),now());
        INSERT INTO commercial.users (id,email,display_name,status_code,mfa_enabled,version,created_at_utc,updated_at_utc)
        VALUES ('{actor}','brief@example.test','Synthetic owner','{LifecycleStatuses.ACTIVE}',true,1,now(),now()),
            ('{other}','other@example.test','Synthetic other','{LifecycleStatuses.ACTIVE}',true,1,now(),now());
        BEGIN;
        SET LOCAL ROLE advertified_app;
        SELECT set_config('advertified.tenant_id','{tenant}',true);
        SELECT set_config('advertified.user_id','{actor}',true);
        INSERT INTO commercial.supplied_brief_interpretations
            (id,tenant_id,actor_id,version_no,source_title,source_content,source_hash,input_hash,clarifications_json,created_at_utc)
        VALUES ('{source}','{tenant}','{actor}',1,'Original',E'  Original\\r\\n',
            encode(digest(convert_to(E'  Original\\r\\n','UTF8'),'sha256'),'hex'),repeat('a',64),'[]',now());
        DO $$ BEGIN
            IF (SELECT count(*) FROM commercial.supplied_brief_interpretations) <> 1 THEN
                RAISE EXCEPTION 'Own retained source is missing'; END IF;
            BEGIN
                UPDATE commercial.supplied_brief_interpretations SET source_content='changed';
                RAISE EXCEPTION 'Application may overwrite source';
            EXCEPTION WHEN insufficient_privilege THEN NULL; END;
            BEGIN
                INSERT INTO commercial.supplied_brief_interpretations
                    (id,tenant_id,actor_id,version_no,source_title,source_content,source_hash,input_hash,clarifications_json,created_at_utc)
                VALUES (gen_random_uuid(),'{tenant}','{actor}',1,'Forged','source',repeat('b',64),repeat('a',64),'[]',now());
                RAISE EXCEPTION 'Forged source hash accepted';
            EXCEPTION WHEN check_violation THEN NULL; END;
        END $$;
        SELECT set_config('advertified.user_id','{other}',true);
        DO $$ BEGIN
            IF EXISTS (SELECT 1 FROM commercial.supplied_brief_interpretations) THEN
                RAISE EXCEPTION 'Same-tenant different actor can read source'; END IF;
            BEGIN
                INSERT INTO commercial.supplied_brief_interpretations
                    (id,tenant_id,actor_id,parent_id,version_no,source_title,source_content,source_hash,input_hash,clarifications_json,created_at_utc)
                VALUES (gen_random_uuid(),'{tenant}','{other}','{source}',2,'Forged parent','source',
                    encode(digest('source','sha256'),'hex'),repeat('a',64),'[]',now());
                RAISE EXCEPTION 'Different actor can claim source lineage';
            EXCEPTION WHEN foreign_key_violation THEN NULL; END;
        END $$;
        SELECT set_config('advertified.user_id','{actor}',true);
        SELECT set_config('advertified.tenant_id',gen_random_uuid()::text,true);
        DO $$ BEGIN
            IF EXISTS (SELECT 1 FROM commercial.supplied_brief_interpretations) THEN
                RAISE EXCEPTION 'Cross-tenant source disclosure'; END IF;
        END $$;
        COMMIT;
        DO $$ BEGIN
            BEGIN
                UPDATE commercial.supplied_brief_interpretations SET source_content='changed';
            EXCEPTION WHEN raise_exception THEN RETURN; END;
            RAISE EXCEPTION 'Source mutation trigger did not reject administrator overwrite';
        END $$;
    """


def purchase_acceptance_sql() -> str:
    booking = str(uuid.uuid4())
    buyer, supplier = str(uuid.uuid4()), str(uuid.uuid4())
    references = [str(uuid.uuid4()) for _ in range(12)]
    return f"""
        SET session_replication_role = replica;
        DO $$ BEGIN
            IF (SELECT data_type FROM information_schema.columns
                WHERE table_schema='commercial' AND table_name='media_plan_lines'
                    AND column_name='purchase_json') <> 'jsonb' THEN
                RAISE EXCEPTION 'Plan purchase snapshot column is missing'; END IF;
            IF (SELECT data_type FROM information_schema.columns
                WHERE table_schema='commercial' AND table_name='bookings'
                    AND column_name='purchase_json') <> 'jsonb' THEN
                RAISE EXCEPTION 'Booking purchase snapshot column is missing'; END IF;
            BEGIN
                INSERT INTO commercial.bookings (
                    id,buyer_tenant_id,supplier_tenant_id,proposal_version_id,proposal_option_id,
                    proposal_decision_id,plan_version_id,media_plan_line_id,marketplace_listing_version_id,
                    commercial_policy_version_id,supplier_id,inventory_product_id,product_version_id,rate_id,
                    availability_id,supplier_name,product_name,channel_code,geography,flight_start,flight_end,
                    running_periods,quantity,supplier_cost_minor,markup_minor,commission_minor,
                    management_fee_minor,client_price_minor,fees_minor,vat_minor,
                    booking_approval_threshold_minor,currency_code,terms,status_code,created_by,
                    created_at_utc,version,updated_at_utc,purchase_json)
                VALUES ('{booking}','{buyer}','{supplier}',
                    {','.join(literal(value) for value in references)},
                    'Supplier','Product','{Channels.OOH}','Gauteng','2026-09-01','2026-09-30',1,1,
                    0,0,0,0,0,0,0,0,'{Currencies.ZAR}','Synthetic immutable snapshot',
                    '{LifecycleStatuses.DRAFT}','{references[0]}',
                    now(),1,now(),'[]'::jsonb);
                RAISE EXCEPTION 'Non-object booking purchase snapshot accepted';
            EXCEPTION WHEN check_violation THEN NULL; END;
        END $$;
        INSERT INTO commercial.bookings (
            id,buyer_tenant_id,supplier_tenant_id,proposal_version_id,proposal_option_id,
            proposal_decision_id,plan_version_id,media_plan_line_id,marketplace_listing_version_id,
            commercial_policy_version_id,supplier_id,inventory_product_id,product_version_id,rate_id,
            availability_id,supplier_name,product_name,channel_code,geography,flight_start,flight_end,
            running_periods,quantity,supplier_cost_minor,markup_minor,commission_minor,
            management_fee_minor,client_price_minor,fees_minor,vat_minor,
            booking_approval_threshold_minor,currency_code,terms,status_code,created_by,
            created_at_utc,version,updated_at_utc,purchase_json)
        VALUES ('{booking}','{buyer}','{supplier}',
            {','.join(literal(value) for value in references)},
            'Supplier','Product','{Channels.OOH}','Gauteng','2026-09-01','2026-09-30',1,100000,
            0,0,0,0,0,0,0,0,'{Currencies.ZAR}','Synthetic immutable snapshot',
            '{LifecycleStatuses.DRAFT}','{references[0]}',now(),1,now(),
            '{{"rateType":"{RateTypes.CPM}","quantity":100000,"denominator":1000}}'::jsonb);
        SET session_replication_role = origin;
        DO $$ BEGIN
            BEGIN
                UPDATE commercial.bookings SET purchase_json='{{"quantity":1}}'::jsonb
                WHERE id='{booking}';
            EXCEPTION WHEN raise_exception THEN RETURN; END;
            RAISE EXCEPTION 'Booking purchase snapshot mutation was accepted';
        END $$;
    """


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("migration", type=Path, help="SQL exported by the pinned C# migration test")
    parser.add_argument("--test-image", help="Already-built pinned SDK test image; no service or migration runner")
    parser.add_argument("--additional-migration", type=Path, help="Additional compiled migration SQL for the selected test")
    parser.add_argument("--test-filter", default="FullyQualifiedName~SuppliedBriefPersistenceTests",
                        choices=["FullyQualifiedName~SuppliedBriefPersistenceTests", "FullyQualifiedName~InventoryPurchasePersistenceTests"])
    args = parser.parse_args()
    migration = args.migration.read_text(encoding="utf-8")
    if args.additional_migration:
        migration += "\n" + args.additional_migration.read_text(encoding="utf-8")
    database = "advertified_brief_test_" + uuid.uuid4().hex
    if not re.fullmatch(r"advertified_brief_test_[0-9a-f]{32}", database):
        raise ValueError("Unsafe disposable database name")
    execute("postgres", f'CREATE DATABASE "{database}";')
    try:
        baseline = (ROOT / "api/src/Advertified.Commercial.Infrastructure/Migrations/InitialBaseline.g.sql").read_text(encoding="utf-8")
        execute(database, "CREATE EXTENSION pgcrypto; CREATE EXTENSION postgis; CREATE EXTENSION vector; CREATE EXTENSION pg_trgm;")
        execute(database, "BEGIN;\n" + baseline + "\n" + migration + "\nCOMMIT;")
        execute(database, registry_sql())
        if args.additional_migration:
            execute(database, purchase_acceptance_sql())
        execute(database, acceptance_sql())
        if args.test_image:
            run_connected_test(database, args.test_image, args.test_filter)
        print("PASS: baseline + exported migration, immutable exact source/hash, actor/tenant RLS and correction-parent isolation")
    finally:
        execute("postgres", f'DROP DATABASE "{database}";')
        print("Disposable logical database removed; existing application database untouched")


def run_connected_test(database: str, image: str, test_filter: str) -> None:
    inspected = subprocess.run(["docker", "inspect", CONTAINER], capture_output=True,
                               text=True, check=True)
    variables = dict(item.split("=", 1) for item in json.loads(inspected.stdout)[0]["Config"]["Env"])
    environment = dict(os.environ, PGHOST="postgres", PGDATABASE=database,
                       PGUSER="advertified", PGPASSWORD=variables["POSTGRES_PASSWORD"])
    command = ["docker", "run", "--rm", "--network", "advertified-os2-dev_default"]
    for name in ("PGHOST", "PGDATABASE", "PGUSER", "PGPASSWORD"):
        command.extend(["-e", name])
    command.extend([image, "dotnet", "test",
                    "api/tests/Advertified.Commercial.Api.Tests/Advertified.Commercial.Api.Tests.csproj",
                    "--configuration", "Release", "--no-build", "--no-restore", "--filter",
                    test_filter, "--verbosity", "normal"])
    subprocess.run(command, env=environment, check=True)


if __name__ == "__main__":
    main()

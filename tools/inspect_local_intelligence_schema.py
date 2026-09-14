"""Read-only schema and usage reconciliation for the canonical local intelligence store."""
from __future__ import annotations

import argparse
import subprocess

from load_local_audience_bootstrap import CONTAINER, ROOT, inspect_container

SQL = """
BEGIN READ ONLY;
SET LOCAL statement_timeout = '10s';
SELECT jsonb_build_object('columns', jsonb_agg(to_jsonb(c))) FROM (
    SELECT table_name, column_name, data_type, is_nullable, column_default
    FROM information_schema.columns
    WHERE table_schema = 'commercial'
      AND table_name IN ('intelligence_artifacts', 'intelligence_artifact_invocations')
    ORDER BY table_name, ordinal_position
) c;
SELECT jsonb_build_object('constraints', jsonb_agg(to_jsonb(c))) FROM (
    SELECT conname, pg_get_constraintdef(oid) AS definition
    FROM pg_constraint WHERE conrelid = 'commercial.intelligence_artifacts'::regclass
    ORDER BY conname
) c;
SELECT jsonb_build_object('artifacts', count(*), 'artifact_fingerprint',
    md5(COALESCE(string_agg(id::text || ':' || md5(artifact_json::text), ',' ORDER BY id), '')),
    'legacy_usage_rows', count(*) FILTER (WHERE to_jsonb(a)->>'agent_provider_code' IS NOT NULL))
FROM commercial.intelligence_artifacts a;
SELECT to_regclass('commercial.intelligence_artifact_invocations') IS NOT NULL AS has_invocations
\\gset
\\if :has_invocations
SELECT jsonb_build_object('invocations', count(*), 'cost_minor', COALESCE(sum(incremental_cost_minor), 0),
    'cost_usd_micros', COALESCE(sum(incremental_cost_usd_micros), 0))
FROM commercial.intelligence_artifact_invocations;
\\else
SELECT jsonb_build_object('invocations_table', 'MISSING');
\\endif
SELECT jsonb_build_object('kernel_tables', jsonb_agg(to_jsonb(c))) FROM (
    SELECT name, to_regclass('commercial.' || name)::text AS relation FROM unnest(ARRAY[
        'intelligence_artifacts', 'intelligence_artifact_invocations',
        'intelligence_artifact_dependencies', 'intelligence_artifact_evidence']) AS name
) c;
SELECT jsonb_build_object('legacy_usage', jsonb_agg(to_jsonb(c))) FROM (
    SELECT to_jsonb(a)->>'agent_provider_code' AS provider,
        to_jsonb(a)->>'agent_model_code' AS model, count(*) AS artifact_count,
        sum((to_jsonb(a)->>'agent_incremental_cost_minor')::bigint) AS cost_minor
    FROM commercial.intelligence_artifacts a
    GROUP BY 1, 2 ORDER BY 1, 2
) c;
SELECT jsonb_build_object('migrations', jsonb_agg("MigrationId")) FROM (
    SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 5
) h;
SELECT jsonb_build_object('owner_ai_budget', jsonb_build_object(
    'reservations', count(*),
    'committed_usd_micros', COALESCE(sum(maximum_cost_usd_micros), 0),
    'recorded_actual_usd_micros', COALESCE(sum(actual_cost_usd_micros), 0),
    'unreconciled', count(*) FILTER (WHERE actual_cost_usd_micros IS NULL),
    'ledger_fingerprint', md5(COALESCE(string_agg(to_jsonb(ledger)::text,
        ',' ORDER BY month_start_utc, run_id, step_id), '')),
    'guard_reported_committed_usd_micros', governance.read_ai_monthly_budget(CURRENT_DATE)))
FROM governance.ai_monthly_budget_ledger ledger;
COMMIT;
"""


LATEST_BRIEF_SQL = """
BEGIN READ ONLY;
SET LOCAL statement_timeout = '10s';
SELECT jsonb_build_object('interpretationId', source.id, 'status', step.status_code,
    'createdAtUtc', source.created_at_utc, 'result', step.output_json)
FROM commercial.supplied_brief_interpretations source
JOIN commercial.agent_run_steps step ON step.tenant_id = source.tenant_id AND step.run_id = source.id
WHERE source.tenant_id = '10000000-0000-0000-0000-000000000002'
ORDER BY source.created_at_utc DESC LIMIT 1;
COMMIT;
"""


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument('--latest-brief', action='store_true')
    args = parser.parse_args()
    inspect_container()
    sql = LATEST_BRIEF_SQL if args.latest_brief else SQL
    result = subprocess.run([
        'docker', 'exec', '--user', 'postgres', '--interactive', CONTAINER,
        'psql', '-X', '-qAt', '--set', 'ON_ERROR_STOP=1',
        '--username', 'advertified', '--dbname', 'advertified',
    ], cwd=ROOT, input=sql, encoding='utf-8', capture_output=True, timeout=30, check=False)
    if result.returncode != 0:
        raise RuntimeError('Read-only intelligence schema inspection failed: ' + result.stderr.strip())
    print(result.stdout.strip())


if __name__ == '__main__':
    main()

#!/usr/bin/env bash
set -euo pipefail

: "${ADVERTIFIED_RESTORE_S3_URI:?ADVERTIFIED_RESTORE_S3_URI is required}"
: "${ADVERTIFIED_DATABASE_SECRET_ARN:?ADVERTIFIED_DATABASE_SECRET_ARN is required}"
: "${ADVERTIFIED_RESTORE_CONFIRMATION:?ADVERTIFIED_RESTORE_CONFIRMATION is required}"
REGION=${ADVERTIFIED_AWS_REGION:-af-south-1}
CONTAINER=advertified-production-postgres
EXPECTED_CONFIRMATION=ISOLATED_REPLACEMENT_DATA_HOST

if [[ ${ADVERTIFIED_RESTORE_CONFIRMATION} != "${EXPECTED_CONFIRMATION}" ]]; then
  echo "Restore is permitted only on an explicitly confirmed isolated replacement data host." >&2
  exit 1
fi
if [[ ! ${ADVERTIFIED_RESTORE_S3_URI} =~ ^s3://[^/]+/.+\.dump$ ]]; then
  echo "Restore source must be an S3 custom-format .dump object." >&2
  exit 1
fi
[[ $(docker inspect --format '{{.State.Health.Status}}' "${CONTAINER}" 2>/dev/null || true) == "healthy" ]] || {
  echo "Production PostgreSQL target is not healthy." >&2
  exit 1
}

install -d -m 0700 /var/lib/advertified/backup/restore
DUMP=$(mktemp /var/lib/advertified/backup/restore/restore.XXXXXX.dump)
CHECKSUM="${DUMP}.sha256"
cleanup() { rm -f "${DUMP}" "${CHECKSUM}"; }
trap cleanup EXIT

aws s3 cp "${ADVERTIFIED_RESTORE_S3_URI}" "${DUMP}" --region "${REGION}" --only-show-errors
aws s3 cp "${ADVERTIFIED_RESTORE_S3_URI}.sha256" "${CHECKSUM}" --region "${REGION}" --only-show-errors
EXPECTED_SHA=$(awk 'NR==1 {print $1}' "${CHECKSUM}")
[[ ${EXPECTED_SHA} =~ ^[0-9a-f]{64}$ ]] || { echo "Backup checksum sidecar is invalid." >&2; exit 1; }
ACTUAL_SHA=$(sha256sum "${DUMP}" | awk '{print $1}')
[[ ${ACTUAL_SHA} == "${EXPECTED_SHA}" ]] || { echo "Backup checksum verification failed." >&2; exit 1; }

SECRET=$(aws secretsmanager get-secret-value \
  --region "${REGION}" \
  --secret-id "${ADVERTIFIED_DATABASE_SECRET_ARN}" \
  --query SecretString --output text)
DB_USER=$(jq -er '.admin.username' <<<"${SECRET}")
DB_NAME=$(jq -er '.database' <<<"${SECRET}")
unset SECRET

if docker exec "${CONTAINER}" psql --username "${DB_USER}" --dbname "${DB_NAME}" \
  --tuples-only --no-align --command \
  "SELECT CASE WHEN EXISTS (SELECT 1 FROM pg_catalog.pg_tables WHERE schemaname='commercial') THEN 'nonempty' ELSE 'empty' END;" | \
  grep -qx nonempty; then
  echo "Restore target already contains Advertified commercial tables; refusing destructive restore." >&2
  exit 1
fi

docker cp "${DUMP}" "${CONTAINER}:/tmp/advertified-restore.dump"
docker exec "${CONTAINER}" pg_restore \
  --exit-on-error \
  --no-owner \
  --username "${DB_USER}" \
  --dbname "${DB_NAME}" \
  /tmp/advertified-restore.dump
docker exec "${CONTAINER}" rm -f /tmp/advertified-restore.dump

EXTENSIONS=$(docker exec "${CONTAINER}" psql --username "${DB_USER}" --dbname "${DB_NAME}" \
  --tuples-only --no-align --command \
  "SELECT string_agg(extname || '=' || extversion, ',' ORDER BY extname) FROM pg_extension WHERE extname IN ('postgis','vector');")
MIGRATIONS=$(docker exec "${CONTAINER}" psql --username "${DB_USER}" --dbname "${DB_NAME}" \
  --tuples-only --no-align --command 'SELECT count(*) FROM "__EFMigrationsHistory";')
MASTER_DATA=$(docker exec "${CONTAINER}" psql --username "${DB_USER}" --dbname "${DB_NAME}" \
  --tuples-only --no-align --command 'SELECT count(*) FROM governance.master_data_collections;')
[[ ${EXTENSIONS} == *postgis=* && ${EXTENSIONS} == *vector=* ]] || {
  echo "Restored database is missing PostGIS or pgvector." >&2
  exit 1
}
[[ ${MIGRATIONS} =~ ^[1-9][0-9]*$ ]] || { echo "Restored migration history is empty." >&2; exit 1; }
[[ ${MASTER_DATA} =~ ^[1-9][0-9]*$ ]] || { echo "Restored master data is empty." >&2; exit 1; }

STAMP=$(date --utc +%Y%m%dT%H%M%SZ)
RECEIPT=/var/lib/advertified/backup/restore/restore-${STAMP}.json
jq -n \
  --arg restoredAtUtc "$(date --utc +%Y-%m-%dT%H:%M:%SZ)" \
  --arg source "${ADVERTIFIED_RESTORE_S3_URI}" \
  --arg sha256 "${ACTUAL_SHA}" \
  --arg extensions "${EXTENSIONS}" \
  --argjson migrations "${MIGRATIONS}" \
  --argjson masterDataCollections "${MASTER_DATA}" \
  '{schema:"advertified.database-restore-receipt.v1",restoredAtUtc:$restoredAtUtc,source:$source,sha256:$sha256,extensions:$extensions,migrations:$migrations,masterDataCollections:$masterDataCollections}' \
  >"${RECEIPT}"
chmod 0600 "${RECEIPT}"
echo "Advertified isolated database restore completed; receipt: ${RECEIPT}"

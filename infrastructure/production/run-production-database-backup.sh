#!/usr/bin/env bash
set -euo pipefail

: "${ADVERTIFIED_BACKUP_BUCKET:?ADVERTIFIED_BACKUP_BUCKET is required}"
: "${ADVERTIFIED_DATABASE_SECRET_ARN:?ADVERTIFIED_DATABASE_SECRET_ARN is required}"
REGION=${ADVERTIFIED_AWS_REGION:-af-south-1}
CONTAINER=advertified-production-postgres

TOKEN=$(curl --fail --silent --show-error --max-time 2 \
  -X PUT -H 'X-aws-ec2-metadata-token-ttl-seconds: 21600' \
  http://169.254.169.254/latest/api/token)
INSTANCE_ID=$(curl --fail --silent --show-error --max-time 2 \
  -H "X-aws-ec2-metadata-token: ${TOKEN}" \
  http://169.254.169.254/latest/meta-data/instance-id)

emit_failure() {
  aws cloudwatch put-metric-data \
    --region "${REGION}" \
    --namespace 'Advertified/Production' \
    --metric-name DatabaseLogicalBackupFailures \
    --dimensions "HostRole=database,InstanceId=${INSTANCE_ID}" \
    --unit Count --value 1 >/dev/null 2>&1 || true
}
trap 'emit_failure' ERR

[[ $(docker inspect --format '{{.State.Health.Status}}' "${CONTAINER}" 2>/dev/null || true) == "healthy" ]] || {
  echo "Production PostgreSQL is not healthy; logical backup aborted." >&2
  exit 1
}

SECRET=$(aws secretsmanager get-secret-value \
  --region "${REGION}" \
  --secret-id "${ADVERTIFIED_DATABASE_SECRET_ARN}" \
  --query SecretString --output text)
DB_USER=$(jq -er '.admin.username' <<<"${SECRET}")
DB_NAME=$(jq -er '.database' <<<"${SECRET}")
unset SECRET

STAMP=$(date --utc +%Y%m%dT%H%M%SZ)
KEY="postgres/${STAMP}/advertified.dump"
TMP=$(mktemp /var/lib/advertified/backup/advertified.XXXXXX.dump)
cleanup() { rm -f "${TMP}"; }
trap cleanup EXIT

docker exec "${CONTAINER}" pg_dump \
  --username "${DB_USER}" \
  --dbname "${DB_NAME}" \
  --format=custom \
  --no-owner >"${TMP}"
[[ -s ${TMP} ]] || { echo "Logical backup archive is empty." >&2; exit 1; }

SHA256=$(sha256sum "${TMP}" | awk '{print $1}')
aws s3 cp "${TMP}" "s3://${ADVERTIFIED_BACKUP_BUCKET}/${KEY}" \
  --region "${REGION}" \
  --sse AES256 \
  --only-show-errors
printf '%s  %s\n' "${SHA256}" "${KEY}" | \
  aws s3 cp - "s3://${ADVERTIFIED_BACKUP_BUCKET}/${KEY}.sha256" \
    --region "${REGION}" --sse AES256 --only-show-errors

date --utc +%Y-%m-%dT%H:%M:%SZ >/var/lib/advertified/backup/last-success-utc
chmod 0600 /var/lib/advertified/backup/last-success-utc
trap - ERR
aws cloudwatch put-metric-data \
  --region "${REGION}" \
  --namespace 'Advertified/Production' \
  --metric-name DatabaseLogicalBackupFailures \
  --dimensions "HostRole=database,InstanceId=${INSTANCE_ID}" \
  --unit Count --value 0

echo "Advertified logical backup uploaded to s3://${ADVERTIFIED_BACKUP_BUCKET}/${KEY}."

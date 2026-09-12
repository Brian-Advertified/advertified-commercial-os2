#!/usr/bin/env bash
set -euo pipefail

: "${ADVERTIFIED_DATABASE_IMAGE:?ADVERTIFIED_DATABASE_IMAGE is required}"
: "${ADVERTIFIED_DATABASE_SECRET_ARN:?ADVERTIFIED_DATABASE_SECRET_ARN is required}"
REGION=${ADVERTIFIED_AWS_REGION:-af-south-1}
CONTAINER=advertified-production-postgres
PROVISIONER=/usr/local/sbin/advertified-provision-database-logins

if [[ ! ${ADVERTIFIED_DATABASE_IMAGE} =~ @sha256:[0-9a-f]{64}$ ]]; then
  echo "Database image must be an immutable sha256 digest." >&2
  exit 1
fi
if [[ ! -d /var/lib/advertified/postgres/data ]] || ! mountpoint -q /var/lib/advertified/postgres; then
  echo "Dedicated database volume is not mounted." >&2
  exit 1
fi
[[ -x ${PROVISIONER} ]] || { echo "Database login-role provisioner is unavailable." >&2; exit 1; }

SECRET=$(aws secretsmanager get-secret-value \
  --region "${REGION}" \
  --secret-id "${ADVERTIFIED_DATABASE_SECRET_ARN}" \
  --query SecretString --output text)
DB_USER=$(jq -er '.admin.username' <<<"${SECRET}")
DB_NAME=$(jq -er '.database' <<<"${SECRET}")
DB_PASSWORD=$(jq -er '.admin.password' <<<"${SECRET}")
unset SECRET

[[ ${DB_USER} =~ ^[A-Za-z_][A-Za-z0-9_]{0,62}$ ]] || { echo "Database admin username is invalid." >&2; exit 1; }
[[ ${DB_NAME} =~ ^[A-Za-z_][A-Za-z0-9_]{0,62}$ ]] || { echo "Database name is invalid." >&2; exit 1; }
[[ ${#DB_PASSWORD} -ge 24 && ${#DB_PASSWORD} -le 128 && ${DB_PASSWORD} != *$'\n'* && ${DB_PASSWORD} != *$'\r'* ]] || {
  echo "Database admin password must be 24-128 characters without line breaks." >&2
  exit 1
}

REGISTRY=${ADVERTIFIED_DATABASE_IMAGE%%/*}
aws ecr get-login-password --region "${REGION}" | \
  docker login --username AWS --password-stdin "${REGISTRY}"
docker pull "${ADVERTIFIED_DATABASE_IMAGE}"

TOKEN=$(curl --fail --silent --show-error --max-time 2 \
  -X PUT -H 'X-aws-ec2-metadata-token-ttl-seconds: 21600' \
  http://169.254.169.254/latest/api/token)
PRIVATE_IP=$(curl --fail --silent --show-error --max-time 2 \
  -H "X-aws-ec2-metadata-token: ${TOKEN}" \
  http://169.254.169.254/latest/meta-data/local-ipv4)

NEEDS_START=true
if docker container inspect "${CONTAINER}" >/dev/null 2>&1; then
  CURRENT_IMAGE=$(docker inspect --format '{{.Config.Image}}' "${CONTAINER}")
  if [[ ${CURRENT_IMAGE} == "${ADVERTIFIED_DATABASE_IMAGE}" ]] && \
      [[ $(docker inspect --format '{{.State.Running}}' "${CONTAINER}") == "true" ]]; then
    NEEDS_START=false
  else
    docker rm --force "${CONTAINER}" >/dev/null
  fi
fi

if [[ ${NEEDS_START} == "true" ]]; then
  install -d -m 0700 /run/advertified-postgres
  printf '%s' "${DB_PASSWORD}" >/run/advertified-postgres/password
  chmod 0600 /run/advertified-postgres/password

  docker run --detach \
    --name "${CONTAINER}" \
    --restart unless-stopped \
    --security-opt no-new-privileges:true \
    --cap-drop ALL \
    --cap-add CHOWN \
    --cap-add DAC_OVERRIDE \
    --cap-add FOWNER \
    --cap-add SETGID \
    --cap-add SETUID \
    --publish "${PRIVATE_IP}:5432:5432" \
    --volume /var/lib/advertified/postgres/data:/var/lib/postgresql/data \
    --volume /run/advertified-postgres/password:/run/secrets/postgres_password:ro \
    --tmpfs /tmp:rw,noexec,nosuid,size=128m \
    --tmpfs /run/postgresql:rw,noexec,nosuid,size=16m \
    --env "POSTGRES_USER=${DB_USER}" \
    --env "POSTGRES_DB=${DB_NAME}" \
    --env POSTGRES_PASSWORD_FILE=/run/secrets/postgres_password \
    --health-cmd /usr/local/bin/advertified-healthcheck \
    --health-interval 10s \
    --health-timeout 5s \
    --health-retries 12 \
    --health-start-period 30s \
    "${ADVERTIFIED_DATABASE_IMAGE}" >/dev/null
fi
unset DB_PASSWORD

for _ in $(seq 1 60); do
  STATUS=$(docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' "${CONTAINER}")
  if [[ ${STATUS} == "healthy" ]]; then
    ADVERTIFIED_AWS_REGION="${REGION}" \
      ADVERTIFIED_DATABASE_SECRET_ARN="${ADVERTIFIED_DATABASE_SECRET_ARN}" \
      "${PROVISIONER}"
    echo "Advertified production PostgreSQL is healthy and governed login roles are provisioned."
    exit 0
  fi
  [[ ${STATUS} == "unhealthy" || ${STATUS} == "exited" || ${STATUS} == "dead" ]] && break
  sleep 2
done

echo "Advertified production PostgreSQL failed readiness." >&2
docker logs --tail 50 "${CONTAINER}" >&2 || true
exit 1

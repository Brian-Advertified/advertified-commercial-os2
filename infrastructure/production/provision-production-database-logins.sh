#!/usr/bin/env bash
set -euo pipefail

: "${ADVERTIFIED_DATABASE_SECRET_ARN:?ADVERTIFIED_DATABASE_SECRET_ARN is required}"
REGION=${ADVERTIFIED_AWS_REGION:-af-south-1}
CONTAINER=advertified-production-postgres
ROLE_SQL=${ADVERTIFIED_DATABASE_GROUP_ROLE_SQL:-/etc/advertified/02-provision-database-roles.sql}

[[ -r ${ROLE_SQL} ]] || { echo "Governed database group-role SQL is unavailable." >&2; exit 1; }
[[ $(docker inspect --format '{{.State.Health.Status}}' "${CONTAINER}" 2>/dev/null || true) == "healthy" ]] || {
  echo "Production PostgreSQL is not healthy." >&2
  exit 1
}

SECRET=$(aws secretsmanager get-secret-value \
  --region "${REGION}" \
  --secret-id "${ADVERTIFIED_DATABASE_SECRET_ARN}" \
  --query SecretString --output text)
ADMIN_USER=$(jq -er '.admin.username' <<<"${SECRET}")
DB_NAME=$(jq -er '.database' <<<"${SECRET}")
API_USER=$(jq -er '.api.username' <<<"${SECRET}")
API_PASSWORD=$(jq -er '.api.password' <<<"${SECRET}")
WORKER_USER=$(jq -er '.worker.username' <<<"${SECRET}")
WORKER_PASSWORD=$(jq -er '.worker.password' <<<"${SECRET}")
MIGRATOR_USER=$(jq -er '.migrator.username' <<<"${SECRET}")
MIGRATOR_PASSWORD=$(jq -er '.migrator.password' <<<"${SECRET}")
unset SECRET

validate_identifier() {
  [[ $1 =~ ^[A-Za-z_][A-Za-z0-9_]{0,62}$ ]] || {
    echo "Database role identifier is invalid." >&2
    exit 1
  }
}
validate_password() {
  [[ ${#1} -ge 24 && ${#1} -le 128 && $1 != *$'\n'* && $1 != *$'\r'* ]] || {
    echo "Database login password must be 24-128 characters without line breaks." >&2
    exit 1
  }
}
sql_literal() { printf '%s' "$1" | sed "s/'/''/g"; }

for value in "${ADMIN_USER}" "${DB_NAME}" "${API_USER}" "${WORKER_USER}" "${MIGRATOR_USER}"; do
  validate_identifier "${value}"
done
[[ ${API_USER} != "${WORKER_USER}" && ${API_USER} != "${MIGRATOR_USER}" && ${WORKER_USER} != "${MIGRATOR_USER}" ]] || {
  echo "API, worker and migrator database logins must be distinct." >&2
  exit 1
}
validate_password "${API_PASSWORD}"
validate_password "${WORKER_PASSWORD}"
validate_password "${MIGRATOR_PASSWORD}"

cat "${ROLE_SQL}" | docker exec --interactive "${CONTAINER}" \
  psql --set ON_ERROR_STOP=1 --username "${ADMIN_USER}" --dbname "${DB_NAME}"

provision_login() {
  local login=$1 password=$2 governed_role=$3 escaped exists
  escaped=$(sql_literal "${password}")
  exists=$(docker exec "${CONTAINER}" psql --username "${ADMIN_USER}" --dbname "${DB_NAME}" \
    --tuples-only --no-align --command "SELECT 1 FROM pg_roles WHERE rolname = '${login}';")
  {
    if [[ ${exists} != "1" ]]; then
      printf "CREATE ROLE \"%s\" LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOBYPASSRLS PASSWORD '%s';\n" \
        "${login}" "${escaped}"
    fi
    printf "ALTER ROLE \"%s\" PASSWORD '%s';\n" "${login}" "${escaped}"
    printf "GRANT %s TO \"%s\";\n" "${governed_role}" "${login}"
  } | docker exec --interactive "${CONTAINER}" \
      psql --set ON_ERROR_STOP=1 --username "${ADMIN_USER}" --dbname "${DB_NAME}"
}

provision_login "${API_USER}" "${API_PASSWORD}" advertified_app
provision_login "${WORKER_USER}" "${WORKER_PASSWORD}" advertified_worker
provision_login "${MIGRATOR_USER}" "${MIGRATOR_PASSWORD}" advertified_migrator

unset API_PASSWORD WORKER_PASSWORD MIGRATOR_PASSWORD
echo "Advertified production database login-role boundary is provisioned."

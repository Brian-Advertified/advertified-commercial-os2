#!/bin/sh
set -eu

database="${POSTGRES_DB:-advertified}"
user="${POSTGRES_USER:-advertified}"

pg_isready -U "$user" -d "$database"
psql -U "$user" -d "$database" -v ON_ERROR_STOP=1   -f /opt/advertified/verify-extensions.sql >/dev/null
psql -U "$user" -d "$database" -Atc "SHOW server_version_num;" |
  grep -Eq '^16'

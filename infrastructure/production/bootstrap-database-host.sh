#!/usr/bin/env bash
set -euo pipefail

: "${ADVERTIFIED_BACKUP_BUCKET:?ADVERTIFIED_BACKUP_BUCKET is required}"
: "${ADVERTIFIED_DATABASE_SECRET_ARN:?ADVERTIFIED_DATABASE_SECRET_ARN is required}"

SCRIPT_DIR=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
export ADVERTIFIED_HOST_ROLE=database
export ADVERTIFIED_AWS_REGION=${ADVERTIFIED_AWS_REGION:-af-south-1}
export ADVERTIFIED_DISK_PATH=/var/lib/advertified/postgres
"${SCRIPT_DIR}/bootstrap-host-common.sh"

apt-get update
apt-get install --yes --no-install-recommends e2fsprogs
rm -rf /var/lib/apt/lists/*

DEVICE=""
for _ in $(seq 1 60); do
  if [[ -n ${ADVERTIFIED_DATABASE_VOLUME_ID:-} ]]; then
    VOLUME_SERIAL=${ADVERTIFIED_DATABASE_VOLUME_ID//-/}
    while read -r name serial; do
      if [[ ${serial//-/} == "${VOLUME_SERIAL}" ]]; then
        DEVICE="/dev/${name}"
        break
      fi
    done < <(lsblk -dn -o NAME,SERIAL)
  else
    mapfile -t CANDIDATES < <(
      while read -r name type; do
        [[ ${type} == "disk" ]] || continue
        if ! lsblk -nr -o MOUNTPOINT "/dev/${name}" | grep -q '[^[:space:]]'; then
          printf '/dev/%s\n' "${name}"
        fi
      done < <(lsblk -dn -o NAME,TYPE)
    )
    if [[ ${#CANDIDATES[@]} -eq 1 ]]; then
      DEVICE=${CANDIDATES[0]}
    elif [[ ${#CANDIDATES[@]} -gt 1 ]]; then
      echo "More than one unmounted data disk is present; set ADVERTIFIED_DATABASE_VOLUME_ID explicitly." >&2
      exit 1
    fi
  fi
  [[ -n ${DEVICE} ]] && break
  sleep 2
done
[[ -n ${DEVICE} ]] || { echo "Dedicated Advertified database EBS volume was not found." >&2; exit 1; }

if ! blkid -s TYPE -o value "${DEVICE}" >/dev/null 2>&1; then
  mkfs.ext4 -F -L advertified-postgres "${DEVICE}"
fi
FS_TYPE=$(blkid -s TYPE -o value "${DEVICE}")
[[ ${FS_TYPE} == "ext4" ]] || { echo "Database volume must use ext4." >&2; exit 1; }
FS_UUID=$(blkid -s UUID -o value "${DEVICE}")
[[ -n ${FS_UUID} ]] || { echo "Database volume UUID is unavailable." >&2; exit 1; }

install -d -m 0700 -o root -g root /var/lib/advertified/postgres
if ! grep -qE "^UUID=${FS_UUID}[[:space:]]" /etc/fstab; then
  printf 'UUID=%s /var/lib/advertified/postgres ext4 defaults,nofail,nodev,nosuid 0 2\n' "${FS_UUID}" >>/etc/fstab
fi
mount /var/lib/advertified/postgres || mount -a
mountpoint -q /var/lib/advertified/postgres || { echo "Database volume mount failed." >&2; exit 1; }
install -d -m 0700 -o root -g root /var/lib/advertified/postgres/data /var/lib/advertified/backup

cat >/etc/advertified/database-runtime.env <<EOF
ADVERTIFIED_AWS_REGION=${ADVERTIFIED_AWS_REGION}
ADVERTIFIED_BACKUP_BUCKET=${ADVERTIFIED_BACKUP_BUCKET}
ADVERTIFIED_DATABASE_SECRET_ARN=${ADVERTIFIED_DATABASE_SECRET_ARN}
EOF
chmod 0600 /etc/advertified/database-runtime.env

ROLE_SQL_SOURCE="${SCRIPT_DIR}/02-provision-database-roles.sql"
if [[ ! -r ${ROLE_SQL_SOURCE} ]]; then
  ROLE_SQL_SOURCE="${SCRIPT_DIR}/../init-scripts/02-provision-database-roles.sql"
fi
[[ -r ${ROLE_SQL_SOURCE} ]] || { echo "Governed database group-role SQL source is unavailable." >&2; exit 1; }
install -m 0644 "${ROLE_SQL_SOURCE}" /etc/advertified/02-provision-database-roles.sql
install -m 0755 "${SCRIPT_DIR}/provision-production-database-logins.sh" /usr/local/sbin/advertified-provision-database-logins
install -m 0755 "${SCRIPT_DIR}/activate-production-database.sh" /usr/local/sbin/advertified-activate-production-database
install -m 0755 "${SCRIPT_DIR}/run-production-database-backup.sh" /usr/local/sbin/advertified-database-backup
install -m 0755 "${SCRIPT_DIR}/restore-production-database-backup.sh" /usr/local/sbin/advertified-database-restore

cat >/etc/systemd/system/advertified-database-backup.service <<'EOF'
[Unit]
Description=Advertified encrypted logical PostgreSQL backup
After=docker.service network-online.target
Wants=network-online.target
Requires=docker.service

[Service]
Type=oneshot
EnvironmentFile=/etc/advertified/database-runtime.env
ExecStart=/usr/local/sbin/advertified-database-backup
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ProtectHome=true
ReadWritePaths=/var/lib/advertified/backup
EOF

cat >/etc/systemd/system/advertified-database-backup.timer <<'EOF'
[Unit]
Description=Run Advertified logical PostgreSQL backup daily

[Timer]
OnCalendar=*-*-* 01:15:00 UTC
RandomizedDelaySec=300
Persistent=true

[Install]
WantedBy=timers.target
EOF

systemctl daemon-reload
systemctl enable --now advertified-database-backup.timer advertified-host-metrics.timer

echo "Advertified database host bootstrap complete. Activate only an approved digest-pinned database image."

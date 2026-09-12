#!/usr/bin/env bash
set -euo pipefail

if [[ ${EUID} -ne 0 ]]; then
  echo "Advertified production host bootstrap must run as root." >&2
  exit 1
fi

if [[ ! -r /etc/os-release ]]; then
  echo "Cannot verify the production host operating system." >&2
  exit 1
fi
# shellcheck disable=SC1091
source /etc/os-release
if [[ ${ID:-} != "ubuntu" || ${VERSION_ID:-} != "24.04" ]]; then
  echo "Advertified production bootstrap supports Ubuntu 24.04 LTS only." >&2
  exit 1
fi

export DEBIAN_FRONTEND=noninteractive
apt-get update
apt-get install --yes --no-install-recommends \
  awscli ca-certificates curl docker.io docker-compose-v2 jq
rm -rf /var/lib/apt/lists/*
systemctl enable --now docker

install -d -m 0750 -o root -g root /etc/advertified /opt/advertified
install -d -m 0700 -o root -g root /var/lib/advertified

install -m 0755 /dev/stdin /usr/local/sbin/advertified-cloudwatch-host-metrics <<'METRICS'
#!/usr/bin/env bash
set -euo pipefail

ROLE=${ADVERTIFIED_HOST_ROLE:?ADVERTIFIED_HOST_ROLE is required}
DISK_PATH=${ADVERTIFIED_DISK_PATH:-/}
REGION=${ADVERTIFIED_AWS_REGION:-af-south-1}
TOKEN=$(curl --fail --silent --show-error --max-time 2 \
  -X PUT -H 'X-aws-ec2-metadata-token-ttl-seconds: 21600' \
  http://169.254.169.254/latest/api/token)
INSTANCE_ID=$(curl --fail --silent --show-error --max-time 2 \
  -H "X-aws-ec2-metadata-token: ${TOKEN}" \
  http://169.254.169.254/latest/meta-data/instance-id)
USED_PERCENT=$(df -P "${DISK_PATH}" | awk 'NR==2 {gsub(/%/, "", $5); print $5}')
[[ ${USED_PERCENT} =~ ^[0-9]+$ ]] || { echo "Disk utilisation was not numeric." >&2; exit 1; }

aws cloudwatch put-metric-data \
  --region "${REGION}" \
  --namespace 'Advertified/Production' \
  --metric-name HostDiskUsedPercent \
  --dimensions "HostRole=${ROLE},InstanceId=${INSTANCE_ID}" \
  --unit Percent \
  --value "${USED_PERCENT}"

if [[ ${ROLE} == "database" ]]; then
  LAST_SUCCESS=/var/lib/advertified/backup/last-success-utc
  if [[ -s ${LAST_SUCCESS} ]]; then
    LAST_EPOCH=$(date --utc --date="$(cat "${LAST_SUCCESS}")" +%s)
    NOW_EPOCH=$(date --utc +%s)
    AGE=$((NOW_EPOCH - LAST_EPOCH))
    (( AGE >= 0 )) || AGE=0
  else
    AGE=999999999
  fi
  aws cloudwatch put-metric-data \
    --region "${REGION}" \
    --namespace 'Advertified/Production' \
    --metric-name DatabaseLogicalBackupAgeSeconds \
    --dimensions "HostRole=database,InstanceId=${INSTANCE_ID}" \
    --unit Seconds \
    --value "${AGE}"
fi
METRICS

cat >/etc/systemd/system/advertified-host-metrics.service <<EOF
[Unit]
Description=Advertified production host metrics
After=network-online.target
Wants=network-online.target

[Service]
Type=oneshot
Environment=ADVERTIFIED_HOST_ROLE=${ADVERTIFIED_HOST_ROLE:?ADVERTIFIED_HOST_ROLE is required}
Environment=ADVERTIFIED_DISK_PATH=${ADVERTIFIED_DISK_PATH:-/}
Environment=ADVERTIFIED_AWS_REGION=${ADVERTIFIED_AWS_REGION:-af-south-1}
ExecStart=/usr/local/sbin/advertified-cloudwatch-host-metrics
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ProtectHome=true
ReadWritePaths=/var/lib/advertified
EOF

cat >/etc/systemd/system/advertified-host-metrics.timer <<'EOF'
[Unit]
Description=Publish Advertified host metrics every five minutes

[Timer]
OnBootSec=2min
OnUnitActiveSec=5min
AccuracySec=30s
Persistent=true

[Install]
WantedBy=timers.target
EOF

systemctl daemon-reload
systemctl enable --now advertified-host-metrics.timer

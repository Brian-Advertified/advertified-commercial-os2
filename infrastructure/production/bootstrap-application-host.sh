#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
export ADVERTIFIED_HOST_ROLE=application
export ADVERTIFIED_DISK_PATH=/
export ADVERTIFIED_AWS_REGION=${ADVERTIFIED_AWS_REGION:-af-south-1}
"${SCRIPT_DIR}/bootstrap-host-common.sh"

install -d -m 0700 -o root -g root /var/lib/advertified/data-protection-keys
install -d -m 0750 -o root -g root /opt/advertified/releases
install -d -m 0750 -o root -g root /opt/advertified/current

echo "Advertified application host bootstrap complete. Deployment remains separate and digest-pinned."

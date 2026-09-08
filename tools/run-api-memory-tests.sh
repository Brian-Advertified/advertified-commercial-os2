#!/usr/bin/env bash
set -euo pipefail
test -n "${ADVERTIFIED_TEST_FILTER:?A bounded test filter is required}"
tar -C /source --exclude='*/bin' --exclude='*/obj' -cf - \
    api shared .editorconfig Directory.Build.props global.json AGENTS.md ADVERTIFIED.md | tar -C /work -xf -
mkdir -p /work/infrastructure/development
cp /source/infrastructure/development/publish-current-inventory-to-marketplace.sql /work/infrastructure/development/
dotnet restore api/tests/Advertified.Commercial.Api.Tests/Advertified.Commercial.Api.Tests.csproj --locked-mode
dotnet test api/tests/Advertified.Commercial.Api.Tests/Advertified.Commercial.Api.Tests.csproj \
    --configuration Release --no-restore --filter "$ADVERTIFIED_TEST_FILTER" --verbosity minimal

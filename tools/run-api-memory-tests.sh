#!/usr/bin/env bash
set -euo pipefail
test -n "${ADVERTIFIED_TEST_FILTER:?A bounded test filter is required}"
tar -C /source --exclude='*/bin' --exclude='*/obj' -cf - \
    api shared .editorconfig Directory.Build.props global.json AGENTS.md ADVERTIFIED.md | tar -C /work -xf -
mkdir -p /work/infrastructure/development /work/infrastructure/production
cp /source/infrastructure/production/appsettings.Production.example.json /work/infrastructure/production/
cp /source/infrastructure/development/publish-current-inventory-to-marketplace.sql /work/infrastructure/development/

project=api/tests/Advertified.Commercial.Api.Tests/Advertified.Commercial.Api.Tests.csproj
dotnet restore "$project" --locked-mode
if [ "${ADVERTIFIED_TEST_PARTITION_BY_CATEGORY:-0}" != 1 ]; then
    evidence_args=()
    if [ -n "${ADVERTIFIED_TEST_EVIDENCE_DIRECTORY:-}" ]; then
        evidence_args=(--logger 'trx;LogFileName=api.trx' --results-directory "$ADVERTIFIED_TEST_EVIDENCE_DIRECTORY")
    fi
    dotnet test "$project" --configuration Release --no-restore \
        --filter "$ADVERTIFIED_TEST_FILTER" --verbosity minimal "${evidence_args[@]}"
    exit
fi
evidence="${ADVERTIFIED_TEST_EVIDENCE_DIRECTORY:?Partitioned verification requires evidence}"
dotnet build "$project" --configuration Release --no-restore --disable-build-servers
dotnet build-server shutdown
dotnet test "$project" --configuration Release --no-build --list-tests \
    --filter "$ADVERTIFIED_TEST_FILTER" > "$evidence/discovered-tests.txt"
names=(unit database migration recovery)
filters=('Category!=Migration&Category!=Database&Category!=Recovery'
    'Category=Database&Category!=Migration&Category!=Recovery'
    'Category=Migration&Category!=Recovery' 'Category=Recovery')
# Each scenario family gets a fresh host; long commercial journeys otherwise
# retain enough test-host state to exceed the verifier's fixed memory boundary.
scenario_names=(opportunity inventory planning transaction funding measurement)
scenario_methods=(CanonicalOpportunityScenario CanonicalInventoryScenario CanonicalPlanningScenario
    CanonicalTransactionScenario CanonicalFundingScenario CanonicalMeasurementScenario)
for index in "${!scenario_names[@]}"; do
    method="${scenario_methods[$index]}"
    names+=("scenario-${scenario_names[$index]}")
    scenario_filter="FullyQualifiedName~$method"
    if [ "$method" = CanonicalFundingScenario ]; then
        scenario_filter+="|FullyQualifiedName~ManualFundingRouteRetainsIndependentHumanReconciliation"
    fi
    filters+=("$scenario_filter")
    for base in 0 1 2 3; do
        filters[$base]+="&FullyQualifiedName!~$method"
    done
done
for base in 0 1 2 3; do
    filters[$base]+='&FullyQualifiedName!~ManualFundingRouteRetainsIndependentHumanReconciliation'
done
status=0
for index in "${!names[@]}"; do
    name="${names[$index]}"
    filter="($ADVERTIFIED_TEST_FILTER)&(${filters[$index]})"
    printf '%s\t%s\n' "$name" "$filter" >> "$evidence/partition-filters.tsv"
    if dotnet test "$project" --configuration Release --no-build --filter "$filter" \
        --verbosity minimal --logger "trx;LogFileName=$name.trx" --results-directory "$evidence" \
        > "$evidence/$name.log" 2>&1; then
        printf '%s\t0\n' "$name" >> "$evidence/partition-exit-codes.tsv"
    else
        result=$?
        printf '%s\t%s\n' "$name" "$result" >> "$evidence/partition-exit-codes.tsv"
        status=1
    fi
    cat "$evidence/$name.log"
done
exit "$status"

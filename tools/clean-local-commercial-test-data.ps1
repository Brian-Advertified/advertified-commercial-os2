param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'advertified-compose.ps1')
$composeFiles = @('infrastructure/docker-compose.yml','infrastructure/docker-compose.app.yml')
$container = 'advertified-os2-dev-postgres-1'

Push-Location $repoRoot
try {
    Assert-AdvertifiedComposeProject -RequireExisting
    $labelsJson = & docker inspect --format '{{json .Config.Labels}}' $container
    if ($LASTEXITCODE -ne 0) { throw 'Local Advertified PostgreSQL container could not be inspected.' }
    $labels = $labelsJson | ConvertFrom-Json
    if ($labels.'com.docker.compose.project' -ne 'advertified-os2-dev' -or
        $labels.'com.docker.compose.service' -ne 'postgres') {
        throw 'Refusing cleanup outside the Advertified local development PostgreSQL container.'
    }

    $sql = @'
\pset tuples_only on
\pset format unaligned
SELECT 'before.briefs=' || count(*) FROM commercial.campaign_briefs;
SELECT 'before.brief_versions=' || count(*) FROM commercial.brief_versions;
SELECT 'before.proposals=' || count(*) FROM commercial.proposal_versions;
SELECT 'before.tasks=' || count(*) FROM commercial.human_tasks;
BEGIN;
TRUNCATE TABLE
    commercial.human_tasks,
    commercial.proposal_versions,
    commercial.campaign_briefs
CASCADE;
COMMIT;
SELECT 'after.briefs=' || count(*) FROM commercial.campaign_briefs;
SELECT 'after.brief_versions=' || count(*) FROM commercial.brief_versions;
SELECT 'after.proposals=' || count(*) FROM commercial.proposal_versions;
SELECT 'after.tasks=' || count(*) FROM commercial.human_tasks;
'@
    $sql | & docker exec -i $container psql -U advertified -d advertified -v ON_ERROR_STOP=1 -q
    if ($LASTEXITCODE -ne 0) { throw 'Local commercial-data cleanup failed.' }
}
finally {
    Pop-Location
}

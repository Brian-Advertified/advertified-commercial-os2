param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'advertified-compose.ps1')
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
\set ON_ERROR_STOP on
\pset tuples_only on
\pset format unaligned
BEGIN;
CREATE TEMP TABLE canary_scope ON COMMIT DROP AS
SELECT b.tenant_id, b.id AS brief_id, v.id AS brief_version_id
FROM commercial.campaign_briefs b
LEFT JOIN commercial.brief_versions v
  ON v.tenant_id = b.tenant_id AND v.brief_id = b.id
WHERE b.title = 'Takealot Black Friday OOH Canary';

DO $$
DECLARE brief_count integer;
DECLARE artifact_count integer;
BEGIN
  SELECT count(DISTINCT brief_id) INTO brief_count FROM canary_scope;
  IF brief_count <> 1 THEN
    RAISE EXCEPTION 'Expected exactly one canary Brief, found %', brief_count;
  END IF;

  SELECT
      (SELECT count(*) FROM commercial.intelligence_artifacts a
       WHERE EXISTS (SELECT 1 FROM canary_scope s
                     WHERE a.tenant_id = s.tenant_id AND a.subject_id = s.brief_version_id))
    + (SELECT count(*) FROM commercial.media_mix_versions m
       WHERE EXISTS (SELECT 1 FROM canary_scope s
                     WHERE m.tenant_id = s.tenant_id AND m.brief_version_id = s.brief_version_id))
    + (SELECT count(*) FROM commercial.media_plan_versions p
       WHERE EXISTS (SELECT 1 FROM canary_scope s
                     WHERE p.tenant_id = s.tenant_id AND p.brief_version_id = s.brief_version_id))
    + (SELECT count(*) FROM commercial.inventory_shortlist_versions i
       WHERE EXISTS (SELECT 1 FROM canary_scope s
                     WHERE i.tenant_id = s.tenant_id AND i.brief_version_id = s.brief_version_id))
    + (SELECT count(*) FROM commercial.proposal_versions p
       WHERE EXISTS (SELECT 1 FROM canary_scope s
                     WHERE p.tenant_id = s.tenant_id AND p.brief_id = s.brief_id))
    + (SELECT count(*) FROM commercial.campaigns c
       WHERE EXISTS (SELECT 1 FROM canary_scope s
                     WHERE c.tenant_id = s.tenant_id AND c.brief_id = s.brief_id))
  INTO artifact_count;

  IF artifact_count <> 0 THEN
    RAISE EXCEPTION 'Canary has downstream commercial artifacts; refusing targeted cleanup (% rows)', artifact_count;
  END IF;
END $$;

-- Local fixture cleanup only. USER triggers contain immutable-record guards;
-- FK constraint triggers stay enabled, so orphaning is still rejected.
ALTER TABLE commercial.campaign_mode_selections DISABLE TRIGGER USER;
ALTER TABLE commercial.brief_spatial_requirements DISABLE TRIGGER USER;
ALTER TABLE commercial.brief_sources DISABLE TRIGGER USER;
ALTER TABLE commercial.brief_versions DISABLE TRIGGER USER;

DELETE FROM commercial.human_tasks h
WHERE EXISTS (
  SELECT 1 FROM canary_scope s
  WHERE h.tenant_id = s.tenant_id
    AND (
      h.resource_id = s.brief_id OR
      h.resource_id = s.brief_version_id OR
      h.resource_id IN (
        SELECT cms.id
        FROM commercial.campaign_mode_selections cms
        WHERE cms.tenant_id = s.tenant_id
          AND cms.brief_version_id = s.brief_version_id
      )
    )
);

DELETE FROM commercial.campaign_mode_selections cms
WHERE EXISTS (
  SELECT 1 FROM canary_scope s
  WHERE cms.tenant_id = s.tenant_id
    AND cms.brief_version_id = s.brief_version_id
);
DELETE FROM commercial.brief_spatial_requirements r
WHERE EXISTS (
  SELECT 1 FROM canary_scope s
  WHERE r.tenant_id = s.tenant_id
    AND r.brief_version_id = s.brief_version_id
);
DELETE FROM commercial.brief_version_evidence_items e
WHERE EXISTS (
  SELECT 1 FROM canary_scope s
  WHERE e.tenant_id = s.tenant_id
    AND e.brief_version_id = s.brief_version_id
);

UPDATE commercial.campaign_briefs b
SET current_draft_version_id = NULL,
    approved_version_id = NULL,
    ready_version_id = NULL
WHERE EXISTS (
  SELECT 1 FROM canary_scope s
  WHERE b.tenant_id = s.tenant_id AND b.id = s.brief_id
);

DELETE FROM commercial.brief_versions v
WHERE EXISTS (
  SELECT 1 FROM canary_scope s
  WHERE v.tenant_id = s.tenant_id AND v.id = s.brief_version_id
);
DELETE FROM commercial.brief_sources src
WHERE EXISTS (
  SELECT 1 FROM canary_scope s
  WHERE src.tenant_id = s.tenant_id AND src.brief_id = s.brief_id
);
DELETE FROM commercial.campaign_briefs b
WHERE EXISTS (
  SELECT 1 FROM canary_scope s
  WHERE b.tenant_id = s.tenant_id AND b.id = s.brief_id
);

ALTER TABLE commercial.campaign_mode_selections ENABLE TRIGGER USER;
ALTER TABLE commercial.brief_spatial_requirements ENABLE TRIGGER USER;
ALTER TABLE commercial.brief_sources ENABLE TRIGGER USER;
ALTER TABLE commercial.brief_versions ENABLE TRIGGER USER;
COMMIT;

SELECT 'briefs=' || count(*) FROM commercial.campaign_briefs;
SELECT 'canary=' || count(*) FROM commercial.campaign_briefs
WHERE title = 'Takealot Black Friday OOH Canary';
'@
    $sql | & docker exec -i $container psql -U advertified -d advertified -v ON_ERROR_STOP=1 -q
    if ($LASTEXITCODE -ne 0) { throw 'Targeted local canary cleanup failed.' }
}
finally {
    Pop-Location
}

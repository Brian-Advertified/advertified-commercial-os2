import { lazy, Suspense } from 'react'
import type { Shortlist, ShortlistCandidate } from '../api/planning-schemas'
import type { MapFeature } from '../components/map/MapboxMap'
import { masterDataCodes } from '../generated/master-data-codes'
import './planning-opportunity-map.css'

const MapboxMap = lazy(() => import('../components/map/MapboxMap')
  .then(module => ({ default: module.MapboxMap })))

export function PlanningOpportunityMap({ shortlist }: { shortlist: Shortlist | null }) {
  const features = shortlist ? shortlistMapFeatures(shortlist.candidates) : []
  if (features.length === 0) return null
  const eligible = shortlist!.candidates.filter(item => item.isEligible).length
  const selected = shortlist!.candidates.filter(item => item.isSelected === true).length
  return <section className="planning-opportunity-map" aria-labelledby="planning-map-title">
    <header><div><p className="eyebrow">Campaign opportunity map</p>
      <h2 id="planning-map-title">Where the shortlisted media can work</h2>
      <p>Spatial evidence from evaluated inventory is shown on the shared Advertified map. Use it to compare geographic fit before confirming supply.</p></div>
      <span className="status-chip">{eligible} eligible · {selected} selected</span></header>
    <Suspense fallback={<div className="planning-map-loading">Loading campaign geography…</div>}>
      <MapboxMap features={features} ariaLabel="Evaluated campaign inventory map" />
    </Suspense>
  </section>
}

function shortlistMapFeatures(candidates: ShortlistCandidate[]): MapFeature[] {
  const features: MapFeature[] = []
  const seenPois = new Set<string>()
  candidates.forEach((candidate, candidateIndex) => {
    const spatial = candidate.spatial
    const status = candidate.isSelected === true ? 'Selected' : candidate.isEligible ? 'Eligible' : 'Rejected'
    const properties = {
      verified: true,
      priority: candidate.isEligible
        ? masterDataCodes.spatialRequirementPriorities.preferred
        : masterDataCodes.spatialRequirementPriorities.excluded,
      state: status,
    }
    if (candidate.latitude !== null && candidate.longitude !== null) {
      features.push({
        id: `inventory-${candidate.id}-site`,
        label: `${status} · ${candidate.name}${candidate.supplierName ? ` · ${candidate.supplierName}` : ''}`,
        geometry: { type: 'Point', coordinates: [candidate.longitude, candidate.latitude] },
        properties,
      })
    }
    if (!spatial) return
    spatialGeometryEntries(candidate).forEach(([kind, value], geometryIndex) => {
      const geometry = parseGeometry(value)
      if (!geometry) return
      features.push({
        id: `inventory-${candidate.id}-${kind}-${geometryIndex}`,
        label: `${status} · ${candidate.name} · ${kind}`,
        geometry,
        properties,
      })
    })
    spatial.pointsOfInterest.forEach((poi, poiIndex) => {
      if (poi.latitude === null || poi.longitude === null) return
      const key = `${poi.name}:${poi.latitude}:${poi.longitude}`
      if (seenPois.has(key)) return
      seenPois.add(key)
      features.push({
        id: `inventory-poi-${candidateIndex}-${poiIndex}`,
        label: `POI · ${poi.name}`,
        geometry: { type: 'Point', coordinates: [poi.longitude, poi.latitude] },
        properties: { verified: true, state: 'Point of interest' },
      })
    })
  })
  return features
}

function spatialGeometryEntries(candidate: ShortlistCandidate): Array<[string, string | null]> {
  const spatial = candidate.spatial
  if (!spatial) return []
  return [
    ['coverage', spatial.coverageGeoJson],
    ['catchment', spatial.catchmentGeoJson],
    ['route', spatial.routeGeoJson],
    ['direction', spatial.directionGeoJson],
  ]
}

function parseGeometry(value: string | null): Record<string, unknown> | null {
  if (!value) return null
  try {
    const parsed = JSON.parse(value) as unknown
    if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) return null
    const record = parsed as Record<string, unknown>
    if (record.type === 'Feature' && record.geometry && typeof record.geometry === 'object') {
      return record.geometry as Record<string, unknown>
    }
    return record
  } catch {
    return null
  }
}

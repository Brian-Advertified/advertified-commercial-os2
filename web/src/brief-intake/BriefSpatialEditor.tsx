import type { CreateBriefVersion } from '../api/brief-client'
import { MapboxMap, type MapFeature } from '../components/map/MapboxMap'
import { parseGeometry } from '../components/map/geojson'
import { masterDataCodes } from '../generated/master-data-codes'
import { humanizeCode } from '../presentation/format'
import '../brief-intake-map.css'

export type BriefSpatialDraft = NonNullable<CreateBriefVersion['spatialRequirements']>[number]

export function BriefSpatialEditor({ values, onChange }: {
  values: BriefSpatialDraft[]
  onChange: (values: BriefSpatialDraft[]) => void
}) {
  return <section className="brief-spatial-editor">
    <header><div><p className="eyebrow">Spatial eligibility</p>
      <h2>Verified map requirements</h2></div>
      <button type="button" className="secondary-button"
        onClick={() => onChange([...values, emptyRequirement()])}>Add geography</button>
    </header>
    <p>Add exact geometry when location, route, catchment or exclusion boundaries are material.
      Route buffers left blank use the visible 500 metre governed default.</p>
    {values.length === 0
      ? <p className="review-empty-copy">No exact geometry has been supplied. Planning will retain the Brief's stated geography as text.</p>
      : <>
          <SpatialMapPreview values={values} />
          {values.map((value, index) => <SpatialRequirement key={index} value={value}
            update={next => onChange(values.map((item, itemIndex) => itemIndex === index ? next : item))}
            remove={() => onChange(values.filter((_, itemIndex) => itemIndex !== index))} />)}
        </>}
  </section>
}

function SpatialMapPreview({ values }: { values: BriefSpatialDraft[] }) {
  const features = values.flatMap((value, index) => mapFeatures(value, index))
  if (features.length === 0) {
    return <div className="brief-spatial-map-empty">Enter valid EPSG:4326 GeoJSON to preview this geography on the map.</div>
  }
  return <MapboxMap features={features} ariaLabel="Brief spatial requirements map" />
}

function mapFeatures(value: BriefSpatialDraft, index: number): MapFeature[] {
  const geometry = parseGeometry(value.geoJson)
  if (!geometry) return []
  const base = spatialFeature(value, geometry, index)
  const radius = radiusFeature(value, geometry, index)
  return radius ? [base, radius] : [base]
}

function spatialFeature(value: BriefSpatialDraft, geometry: Record<string, unknown>, index: number): MapFeature {
  return {
    id: `brief-spatial-${index}`,
    label: value.label || `Geography ${index + 1}`,
    geometry,
    properties: {
      priority: value.priority,
      type: value.type,
      verified: value.isVerified ?? false,
    },
  }
}

function radiusFeature(value: BriefSpatialDraft, geometry: Record<string, unknown>, index: number): MapFeature | null {
  if (value.type !== masterDataCodes.spatialRequirementTypes.pointRadius || !value.radiusMetres) return null
  const coordinates = pointCoordinates(geometry)
  if (!coordinates) return null
  return {
    id: `brief-spatial-radius-${index}`,
    label: value.label || `Geography ${index + 1}`,
    geometry: circlePolygon(coordinates, value.radiusMetres),
    properties: { type: 'radius', metres: value.radiusMetres },
  }
}

function pointCoordinates(geometry: Record<string, unknown>): [number, number] | null {
  if (geometry.type !== 'Point' || !Array.isArray(geometry.coordinates)) return null
  const [longitude, latitude] = geometry.coordinates
  return typeof longitude === 'number' && typeof latitude === 'number' ? [longitude, latitude] : null
}


function circlePolygon([longitude, latitude]: [number, number], radiusMetres: number) {
  const earthRadiusMetres = 6_371_008.8
  const angularDistance = radiusMetres / earthRadiusMetres
  const latitudeRadians = latitude * Math.PI / 180
  const longitudeRadians = longitude * Math.PI / 180
  const ring: [number, number][] = []
  for (let step = 0; step <= 64; step += 1) {
    const bearing = step / 64 * Math.PI * 2
    const lat = Math.asin(
      Math.sin(latitudeRadians) * Math.cos(angularDistance) +
      Math.cos(latitudeRadians) * Math.sin(angularDistance) * Math.cos(bearing),
    )
    const lng = longitudeRadians + Math.atan2(
      Math.sin(bearing) * Math.sin(angularDistance) * Math.cos(latitudeRadians),
      Math.cos(angularDistance) - Math.sin(latitudeRadians) * Math.sin(lat),
    )
    ring.push([lng * 180 / Math.PI, lat * 180 / Math.PI])
  }
  ring[ring.length - 1] = [...ring[0]]
  return { type: 'Polygon', coordinates: [ring] }
}

function SpatialRequirement({ value, update, remove }: {
  value: BriefSpatialDraft
  update: (value: BriefSpatialDraft) => void
  remove: () => void
}) {
  const point = value.type === masterDataCodes.spatialRequirementTypes.pointRadius
  const boundary = value.type === masterDataCodes.spatialRequirementTypes.adminBoundary
  return <fieldset className="brief-spatial-requirement"><legend>{value.label || 'New map requirement'}</legend>
    <label>Geometry type<select value={value.type} onChange={event => update({
      ...value, type: event.target.value,
      radiusMetres: event.target.value === masterDataCodes.spatialRequirementTypes.pointRadius
        ? value.radiusMetres ?? 1000 : null,
    })}>{Object.values(masterDataCodes.spatialRequirementTypes).map(code =>
      <option value={code} key={code}>{humanizeCode(code, true)}</option>)}</select></label>
    <label>Priority<select value={value.priority}
      onChange={event => update({ ...value, priority: event.target.value })}>
      {Object.values(masterDataCodes.spatialRequirementPriorities).map(code =>
        <option value={code} key={code}>{humanizeCode(code, true)}</option>)}</select></label>
    <label>Label<input value={value.label} maxLength={500} required
      onChange={event => update({ ...value, label: event.target.value })} /></label>
    {usesRadius(value.type) && <label>Radius / buffer in metres
      <input type="number" min="1" step="1" value={value.radiusMetres ?? ''}
        placeholder={routePlaceholder(value.type)}
        required={point}
        onChange={event => update({ ...value,
          radiusMetres: event.target.value ? Number(event.target.value) : null })} /></label>}
    <label>Minimum target coverage
      <input type="number" min="0.01" max="1" step="0.01"
        value={value.coverageThreshold ?? 0.5}
        onChange={event => update({ ...value,
          coverageThreshold: Number(event.target.value) })} /></label>
    {boundary && <><label>Boundary source<input value={value.boundarySource ?? ''} required
      onChange={event => update({ ...value, boundarySource: event.target.value })} /></label>
      <label>Boundary version<input value={value.boundaryVersion ?? ''} required
        onChange={event => update({ ...value, boundaryVersion: event.target.value })} /></label></>}
    <label className="spatial-geojson">EPSG:4326 GeoJSON<textarea value={value.geoJson}
      required rows={5} onChange={event => update({ ...value, geoJson: event.target.value })} /></label>
    <label><input type="checkbox" checked={value.isVerified ?? false}
      onChange={event => update({ ...value, isVerified: event.target.checked })} />
      I verified this geometry against the named source.</label>
    <button type="button" className="text-action" onClick={remove}>Remove geography</button>
  </fieldset>
}

function usesRadius(type: string) {
  return type === masterDataCodes.spatialRequirementTypes.pointRadius ||
    type === masterDataCodes.spatialRequirementTypes.routeBuffer
}

function routePlaceholder(type: string) {
  return type === masterDataCodes.spatialRequirementTypes.routeBuffer
    ? '500 metre default' : undefined
}

function emptyRequirement(): BriefSpatialDraft {
  return {
    type: masterDataCodes.spatialRequirementTypes.pointRadius,
    priority: masterDataCodes.spatialRequirementPriorities.required,
    label: '',
    geoJson: '',
    radiusMetres: 1000,
    coverageThreshold: 0.5,
    boundarySource: null,
    boundaryVersion: null,
    sourceLocator: 'supplied:web:spatial',
    isVerified: false,
  }
}

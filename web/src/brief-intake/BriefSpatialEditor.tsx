import type { CreateBriefVersion } from '../api/brief-client'
import { masterDataCodes } from '../generated/master-data-codes'
import { humanizeCode } from '../presentation/format'

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
      : values.map((value, index) => <SpatialRequirement key={index} value={value}
          update={next => onChange(values.map((item, itemIndex) => itemIndex === index ? next : item))}
          remove={() => onChange(values.filter((_, itemIndex) => itemIndex !== index))} />)}
  </section>
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

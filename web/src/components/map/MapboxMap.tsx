import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import {
  Map as MapboxGLMap,
  NavigationControl,
  type ExpressionSpecification,
  type GeoJSONSource,
  type GeoJSONSourceSpecification,
  type LayerSpecification,
} from 'mapbox-gl/esm'
import { masterDataCodes } from '../../generated/master-data-codes'
import 'mapbox-gl/dist/mapbox-gl.css'
import './mapbox-map.css'
import { validateGeometry } from './geojson'
import { mapContent as copy } from './map-content'

type Position = [number, number]
type GeoJsonObject = Record<string, unknown>
type MapGeoJsonData = Exclude<GeoJSONSourceSpecification['data'], string>
type MapStatus = 'loading' | 'ready' | 'token-missing' | 'failed'
type InspectFeature = { id: string; properties: { label: string; verified: boolean; state?: string | null } }
const spatialColor: ExpressionSpecification = ['case',
  ['!=', ['get', 'verified'], true], '#b7791f',
  ['==', ['get', 'state'], 'Selected'], '#16b364',
  ['==', ['get', 'state'], 'Eligible'], '#2089ff',
  ['==', ['get', 'state'], 'Rejected'], '#b42318',
  ['==', ['get', 'state'], 'Point of interest'], '#22bdd0',
  ['==', ['get', 'priority'], masterDataCodes.spatialRequirementPriorities.excluded], '#b42318',
  '#6038f5']

export type MapFeature = {
  id?: string
  label?: string
  geometry: GeoJsonObject
  properties?: Record<string, string | number | boolean | null>
}

export function MapboxMap({ features, ariaLabel = 'Campaign geography map' }: {
  features: MapFeature[]
  ariaLabel?: string
}) {
  const data = useMemo(() => featureCollection(features), [features])
  const token = mapboxToken()
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const items = data.features as InspectFeature[]
  const selected = items.find(feature => feature.id === selectedId)
  const { containerRef, status, focus } = useAdvertifiedMap(token, data, setSelectedId)
  return <section className="advertified-map" aria-label={ariaLabel}>
    <MapInspector items={items} selected={selected} focus={id => { setSelectedId(id); focus(id) }} />
    <div className="advertified-map-canvas" ref={containerRef} />
    {status === 'ready' && <MapLegend items={items} />}
    {status === 'loading' && <MapMessage>Loading campaign map…</MapMessage>}
    {status === 'token-missing' && <MapMessage>
      Map preview is unavailable. You can continue using the geography fields.
    </MapMessage>}
    {status === 'failed' && <MapMessage>
      The map could not be loaded. You can continue reviewing the geography fields.
    </MapMessage>}
    {data.features.length !== features.length && <MapMessage>
      Some geography cannot be previewed. Review its coordinates and shape in the geography fields.
    </MapMessage>}
  </section>
}

function MapInspector({ items, selected, focus }: {
  items: InspectFeature[]; selected?: InspectFeature; focus: (id: string | null) => void
}) {
  return <div className="advertified-map-inspector">
      <label>{copy.places}<select value={selected?.id ?? ''} onChange={event => {
        const id = event.target.value || null
        focus(id)
      }}><option value="">{copy.all}</option>{items.map(feature =>
        <option key={feature.id} value={feature.id}>{String(feature.properties?.label)}</option>)}</select></label>
      <button type="button" className="secondary-button" onClick={() => focus(null)}>{copy.all}</button>
      {selected && <p role="status"><strong>{copy.selected}: {String(selected.properties?.label)}</strong><br />
        {selected.properties?.verified ? copy.verified : copy.unverified}</p>}
      <p>{copy.note}</p>
    </div>
}

function useAdvertifiedMap(token: string, data: MapGeoJsonData, select: (id: string | null) => void) {
  const containerRef = useRef<HTMLDivElement | null>(null)
  const mapRef = useRef<MapboxGLMap | null>(null)
  const dataRef = useRef(data)
  const [status, setStatus] = useState<MapStatus>(token ? 'loading' : 'token-missing')

  useEffect(() => {
    dataRef.current = data
  }, [data])

  useEffect(() => {
    if (!token || !containerRef.current) return
    let active = true
    const updateStatus = (next: MapStatus) => { if (active) setStatus(next) }
    let map: MapboxGLMap
    try {
      map = createMap(containerRef.current, token, () => dataRef.current, updateStatus)
      map.on('click', 'advertified-spatial-point', event => {
        const feature = event.features?.[0] as { properties?: { featureId?: string } } | undefined
        if (feature?.properties?.featureId) select(String(feature.properties.featureId))
      })
    } catch {
      queueMicrotask(() => updateStatus('failed'))
      return () => { active = false }
    }
    mapRef.current = map
    return () => {
      active = false
      map.remove()
      mapRef.current = null
    }
  }, [token, select])

  useEffect(() => {
    const map = mapRef.current
    if (!map || status !== 'ready') return
    const source = map.getSource('advertified-spatial') as GeoJSONSource | undefined
    source?.setData(data)
    fitToData(map, data)
  }, [data, status])

  const focus = (id: string | null) => {
    const map = mapRef.current
    if (!map || status !== 'ready') return
    const collection = data as Extract<MapGeoJsonData, { type: 'FeatureCollection' }>
    fitToData(map, id ? { ...collection, features: collection.features.filter((feature: InspectFeature) => feature.id === id) } : data)
  }
  return { containerRef, status, focus }
}

function createMap(
  container: HTMLDivElement,
  token: string,
  getData: () => MapGeoJsonData,
  setStatus: (status: MapStatus) => void,
) {
  const map = new MapboxGLMap({
    accessToken: token,
    container,
    style: 'mapbox://styles/mapbox/light-v11',
    center: [24.5, -29],
    zoom: 4.2,
    attributionControl: true,
  })
  map.addControl(new NavigationControl({ showCompass: false }), 'top-right')
  map.on('load', () => {
    const data = getData()
    ensureSpatialLayers(map, data)
    fitToData(map, data)
    setStatus('ready')
  })
  map.on('error', () => {
    if (!map.loaded()) setStatus('failed')
  })
  return map
}

function MapMessage({ children }: { children: ReactNode }) {
  return <div className="advertified-map-message">{children}</div>
}

function MapLegend({ items }: { items: InspectFeature[] }) {
  const states = new Set(items.map(item => item.properties.state).filter(Boolean))
  if (states.has('Selected') || states.has('Eligible') || states.has('Rejected')) {
    return <div className="advertified-map-legend">
      {states.has('Selected') && <span>Green: selected media</span>}
      {states.has('Eligible') && <span>Blue: eligible media</span>}
      {states.has('Rejected') && <span>Red: rejected media</span>}
      {states.has('Point of interest') && <span>Cyan: point of interest</span>}
    </div>
  }
  return <div className="advertified-map-legend">
    <span>Amber: needs verification</span><span>Purple: verified area</span>
    <span>Red: verified exclusion</span>
  </div>
}

function featureCollection(features: MapFeature[]) {
  const budget = { remainingPoints: 20_000 }
  // A Brief admits 100 requirements; each point-radius contributes a point and an overlay.
  const validFeatures = features.slice(0, 200).flatMap((feature, index) => {
    const geometry = validateGeometry(feature.geometry, budget)
    return geometry ? [{
      type: 'Feature' as const,
      id: feature.id ?? `spatial-${index}`,
      properties: { label: feature.label ?? '', featureId: feature.id ?? `spatial-${index}`, verified: feature.properties?.verified === true,
        priority: feature.properties?.priority ?? null, state: feature.properties?.state ?? null },
      geometry,
    }] : []
  })
  return {
    type: 'FeatureCollection' as const,
    features: validFeatures,
  } as Extract<MapGeoJsonData, { type: 'FeatureCollection' }>
}

function ensureSpatialLayers(map: MapboxGLMap, data: MapGeoJsonData) {
  if (!map.getSource('advertified-spatial')) {
    map.addSource('advertified-spatial', { type: 'geojson', data })
  }
  addLayer(map, 'advertified-spatial-fill', {
    id: 'advertified-spatial-fill', type: 'fill', source: 'advertified-spatial',
    filter: ['==', ['geometry-type'], 'Polygon'],
    paint: { 'fill-color': spatialColor, 'fill-opacity': 0.14 },
  })
  addLayer(map, 'advertified-spatial-line', {
    id: 'advertified-spatial-line', type: 'line', source: 'advertified-spatial',
    filter: ['in', ['geometry-type'], ['literal', ['LineString', 'Polygon']]],
    paint: { 'line-color': spatialColor, 'line-width': 3 },
  })
  addLayer(map, 'advertified-spatial-point', {
    id: 'advertified-spatial-point', type: 'circle', source: 'advertified-spatial',
    filter: ['==', ['geometry-type'], 'Point'],
    paint: {
      'circle-radius': 7, 'circle-color': spatialColor,
      'circle-stroke-color': '#ffffff', 'circle-stroke-width': 2,
    },
  })
}

function addLayer(map: MapboxGLMap, id: string, layer: LayerSpecification) {
  if (!map.getLayer(id)) map.addLayer(layer)
}

function fitToData(map: MapboxGLMap, data: MapGeoJsonData) {
  const positions: Position[] = []
  collectPositions(data, positions)
  if (positions.length === 0) return
  const [southWest, northEast] = boundsFor(positions)
  map.fitBounds([southWest, northEast], { padding: 42, maxZoom: 14, duration: 350 })
}

function boundsFor(positions: Position[]): [Position, Position] {
  let [minLng, minLat] = positions[0]
  let [maxLng, maxLat] = positions[0]
  for (const [lng, lat] of positions.slice(1)) {
    minLng = Math.min(minLng, lng); maxLng = Math.max(maxLng, lng)
    minLat = Math.min(minLat, lat); maxLat = Math.max(maxLat, lat)
  }
  if (minLng === maxLng && minLat === maxLat) {
    minLng -= .08; maxLng += .08; minLat -= .06; maxLat += .06
  }
  return [[minLng, minLat], [maxLng, maxLat]]
}

function collectPositions(value: unknown, positions: Position[]) {
  const coordinate = coordinatePair(value)
  if (coordinate) {
    positions.push(coordinate)
    return
  }
  if (Array.isArray(value)) {
    value.forEach(item => collectPositions(item, positions))
    return
  }
  if (isRecord(value)) collectRecordPositions(value, positions)
}

function collectRecordPositions(record: Record<string, unknown>, positions: Position[]) {
  for (const key of ['coordinates', 'geometry', 'features']) {
    if (key in record) {
      collectPositions(record[key], positions)
      return
    }
  }
}

function coordinatePair(value: unknown): Position | null {
  if (!Array.isArray(value) || value.length < 2) return null
  const [longitude, latitude] = value
  if (typeof longitude !== 'number' || typeof latitude !== 'number') return null
  if (!Number.isFinite(longitude) || !Number.isFinite(latitude)) return null
  if (Math.abs(longitude) > 180 || Math.abs(latitude) > 90) return null
  return [longitude, latitude]
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value) && typeof value === 'object' && !Array.isArray(value)
}

function mapboxToken() {
  const environment = (import.meta as ImportMeta & { env?: Record<string, string | undefined> }).env
  const token = environment?.VITE_MAPBOX_PUBLIC_TOKEN?.trim() ?? ''
  return token.startsWith('pk.') ? token : ''
}

import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import mapboxgl, { type GeoJSONSource, type GeoJSONSourceSpecification } from 'mapbox-gl'
import 'mapbox-gl/dist/mapbox-gl.css'
import './mapbox-map.css'
import { validateGeometry } from './geojson'

type Position = [number, number]
type GeoJsonObject = Record<string, unknown>
type MapGeoJsonData = Exclude<GeoJSONSourceSpecification['data'], string>
type MapStatus = 'loading' | 'ready' | 'token-missing' | 'failed'

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
  const { containerRef, status } = useAdvertifiedMap(token, data)
  return <section className="advertified-map" aria-label={ariaLabel}>
    <div className="advertified-map-canvas" ref={containerRef} />
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

function useAdvertifiedMap(token: string, data: MapGeoJsonData) {
  const containerRef = useRef<HTMLDivElement | null>(null)
  const mapRef = useRef<mapboxgl.Map | null>(null)
  const dataRef = useRef(data)
  const [status, setStatus] = useState<MapStatus>(token ? 'loading' : 'token-missing')

  useEffect(() => {
    dataRef.current = data
  }, [data])

  useEffect(() => {
    if (!token || !containerRef.current) return
    let map: mapboxgl.Map
    try {
      map = createMap(containerRef.current, token, () => dataRef.current, setStatus)
    } catch {
      setStatus('failed')
      return
    }
    mapRef.current = map
    return () => {
      map.remove()
      mapRef.current = null
    }
  }, [token])

  useEffect(() => {
    const map = mapRef.current
    if (!map || status !== 'ready') return
    const source = map.getSource('advertified-spatial') as GeoJSONSource | undefined
    source?.setData(data)
    fitToData(map, data)
  }, [data, status])

  return { containerRef, status }
}

function createMap(
  container: HTMLDivElement,
  token: string,
  getData: () => MapGeoJsonData,
  setStatus: (status: MapStatus) => void,
) {
  const map = new mapboxgl.Map({
    accessToken: token,
    container,
    style: 'mapbox://styles/mapbox/light-v11',
    center: [24.5, -29],
    zoom: 4.2,
    attributionControl: true,
  })
  map.addControl(new mapboxgl.NavigationControl({ showCompass: false }), 'top-right')
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

function featureCollection(features: MapFeature[]) {
  const budget = { remainingPoints: 20_000 }
  const validFeatures = features.slice(0, 100).flatMap((feature, index) => {
    const geometry = validateGeometry(feature.geometry, budget)
    return geometry ? [{
      type: 'Feature' as const,
      id: feature.id ?? `spatial-${index}`,
      properties: { label: feature.label ?? '' },
      geometry,
    }] : []
  })
  return {
    type: 'FeatureCollection' as const,
    features: validFeatures,
  } as Extract<MapGeoJsonData, { type: 'FeatureCollection' }>
}

function ensureSpatialLayers(map: mapboxgl.Map, data: MapGeoJsonData) {
  if (!map.getSource('advertified-spatial')) {
    map.addSource('advertified-spatial', { type: 'geojson', data })
  }
  addLayer(map, 'advertified-spatial-fill', {
    id: 'advertified-spatial-fill', type: 'fill', source: 'advertified-spatial',
    filter: ['==', ['geometry-type'], 'Polygon'],
    paint: { 'fill-color': '#6038f5', 'fill-opacity': 0.14 },
  })
  addLayer(map, 'advertified-spatial-line', {
    id: 'advertified-spatial-line', type: 'line', source: 'advertified-spatial',
    filter: ['in', ['geometry-type'], ['literal', ['LineString', 'Polygon']]],
    paint: { 'line-color': '#6038f5', 'line-width': 3 },
  })
  addLayer(map, 'advertified-spatial-point', {
    id: 'advertified-spatial-point', type: 'circle', source: 'advertified-spatial',
    filter: ['==', ['geometry-type'], 'Point'],
    paint: {
      'circle-radius': 7, 'circle-color': '#6038f5',
      'circle-stroke-color': '#ffffff', 'circle-stroke-width': 2,
    },
  })
}

function addLayer(map: mapboxgl.Map, id: string, layer: mapboxgl.LayerSpecification) {
  if (!map.getLayer(id)) map.addLayer(layer)
}

function fitToData(map: mapboxgl.Map, data: MapGeoJsonData) {
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

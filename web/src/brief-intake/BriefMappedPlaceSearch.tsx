import { useRef, useState } from 'react'
import { discoverPlaces, type DiscoveredPlace } from '../api/place-client'
import { useWorkspace } from '../auth/workspace-state'
import { placeCopy } from '../content/place-copy'
import type { PlaceInput } from './place-requirement'

export function BriefMappedPlaceSearch({ onSelect }: { onSelect: (value: Partial<PlaceInput>) => void }) {
  const { selected } = useWorkspace()
  const [query, setQuery] = useState('')
  const [places, setPlaces] = useState<DiscoveredPlace[]>([])
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState('')
  const requestId = useRef(0)
  async function search() {
    if (!selected || query.trim().length < 2) return
    const current = ++requestId.current
    setBusy(true); setPlaces([]); setMessage('')
    try {
      const response = await discoverPlaces(selected.tenantId, query.trim())
      if (current !== requestId.current) return
      setPlaces(response.places)
      setMessage(!response.available ? placeCopy.providerUnavailable
        : response.places.length ? placeCopy.chooseMapped : placeCopy.noMappedResults)
    } catch { if (current === requestId.current) setMessage(placeCopy.searchError) }
    finally { if (current === requestId.current) setBusy(false) }
  }
  return <section>
    <label>{placeCopy.mappedSearch}<input maxLength={200} value={query}
      placeholder={placeCopy.mappedHint} onChange={event => {
        setQuery(event.target.value); requestId.current++; setBusy(false); setPlaces([]); setMessage('')
      }} /></label>
    <p>{placeCopy.mappedPrivacy}</p>
    <button type="button" className="secondary-button" disabled={busy || !selected || query.trim().length < 2}
      onClick={() => void search()}>{busy ? placeCopy.searching : placeCopy.searchMapped}</button>
    {message && <p role="status">{message}</p>}
    {places.length > 0 && <ul>{places.map(place => <li key={place.id}>
      <strong>{place.name}</strong><p>{place.address}</p><p>{place.geometryBasis}</p>
      <a href={place.sourceLocator} target="_blank" rel="noreferrer">{placeCopy.viewSource}</a>
      <span> · {place.attribution}</span>
      <button type="button" className="secondary-button" onClick={() => {
        onSelect({ name: `${place.name} — ${place.address}`, latitude: String(place.latitude),
          longitude: String(place.longitude),
          source: `${place.sourceLocator}; retrieved:${place.retrievedAtUtc}; ${place.attribution}; ${place.geometryBasis}` })
        setPlaces([]); setMessage(placeCopy.selected)
      }}>{placeCopy.usePlace} {place.name}</button>
    </li>)}</ul>}
    <p><a href="https://www.openstreetmap.org/copyright" target="_blank" rel="noreferrer">{placeCopy.attribution}</a>
      {' · '}<a href="https://operations.osmfoundation.org/policies/nominatim/" target="_blank" rel="noreferrer">{placeCopy.servicePolicy}</a></p>
  </section>
}

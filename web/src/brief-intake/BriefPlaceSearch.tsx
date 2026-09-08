import { useRef, useState } from 'react'
import { inventoryPlaceSource, searchInventoryPlaces, type InventoryPlace } from '../api/place-client'
import { useWorkspace } from '../auth/workspace-state'
import { placeCopy } from '../content/place-copy'
import type { PlaceInput } from './place-requirement'

export function BriefPlaceSearch({ onSelect }: { onSelect: (value: Partial<PlaceInput>) => void }) {
  const { selected } = useWorkspace()
  const [query, setQuery] = useState('')
  const [results, setResults] = useState<InventoryPlace[]>([])
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState('')
  const requestId = useRef(0)
  async function search() {
    if (!selected || query.trim().length < 2) return
    const current = ++requestId.current
    setBusy(true); setMessage(''); setResults([])
    try {
      const places = await searchInventoryPlaces(selected.tenantId, query.trim())
      if (requestId.current !== current) return
      setResults(places)
      setMessage(places.length ? placeCopy.choose : placeCopy.noResults)
    } catch { if (requestId.current === current) setMessage(placeCopy.searchError) }
    finally { if (requestId.current === current) setBusy(false) }
  }
  return <section aria-label={placeCopy.searchTitle}>
    <label>{placeCopy.searchTitle}<input value={query} maxLength={200}
      placeholder={placeCopy.nameHint} onChange={event => {
        setQuery(event.target.value); requestId.current++; setBusy(false); setResults([]); setMessage('')
      }} /></label>
    <button type="button" className="secondary-button" onClick={() => void search()}
      disabled={busy || query.trim().length < 2 || !selected}>{busy ? placeCopy.searching : placeCopy.search}</button>
    {message && <p role="status">{message}</p>}
    {results.length > 0 && <ul>{results.map(place => <li key={place.id}>
      <strong>{place.name}</strong> · {place.context} · {place.latitude}, {place.longitude}
      <p>{placeCopy.importedContext} {place.sourceLocator}</p>
      <button type="button" className="secondary-button" onClick={() => {
        onSelect({ name: place.name, latitude: String(place.latitude), longitude: String(place.longitude),
          source: inventoryPlaceSource(place) }); setResults([]); setMessage(placeCopy.selected)
      }}>{placeCopy.usePlace} {place.name}</button>
    </li>)}</ul>}
  </section>
}

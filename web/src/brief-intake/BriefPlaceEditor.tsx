import { useState } from 'react'
import { placeCopy } from '../content/place-copy'
import { placeRequirement, type PlaceInput } from './place-requirement'
import type { BriefSpatialDraft } from './BriefSpatialEditor'
import { BriefPlaceSearch } from './BriefPlaceSearch'
import { BriefMappedPlaceSearch } from './BriefMappedPlaceSearch'

const emptyPlace: PlaceInput = { name: '', latitude: '', longitude: '', radius: '', source: '' }

export function BriefPlaceEditor({ onAdd }: { onAdd: (value: BriefSpatialDraft) => void }) {
  const [input, setInput] = useState(emptyPlace)
  const [message, setMessage] = useState('')
  const update = (key: keyof PlaceInput, value: string) => {
    setInput(current => ({ ...current, [key]: value }))
    setMessage('')
  }
  function add() {
    const requirement = placeRequirement(input)
    if (!requirement) { setMessage(placeCopy.invalid); return }
    onAdd(requirement)
    setInput(emptyPlace)
    setMessage(placeCopy.added)
  }
  return <section className="brief-place-editor" aria-label={placeCopy.title}>
    <h3>{placeCopy.title}</h3><p>{placeCopy.introduction}</p>
    <p className="brief-place-note">{placeCopy.limitation}</p>
    <BriefMappedPlaceSearch onSelect={value => { setInput(current => ({ ...current, ...value })); setMessage('') }} />
    <BriefPlaceSearch onSelect={value => { setInput(current => ({ ...current, ...value })); setMessage('') }} />
    <div className="brief-place-fields">
      <label>{placeCopy.name}<input value={input.name} maxLength={500}
        placeholder={placeCopy.nameHint} onChange={event => update('name', event.target.value)} /></label>
      <label>{placeCopy.source}<input value={input.source} maxLength={1000}
        placeholder={placeCopy.sourceHint} onChange={event => update('source', event.target.value)} /></label>
      <label>{placeCopy.latitude}<input type="number" min={-90} max={90} step="any"
        value={input.latitude} onChange={event => update('latitude', event.target.value)} /></label>
      <label>{placeCopy.longitude}<input type="number" min={-180} max={180} step="any"
        value={input.longitude} onChange={event => update('longitude', event.target.value)} /></label>
      <label>{placeCopy.radius}<input type="number" min={1} step="any"
        value={input.radius} onChange={event => update('radius', event.target.value)} /></label>
    </div>
    <button className="secondary-button" type="button" onClick={add}>{placeCopy.add}</button>
    {message && <p role="status">{message}</p>}
  </section>
}

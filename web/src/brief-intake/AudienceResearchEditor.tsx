import { useState, type FormEvent } from 'react'
import { audienceResearchSchema, type AudienceResearch } from '../api/audience-research-schema'
import { AudienceSourceFields } from '../components/AudienceSourceFields'
import { audienceSourceValues } from '../components/audience-source-values'

export function AudienceResearchEditor({ audiences, values, onChange }: {
  audiences: string[]; values: AudienceResearch[]; onChange: (values: AudienceResearch[]) => void
}) {
  const [error, setError] = useState<string | null>(null)
  function add(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = event.currentTarget
    const input = new FormData(form)
    const result = audienceResearchSchema.safeParse({ ...audienceSourceValues(input),
      ...Object.fromEntries(['sourceLocator', 'sourceExcerpt', 'measurementPeriod', 'methodology']
        .map(key => [key, String(input.get(key) ?? '').trim()])) })
    if (!result.success || !audiences.includes(result.data.audienceName)) {
      setError('Complete the source, excerpt, period and method, and use an audience name from this Brief. Segmentation needs its taxonomy and version.')
      return
    }
    onChange([...values, result.data]); setError(null); form.reset()
  }
  return <section className="brief-workspace-panel" aria-label="Audience research">
    <h2>Audience research</h2><p>Add research you are authorised to use. These supplied claims will be retained with this exact Brief and submitted for human approval.</p>
    <p>Audience names: {audiences.join(' · ')}</p>
    <ul>{values.map((value, index) => <li key={`${value.sourceLocator}-${index}`}>
      <strong>{value.audienceName}</strong> · {value.sourceLocator} · {value.measurementPeriod}
      <p>{value.sourceExcerpt}</p><button type="button" className="secondary-button"
        onClick={() => onChange(values.filter((_, current) => current !== index))}>Remove research record</button></li>)}</ul>
    {values.length < 20 && <form onSubmit={add}>
      <label className="field-group">Research source reference<input name="sourceLocator" required maxLength={2000} /></label>
      <label className="field-group">Supporting research excerpt<textarea name="sourceExcerpt" required maxLength={4000} /></label>
      <label className="field-group">Research measurement period<input name="measurementPeriod" required maxLength={200} /></label>
      <label className="field-group">Research method and limitations<textarea name="methodology" required maxLength={1000} /></label>
      <AudienceSourceFields />
      {error && <p role="alert">{error}</p>}
      <button className="secondary-button">Add research for review</button>
    </form>}
  </section>
}

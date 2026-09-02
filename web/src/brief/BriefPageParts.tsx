import type { CSSProperties } from 'react'
import type { BriefVersion } from '../api/schemas'

export function BriefSummary({ version, budget, completeness, attention }: {
  version: BriefVersion
  budget: string
  completeness: number
  attention: number
}) {
  return <aside className="approved-brief-summary">
    <article><header><h3>Brief Summary</h3></header><dl>
      <div><dt>Objective</dt><dd>{version.objective || 'Not supplied'}</dd></div>
      <div><dt>Audience</dt><dd>{version.audiences[0] ?? 'Not supplied'}</dd></div>
      <div><dt>Geography</dt><dd>{version.geographies[0] ?? 'Not supplied'}</dd></div>
      <div><dt>Timing</dt><dd>{version.timing || 'Not supplied'}</dd></div>
      <div><dt>Budget</dt><dd>{budget}</dd></div>
      <div><dt>Primary KPI</dt><dd>{version.measurement[0] ?? 'Not supplied'}</dd></div>
    </dl></article>
    <article className="approved-completeness">
      <div className="approved-completeness-ring"
        style={{ '--value': `${completeness}%` } as CSSProperties}>
        <span>{completeness}%</span>
      </div>
      <div><h3>Completeness</h3><p>{attention === 0
        ? 'Every section is complete.'
        : `${attention} section${attention === 1 ? '' : 's'} need attention.`}</p>
        <a href="#brief-review">View review →</a></div>
    </article>
    <article className="approved-ai-suggestions">
      <header><h3>Assumptions carried forward</h3></header><ul>
        {version.assumptions.slice(0, 3).map((item, index) =>
          <li key={`${item.fieldPath}-${index}`}>! {item.value}</li>)}
        {version.assumptions.length === 0 &&
          <li>✓ No unsupported assumptions added.</li>}
      </ul>
    </article>
  </aside>
}

export function Field({ label, value, wide = false }: {
  label: string
  value: string
  wide?: boolean
}) {
  return <label className={wide ? 'wide' : undefined}>
    {label}<input readOnly value={value} />
  </label>
}

export function ListFields({ label, values }: {
  label: string
  values: string[]
}) {
  if (values.length === 0) return <Field label={label} value="Not supplied" wide />
  return <>{values.map((value, index) =>
    <Field key={`${label}-${index}`} label={`${label} ${index + 1}`}
      value={value} wide />)}</>
}

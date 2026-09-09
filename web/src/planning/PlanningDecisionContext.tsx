import type { PlanningWorkspace } from '../api/planning-schemas'
import { formatMoney, humanizeCode } from '../presentation/format'
import './planning-decision-context.css'

type DecisionContext = NonNullable<PlanningWorkspace['decisionContext']>

export function PlanningDecisionContext({ value }: { value: DecisionContext }) {
  return <section className="planning-decision-context" aria-labelledby="commercial-chain-title">
    <header><p className="eyebrow">Commercial reasoning chain</p>
      <h2 id="commercial-chain-title">Why this plan should exist</h2>
      <p>Approved Brief facts flow into audience strategy, media jobs and the eventual buy decision.</p></header>
    <ol>
      <DecisionStep number="1" label="Business problem" text={value.businessProblem} />
      <DecisionStep number="2" label="Objective" text={value.objective} />
      <DecisionList number="3" label="Success measures" values={value.successMeasures}
        empty="No explicit success measure is retained in the approved Brief." />
      <DecisionStep number="4" label="Audience strategy"
        text={value.targetingRationale || 'Audience strategy is not approved yet.'}
        detail={value.positioningStatement || undefined} />
      <MediaJobs value={value} />
    </ol>
    {value.evidenceGaps.length > 0 && <p className="planning-decision-gaps">
      <strong>Still to establish:</strong> {value.evidenceGaps.map(humanizeGap).join(' · ')}</p>}
  </section>
}

function DecisionStep({ number, label, text, detail }: {
  number: string; label: string; text: string; detail?: string
}) {
  return <li><span>{number}</span><div><strong>{label}</strong><p>{text || 'Not established'}</p>
    {detail && <small>{detail}</small>}</div></li>
}

function DecisionList({ number, label, values, empty }: {
  number: string; label: string; values: string[]; empty: string
}) {
  return <li><span>{number}</span><div><strong>{label}</strong>
    {values.length === 0 ? <p>{empty}</p> : <ul>{values.map(value => <li key={value}>{value}</li>)}</ul>}
  </div></li>
}

function MediaJobs({ value }: { value: DecisionContext }) {
  return <li><span>5</span><div><strong>Media jobs</strong>
    {value.mediaJobs.length === 0 ? <p>Media roles will appear after the approved allocation exists.</p> :
      <ul>{value.mediaJobs.map(job => <li key={job.channel}>
        <b>{humanizeCode(job.channel, true)}</b>: {job.role || 'Role not established'} · {' '}
        {formatMoney(job.budgetMinor, job.currency, 0)}
      </li>)}</ul>}
  </div></li>
}

function humanizeGap(value: string) {
  const key = value.split('.').at(-1) ?? value
  return key.replace(/([a-z])([A-Z])/g, '$1 $2').toLowerCase()
}

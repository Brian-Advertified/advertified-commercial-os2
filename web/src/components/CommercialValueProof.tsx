import type { ReactNode } from 'react'
import { Icon, type IconName } from './Icon'
import './commercial-value-proof.css'

export type CommercialProofMetric = {
  label: string
  value: ReactNode
  detail: ReactNode
  icon: IconName
  tone?: 'neutral' | 'positive' | 'warning' | 'violet' | 'blue'
  why?: ReactNode
}

export function CommercialValueProof({
  title,
  description,
  metrics,
  note,
}: {
  title: string
  description: ReactNode
  metrics: CommercialProofMetric[]
  note?: ReactNode
}) {
  if (metrics.length === 0) return null
  return <section className="commercial-value-proof" aria-label={title}>
    <header>
      <span className="commercial-value-mark" aria-hidden="true">✦</span>
      <div><p className="eyebrow">Advertified value</p><h2>{title}</h2><p>{description}</p></div>
    </header>
    <div className="commercial-value-grid">{metrics.map(metric =>
      <article key={metric.label} className={`commercial-value-metric tone-${metric.tone ?? 'neutral'}`}>
        <span className="commercial-value-icon"><Icon name={metric.icon} /></span>
        <div><small>{metric.label}</small><strong>{metric.value}</strong><p>{metric.detail}</p>
          {metric.why && <details><summary>Why this?</summary><div>{metric.why}</div></details>}
        </div>
      </article>)}</div>
    {note && <footer>{note}</footer>}
  </section>
}

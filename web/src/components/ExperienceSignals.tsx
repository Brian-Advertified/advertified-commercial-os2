import type { ReactNode } from 'react'
import { Icon, type IconName } from './Icon'
import './experience-signals.css'

export type ExperienceSignal = {
  label: string
  value: ReactNode
  detail: ReactNode
  icon: IconName
  tone?: 'neutral' | 'positive' | 'warning' | 'violet' | 'blue'
  why?: ReactNode
}

export function ExperienceSignals({ title = 'What Advertified sees', signals }: {
  title?: string
  signals: ExperienceSignal[]
}) {
  if (signals.length === 0) return null
  return <section className="experience-signals" aria-label={title}>
    <header><span className="experience-signals-spark">✦</span><div>
      <p className="eyebrow">Live campaign intelligence</p><h2>{title}</h2>
    </div></header>
    <div className="experience-signals-grid">{signals.map((signal) =>
      <article key={signal.label} className={`experience-signal tone-${signal.tone ?? 'neutral'}`}>
        <span className="experience-signal-icon"><Icon name={signal.icon} /></span>
        <div><small>{signal.label}</small><strong>{signal.value}</strong><p>{signal.detail}</p>
          {signal.why && <details><summary>Why this?</summary><div>{signal.why}</div></details>}
        </div>
      </article>)}</div>
  </section>
}

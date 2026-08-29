import type { MediaAllocation } from '../api/planning-schemas'
import { MediaTypeIcon } from '../components/MediaTypeIcon'
import { mediaVisual } from './media-visuals'

export function MediaTimeline({ allocations }: { allocations: MediaAllocation[] }) {
  const periods = allocations.flatMap(item => item.runningPeriods)
  if (periods.length === 0) return null
  const start = Math.min(...periods.map(item => dateValue(item.start)))
  const end = Math.max(...periods.map(item => dateValue(item.end)))
  const span = Math.max(1, end - start)
  return <section className="planning-section media-timeline" aria-labelledby="timeline-title">
    <div className="planning-section-heading"><div><p className="eyebrow">Running periods</p>
      <h2 id="timeline-title">See when each media type is live</h2>
      <p>Each channel keeps its own schedule; separate bursts remain separate on the plan.</p></div>
      <div className="timeline-range"><span>{shortDate(start)}</span><span>{shortDate(end)}</span></div></div>
    <div className="timeline-grid">{allocations.map(allocation => {
      const visual = mediaVisual(allocation.channel)
      return <div className="timeline-row" key={allocation.channel}>
        <div className={`media-identity media-tone-${visual.tone}`}>
          <MediaTypeIcon channel={allocation.channel} /><span>{visual.label}</span></div>
        <div className="timeline-track">{allocation.runningPeriods.map((period, index) => {
          const periodStart = dateValue(period.start)
          const periodEnd = dateValue(period.end)
          const left = (periodStart - start) / span * 100
          const width = Math.max(1.5, (periodEnd - periodStart + day) / (span + day) * 100)
          return <span key={`${period.start}-${period.end}-${index}`}
            className={`timeline-segment media-tone-${visual.tone}`}
            style={{ left: `${left}%`, width: `${width}%` }}
            title={`${visual.label}: ${period.start} to ${period.end}`} />
        })}</div>
      </div>
    })}</div>
  </section>
}

const day = 86_400_000

function dateValue(value: string) {
  return new Date(`${value}T00:00:00Z`).getTime()
}

function shortDate(value: number) {
  return new Intl.DateTimeFormat('en-ZA', { day: 'numeric', month: 'short', year: 'numeric' })
    .format(new Date(value))
}

import type { ReactNode } from 'react'
import type { BriefSectionId, BriefSectionState } from './brief-section-flow-state'

export function BriefLocalNavigation({
  sections,
  activeId,
  onSelect,
}: {
  sections: BriefSectionState[]
  activeId: BriefSectionId
  onSelect: (id: BriefSectionId) => void
}) {
  return <aside className="approved-brief-localnav" aria-label="Brief sections">
    {sections.map((section, index) => <a key={section.id} href={`#brief-${section.id}`}
      className={[
        section.id === activeId ? 'is-active' : '',
        section.status === 'complete' ? 'is-complete' : 'needs-attention',
      ].filter(Boolean).join(' ')}
      aria-current={section.id === activeId ? 'step' : undefined}
      onClick={(event) => { event.preventDefault(); onSelect(section.id) }}>
      <span className="approved-brief-step-number">{index + 1}</span>
      <span className="approved-brief-step-copy">{section.label}
        <small>{section.status === 'complete' ? 'Complete' : 'Needs attention'}</small></span>
      <span className="approved-brief-step-state" aria-hidden="true">
        {section.status === 'complete' ? '✓' : '!'}
      </span>
    </a>)}
  </aside>
}

export function BriefStep({
  section,
  active,
  previous,
  next,
  onSelect,
  copy,
  children,
  finalAction,
}: {
  section: BriefSectionState
  active: boolean
  previous: BriefSectionState | null
  next: BriefSectionState | null
  onSelect: (id: BriefSectionId) => void
  copy: string
  children: ReactNode
  finalAction?: ReactNode
}) {
  return <section className="approved-brief-section" id={`brief-${section.id}`}
    hidden={!active} tabIndex={-1} aria-labelledby={`brief-${section.id}-title`}>
    <header><div><h2 id={`brief-${section.id}-title`}>{section.label}</h2>
      <p>{copy}</p></div>
      <span className={section.status === 'complete'
        ? 'approved-section-state is-complete'
        : 'approved-section-state needs-attention'}>
        {section.status === 'complete' ? '✓ Complete' : '! Needs attention'}
      </span>
    </header>
    <div className="approved-brief-formgrid">{children}</div>
    <footer className="approved-brief-step-actions">
      <div>{previous && <button className="secondary-button" type="button"
        onClick={() => onSelect(previous.id)}>← Previous: {previous.label}</button>}</div>
      <div>{next
        ? <button className="primary-button" type="button"
            onClick={() => onSelect(next.id)}>Continue to {next.label} →</button>
        : finalAction}</div>
    </footer>
  </section>
}

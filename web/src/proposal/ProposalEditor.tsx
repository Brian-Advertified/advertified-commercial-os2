import { useState } from 'react'
import type { Proposal, ProposalOption, ProposalUpdateInput } from '../api/proposal-schemas'
import { proposalUpdateInputSchema } from '../api/proposal-schemas'
import { formatMoney } from '../presentation/format'

export function ProposalEditor({ proposal, busy, onSave }: {
  proposal: Proposal
  busy: boolean
  onSave: (input: ProposalUpdateInput) => Promise<void>
}) {
  const [core, setCore] = useState({
    title: proposal.title,
    summary: proposal.executiveSummary,
    terms: proposal.terms,
    expiry: proposal.expiryAtUtc.slice(0, 10),
  })
  const [options, setOptions] = useState(proposal.options)
  const [error, setError] = useState<string | null>(null)
  function updateOption(id: string, patch: Partial<Pick<ProposalOption, 'label' | 'outcome'>>) {
    setOptions(current => current.map(item => item.id === id ? { ...item, ...patch } : item))
  }
  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const parsed = proposalUpdateInputSchema.safeParse(toInput(core, options))
    if (!parsed.success) {
      setError(parsed.error.issues[0]?.message ?? 'Review the proposal wording and try again.')
      return
    }
    setError(null)
    try { await onSave(parsed.data) }
    catch (failure) { setError(failure instanceof Error ? failure.message : 'The proposal could not be saved.') }
  }
  return <form className="proposal-editor" onSubmit={submit}>
    <EditorHeading busy={busy} />
    {error && <p className="inline-alert" role="alert">{error}</p>}
    <CoreFields core={core} onChange={patch => setCore(current => ({ ...current, ...patch }))} />
    <div className="proposal-editor-options">{options.map((option, index) =>
      <OptionEditor key={option.id} option={option} index={index}
        onChange={patch => updateOption(option.id, patch)} />)}</div>
  </form>
}

type CoreDraft = { title: string; summary: string; terms: string; expiry: string }

function EditorHeading({ busy }: { busy: boolean }) {
  return <div className="proposal-section-heading"><div><p className="eyebrow">Review client wording</p>
    <h2>Make the proposal clear before approval</h2>
    <p>Plan budgets, placements and running periods stay locked to their approved versions.</p></div>
    <button className="secondary-button" type="submit" disabled={busy}>
      {busy ? 'Saving…' : 'Save wording'}
    </button></div>
}

function CoreFields({ core, onChange }: {
  core: CoreDraft
  onChange: (patch: Partial<CoreDraft>) => void
}) {
  return <div className="proposal-editor-grid">
    <label className="field-group field-wide">Proposal title
      <input value={core.title} required maxLength={300}
        onChange={event => onChange({ title: event.target.value })} /></label>
    <label className="field-group field-wide">Executive summary
      <textarea value={core.summary} required maxLength={5000}
        onChange={event => onChange({ summary: event.target.value })} /></label>
    <label className="field-group">Valid until
      <input type="date" value={core.expiry} required
        onChange={event => onChange({ expiry: event.target.value })} /></label>
    <label className="field-group field-wide">Commercial terms
      <textarea value={core.terms} required maxLength={10000}
        onChange={event => onChange({ terms: event.target.value })} /></label>
  </div>
}

function OptionEditor({ option, index, onChange }: {
  option: ProposalOption
  index: number
  onChange: (patch: Partial<Pick<ProposalOption, 'label' | 'outcome'>>) => void
}) {
  return <article><span className="proposal-option-number">Option {index + 1}</span>
    <label className="field-group">Choice name<input value={option.label} required maxLength={200}
      onChange={event => onChange({ label: event.target.value })} /></label>
    <label className="field-group">Client outcome<textarea value={option.outcome} required maxLength={2000}
      onChange={event => onChange({ outcome: event.target.value })} /></label>
    <div className="proposal-locked-total"><span>Approved plan total</span>
      <strong>{formatMoney(option.budgetMinor, option.currency)}</strong></div>
  </article>
}

function toInput(core: CoreDraft, options: ProposalOption[]): ProposalUpdateInput {
  return {
    title: core.title,
    executiveSummary: core.summary,
    terms: core.terms,
    expiryAtUtc: new Date(`${core.expiry}T23:59:59`).toISOString(),
    options: options.map(item => ({ id: item.id, label: item.label, outcome: item.outcome })),
  }
}

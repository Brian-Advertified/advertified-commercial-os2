import type { FormEvent } from 'react'
import { Icon } from '../components/Icon'

export function BriefSourceForm({ busy, source, onSubmit }: {
  busy: boolean
  source: { title: string; content: string }
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
}) {
  return <form className="brief-source-panel" onSubmit={onSubmit}>
    <header className="brief-source-heading">
      <span className="brief-source-icon"><Icon name="brief" /></span>
      <div><p className="eyebrow">Business problem or original Brief</p>
        <h2>Tell Advertified what the client needs to accomplish</h2>
        <p>Paste the email, WhatsApp message, tender extract, written Brief or raw campaign requirement. Advertified will preserve it as supplied and turn it into a structured commercial decision.</p></div>
    </header>
    <label className="field-group">Campaign or Brief name
      <input name="sourceTitle" required maxLength={300}
        defaultValue={source.title}
        placeholder="For example: Spring furniture sales campaign" />
    </label>
    <label className="field-group">Client requirement
      <textarea name="sourceContent" required rows={14} maxLength={262144}
        defaultValue={source.content}
        placeholder="Paste the original request or describe the campaign requirement here. Include the business problem, desired outcome, audiences, locations, dates, budget and media constraints when they are available." />
    </label>
    <div className="brief-source-actions">
      <span>Advertified will identify the business problem, client outcome, audience, geography, timing, budget and media scope.</span>
      <button className="primary-button" type="submit" disabled={busy}>
        {busy ? 'Understanding the requirement…' : 'Understand this campaign'}
        {!busy && <Icon name="arrow" />}
      </button>
    </div>
  </form>
}

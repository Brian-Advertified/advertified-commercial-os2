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
      <div><p className="eyebrow">Original client request</p>
        <h2>Start with what the client actually supplied</h2>
        <p>Paste the email, WhatsApp message, tender extract or written Brief. Advertified will structure it without replacing the original source.</p></div>
    </header>
    <label className="field-group">Campaign or Brief name
      <input name="sourceTitle" required maxLength={300}
        defaultValue={source.title}
        placeholder="For example: Spring furniture sales campaign" />
    </label>
    <label className="field-group">Original Brief
      <textarea name="sourceContent" required rows={14} maxLength={262144}
        defaultValue={source.content}
        placeholder="Paste the original client request here. Include objectives, audiences, locations, dates, budget, tax treatment and media preferences when they are available." />
    </label>
    <div className="brief-source-actions">
      <span>Advertified will identify the client, objective, audience, geography, timing, budget and media scope.</span>
      <button className="primary-button" type="submit" disabled={busy}>
        {busy ? 'Understanding the Brief…' : 'Understand this Brief'}
        {!busy && <Icon name="arrow" />}
      </button>
    </div>
  </form>
}

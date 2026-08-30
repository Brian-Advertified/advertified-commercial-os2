import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import type {
  EmailAutomationClarification,
  InboundEmailDetail,
} from '../api/email-automation-schemas'
import { masterDataCodes } from '../generated/master-data-codes'
import {
  automationCheckpoints,
  automationFailureLabel,
  automationStatusLabel,
  checkpointIndex,
} from './email-automation-presentation'

export function InboxMessageDetail({ detail, busy, onRetry }: {
  detail: InboundEmailDetail
  busy: boolean
  onRetry: (clarifications: EmailAutomationClarification[]) => Promise<void>
}) {
  const { email, run } = detail
  const needsAction = run.status === masterDataCodes.emailAutomationStatuses.reviewRequired ||
    run.status === masterDataCodes.emailAutomationStatuses.failed

  return <article className="ooh-message-detail">
    <header className="ooh-detail-heading"><div><p className="eyebrow">Proposal request</p>
      <h2>{email.subject}</h2><p>From {email.senderName ?? email.senderEmail} · Replying to {email.replyToEmail}</p></div>
      <span className={detailStatusClass(needsAction)}>
        {automationStatusLabel(run.status)}
      </span></header>

    <div className="ooh-mode-lock"><div><strong>OOH-only campaign</strong>
      <span>The planning flow is the same as every campaign. This Brief is locked to OOH and DOOH media only.</span></div>
      <span>Locked</span></div>

    <CheckpointProgress checkpoint={run.checkpoint} />
    <ReviewState detail={detail} needsAction={needsAction} busy={busy} onRetry={onRetry} />
    <ClarificationState detail={detail} busy={busy} onRetry={onRetry} />
    <DeliveredState status={run.status} />

    <SourceEmail detail={detail} />
    <ResultLinks detail={detail} />
  </article>
}

function CheckpointProgress({ checkpoint }: { checkpoint: string }) {
  const currentIndex = checkpointIndex(checkpoint)
  return <section className="ooh-progress-section" aria-labelledby="automation-progress-title">
    <div><p className="eyebrow">Automatic workflow</p>
      <h3 id="automation-progress-title">From email to proposal</h3></div>
    <div className="ooh-checkpoint-grid">{automationCheckpoints.map(([code, label], index) =>
      <div key={code} className={index <= currentIndex ? 'is-complete' : ''}>
        <span>{index <= currentIndex ? '✓' : index + 1}</span><strong>{label}</strong>
      </div>)}</div>
  </section>
}

function ReviewState({ detail, needsAction, busy, onRetry }: {
  detail: InboundEmailDetail
  needsAction: boolean
  busy: boolean
  onRetry: (clarifications: EmailAutomationClarification[]) => Promise<void>
}) {
  if (!needsAction) return null
  const nonOoh = detail.run.failureCode ===
    masterDataCodes.automationFailureReasons.nonOohRequest
  const retryable = detail.questions.length === 0 &&
    detail.run.status === masterDataCodes.emailAutomationStatuses.failed
  return <section className="ooh-review-card" role="status">
    <div><p className="eyebrow">Why it stopped</p><h3>Nothing was sent</h3>
      <p>{automationFailureLabel(detail.run.failureCode, detail.run.failureMessage)}</p></div>
    {nonOoh && <Link className="secondary-button" to="/briefs/new">
      Start a new full campaign</Link>}
    {retryable && <button className="secondary-button" type="button" disabled={busy}
      onClick={() => void onRetry([])}>{busy ? 'Checking again…' : 'Retry request'}</button>}
  </section>
}

function ClarificationState({ detail, busy, onRetry }: {
  detail: InboundEmailDetail
  busy: boolean
  onRetry: (clarifications: EmailAutomationClarification[]) => Promise<void>
}) {
  if (detail.questions.length === 0) return null
  return <ClarificationForm questions={detail.questions} busy={busy} onRetry={onRetry} />
}

function DeliveredState({ status }: { status: string }) {
  if (status !== masterDataCodes.emailAutomationStatuses.sent) return null
  return <section className="ooh-sent-card"><div className="ooh-sent-mark">✓</div><div>
    <p className="eyebrow">Delivered</p><h3>The proposal was sent automatically</h3>
    <p>The approved PDF was replied to the verified address. Duplicate provider events cannot send it again.</p>
  </div></section>
}

function ResultLinks({ detail }: { detail: InboundEmailDetail }) {
  const run = detail.run
  return <div className="ooh-result-links">
    {run.briefId && <Link className="text-action" to={`/briefs/${run.briefId}`}>Open approved Brief</Link>}
    {run.briefVersionId && <Link className="text-action" to={`/planning/${run.briefVersionId}`}>Open STP and media plan</Link>}
    {run.proposalVersionId && <Link className="primary-button" to={`/proposals/${run.proposalVersionId}`}>Open proposal</Link>}
  </div>
}

function detailStatusClass(needsAction: boolean) {
  return `ooh-detail-status ${needsAction ? 'needs-review' : ''}`
}

function ClarificationForm({ questions, busy, onRetry }: {
  questions: InboundEmailDetail['questions']
  busy: boolean
  onRetry: (clarifications: EmailAutomationClarification[]) => Promise<void>
}) {
  const [error, setError] = useState<string | null>(null)
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const values = new FormData(event.currentTarget)
    const answers = questions.map(question => ({
      fieldPath: question.fieldPath,
      value: String(values.get(question.fieldPath) ?? '').trim(),
    }))
    if (answers.some(answer => answer.value.length === 0)) {
      setError('Complete the requested detail before continuing.')
      return
    }
    setError(null)
    await onRetry(answers)
  }
  return <form className="ooh-clarification-card" onSubmit={(event) => void submit(event)}>
    <div><p className="eyebrow">Only unclear details</p>
      <h3>Confirm what the Brief did not establish</h3>
      <p>Everything else remains exactly as supplied in the original email.</p></div>
    <div className="ooh-clarification-grid">{questions.map(question =>
      <label className="field-group" key={question.fieldPath}>{question.question}
        {question.options.length > 0
          ? <select name={question.fieldPath} required defaultValue="">
              <option value="" disabled>Choose one</option>
              {question.options.map(option => <option key={option} value={option}>
                {campaignModeLabel(option)}
              </option>)}
            </select>
          : <input name={question.fieldPath} required maxLength={4000} />}
      </label>)}</div>
    {error && <p className="inline-alert" role="alert">{error}</p>}
    <div className="ooh-clarification-actions"><button className="primary-button"
      type="submit" disabled={busy}>{busy ? 'Applying answer…' : 'Continue automatically'}</button></div>
  </form>
}

function SourceEmail({ detail }: { detail: InboundEmailDetail }) {
  return <details className="ooh-source-email">
    <summary>View the original email Brief</summary>
    <pre>{detail.sourceContent}</pre>
    {detail.email.attachments.length > 0 && <div className="ooh-attachment-list">
      <strong>Attachments requiring review</strong>
      {detail.email.attachments.map(item => <span key={item.providerAttachmentId}>
        {item.fileName} · {formatBytes(item.sizeBytes)}
      </span>)}
    </div>}
  </details>
}

function campaignModeLabel(value: string) {
  return value === masterDataCodes.campaignModes.oohOnly
    ? 'Out-of-home only'
    : value === masterDataCodes.campaignModes.fullCampaign
      ? 'Full campaign'
      : value.replaceAll('_', ' ')
}

function formatBytes(value: number) {
  if (value < 1024) return `${value} B`
  return `${Math.round(value / 1024)} KB`
}

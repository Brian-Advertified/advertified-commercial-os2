import type { InboundCampaignEmail } from '../api/email-automation-schemas'
import { masterDataCodes } from '../generated/master-data-codes'
import { automationFailureLabel, automationStatusLabel } from './email-automation-presentation'

export function InboxMessageList({ messages, selectedId, onSelect }: {
  messages: InboundCampaignEmail[]
  selectedId: string | null
  onSelect: (id: string) => void
}) {
  if (messages.length === 0) {
    return <article className="ooh-inbox-empty"><div className="ooh-empty-mark">OOH</div>
      <h2>No proposal emails yet</h2>
      <p>New messages to the connected mailbox will appear here as they move through STP, media planning and proposal delivery.</p>
    </article>
  }
  return <div className="ooh-message-list" aria-label="OOH proposal emails">
    {messages.map((message) => <button type="button" key={message.id}
      className={`ooh-message-card ${selectedId === message.id ? 'is-selected' : ''}`}
      onClick={() => onSelect(message.id)}>
      <div className="ooh-message-card-top"><span className={statusClass(message.status)}>
        {automationStatusLabel(message.status)}
      </span><time dateTime={message.receivedAtUtc}>{formatDate(message.receivedAtUtc)}</time></div>
      <strong>{message.subject}</strong>
      <span>{message.senderName || message.senderEmail}</span>
      {message.failureCode && <small>{automationFailureLabel(message.failureCode)}</small>}
    </button>)}
  </div>
}

function statusClass(status: string) {
  if (status === masterDataCodes.emailAutomationStatuses.sent) {
    return 'ooh-status ooh-status-sent'
  }
  if (status === masterDataCodes.emailAutomationStatuses.reviewRequired) {
    return 'ooh-status ooh-status-review'
  }
  if (status === masterDataCodes.emailAutomationStatuses.failed) {
    return 'ooh-status ooh-status-failed'
  }
  return 'ooh-status ooh-status-working'
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat('en-ZA', {
    day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit',
  }).format(new Date(value))
}

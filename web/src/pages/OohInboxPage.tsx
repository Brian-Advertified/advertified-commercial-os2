import { useCallback, useEffect, useMemo, useState, type Dispatch, type SetStateAction } from 'react'
import { Navigate } from 'react-router-dom'
import { api, humanMessage } from '../api/client'
import { emailAutomationApi } from '../api/email-automation-client'
import type {
  EmailAutomationClarification,
  InboundCampaignEmail,
  InboundEmailDetail,
  InboundMailbox,
  InboundMailboxInput,
} from '../api/email-automation-schemas'
import type { CurrentUser } from '../api/schemas'
import { useSession } from '../auth/session-state'
import { useWorkspace } from '../auth/workspace-state'
import { LoadingState, MessageState } from '../components/PageState'
import { InboxMessageDetail } from '../email-automation/InboxMessageDetail'
import { InboxMessageList } from '../email-automation/InboxMessageList'
import { MailboxSetupForm } from '../email-automation/MailboxSetupForm'
import { automationStatusLabel } from '../email-automation/email-automation-presentation'
import { masterDataCodes, masterDataDefinitions } from '../generated/master-data-codes'
import { notifications } from '../notifications/notifications'

export function OohInboxPage() {
  const { selected, loading } = useWorkspace()
  const { session } = useSession()
  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  if (!session) return <Navigate to="/sign-in" replace />
  return <OohInboxRecord tenantId={selected.tenantId}
    token={session.antiforgeryToken} />
}

function OohInboxRecord({ tenantId, token }: { tenantId: string; token: string }) {
  const inbox = useOohInbox(tenantId, token)
  if (inbox.error && !inbox.user) {
    return <MessageState title="Proposal inbox could not be opened" message={inbox.error} />
  }
  if (!inbox.user) return <LoadingState label="Loading the proposal inbox" />
  return <OohInboxContent inbox={inbox} />
}

type InboxState = ReturnType<typeof useOohInbox>

function OohInboxContent({ inbox }: { inbox: InboxState }) {
  return <section className="ooh-inbox-page" aria-labelledby="ooh-inbox-title">
    <header className="ooh-inbox-hero"><div><p className="eyebrow eyebrow-light">Email to proposal</p>
      <h1 id="ooh-inbox-title">Proposal inbox</h1>
      <p>Complete OOH requests can move through Brief interpretation, STP, media planning, verified inventory, proposal approval and PDF delivery without a per-request click.</p></div>
      <div className="ooh-hero-lock"><strong>One campaign flow</strong>
        <span>OOH follows the same stages as a full campaign. This inbox only permits OOH and DOOH media, and that choice cannot be widened later.</span></div></header>
    {inbox.error && <p className="inline-alert" role="alert">{inbox.error}</p>}
    {!inbox.mailbox || inbox.editing
      ? <MailboxSetupForm key={inbox.mailbox?.version ?? 'new'}
          current={inbox.mailbox} ownerUserId={inbox.user!.id}
          busy={inbox.busy} onSubmit={inbox.configure}
          onCancel={inbox.mailbox ? () => inbox.setEditing(false) : undefined} />
      : <ConnectedInbox inbox={inbox} />}
  </section>
}

function ConnectedInbox({ inbox }: { inbox: InboxState }) {
  return <>
    <MailboxSummary mailbox={inbox.mailbox!}
      onEdit={() => inbox.setEditing(true)} onRefresh={inbox.refresh} busy={inbox.busy} />
    <InboxMetrics messages={inbox.messages} />
    <div className="ooh-inbox-workspace">
      <section className="ooh-inbox-list-panel"><div className="ooh-panel-heading">
        <div><p className="eyebrow">Incoming requests</p><h2>Email activity</h2></div>
        <span>{inbox.messages.length}</span></div>
        <InboxMessageList messages={inbox.messages} selectedId={inbox.selectedId}
          onSelect={inbox.selectMessage} />
      </section>
      <section className="ooh-inbox-detail-panel">
        {inbox.detail
          ? <InboxMessageDetail detail={inbox.detail} busy={inbox.busy}
              onRetry={inbox.retrySelected} />
          : <article className="ooh-detail-empty"><div>↗</div><h2>Select an email</h2>
              <p>Open a request to see the original Brief, its latest stage and every approved artefact produced.</p></article>}
      </section>
    </div>
  </>
}

function MailboxSummary({ mailbox, onEdit, onRefresh, busy }: {
  mailbox: InboundMailbox
  onEdit: () => void
  onRefresh: () => Promise<void>
  busy: boolean
}) {
  return <article className="ooh-mailbox-summary"><div><p className="eyebrow">Connected mailbox</p>
    <h2>{mailbox.address}</h2><p>{providerLabel(mailbox.provider)} · {mailbox.allowedSenderDomains.length} allowed sender domain{mailbox.allowedSenderDomains.length === 1 ? '' : 's'}</p></div>
    <div className="ooh-mailbox-controls"><div className="ooh-mailbox-state">
      <span>{mailbox.autoSendEnabled ? 'Automatic sending on' : 'Automatic sending paused'}</span>
      <strong>{mailbox.autoSendEnabled ? 'Complete proposals send themselves' : 'Requests stop before delivery'}</strong></div>
      <button className="text-action" type="button" onClick={onEdit}>Settings</button>
      <button className="text-action" type="button" disabled={busy}
        onClick={() => void onRefresh()}>{busy ? 'Refreshing…' : 'Refresh'}</button></div>
  </article>
}

function InboxMetrics({ messages }: { messages: InboundCampaignEmail[] }) {
  const counts = useMemo(() => ({
    sent: messages.filter(item => item.status === masterDataCodes.emailAutomationStatuses.sent).length,
    review: messages.filter(item => item.status === masterDataCodes.emailAutomationStatuses.reviewRequired ||
      item.status === masterDataCodes.emailAutomationStatuses.failed).length,
    active: messages.filter(item => item.status === masterDataCodes.emailAutomationStatuses.received ||
      item.status === masterDataCodes.emailAutomationStatuses.processing).length,
  }), [messages])
  return <div className="ooh-inbox-metrics" aria-label="Proposal inbox summary">
    <article><span>Received</span><strong>{messages.length}</strong><small>Visible requests</small></article>
    <article><span>Proposal sent</span><strong>{counts.sent}</strong><small>Delivered automatically</small></article>
    <article><span>Needs attention</span><strong>{counts.review}</strong><small>Nothing was sent</small></article>
    <article><span>In progress</span><strong>{counts.active}</strong><small>Being prepared</small></article>
  </div>
}

function useOohInbox(tenantId: string, token: string) {
  const [mailbox, setMailbox] = useState<InboundMailbox | null>(null)
  const [messages, setMessages] = useState<InboundCampaignEmail[]>([])
  const [detail, setDetail] = useState<InboundEmailDetail | null>(null)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [user, setUser] = useState<CurrentUser | null>(null)
  const [editing, setEditing] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(async (preferredId?: string | null) => {
    const [currentMailbox, page, profile] = await Promise.all([
      emailAutomationApi.getMailbox(tenantId),
      emailAutomationApi.listMessages(tenantId),
      api.getCurrentUser(),
    ])
    setMailbox(currentMailbox); setMessages(page.items); setUser(profile.user)
    const id = preferredId && page.items.some(item => item.id === preferredId)
      ? preferredId : page.items[0]?.id ?? null
    setSelectedId(id)
    setDetail(id ? await emailAutomationApi.getMessage(tenantId, id) : null)
    setError(null)
  }, [tenantId])

  useInboxInitialLoad(load, setError)

  async function act(action: () => Promise<void>, preferredId = selectedId) {
    setBusy(true); setError(null)
    try { await action(); await load(preferredId) }
    catch (failure) { setError(humanMessage(failure)) }
    finally { setBusy(false) }
  }

  async function configure(configuration: InboundMailboxInput) {
    await act(async () => {
      await emailAutomationApi.configureMailbox(
        tenantId, configuration, token, mailbox)
      setEditing(false)
      notifications.success(mailbox
        ? 'Proposal mailbox settings saved.'
        : 'The proposal mailbox is connected.')
    }, null)
  }

  async function selectMessage(id: string) {
    setSelectedId(id); setDetail(null); setError(null)
    try { setDetail(await emailAutomationApi.getMessage(tenantId, id)) }
    catch (failure) { setError(humanMessage(failure)) }
  }

  async function retrySelected(clarifications: EmailAutomationClarification[]) {
    if (!detail) return
    await act(async () => {
      const run = await emailAutomationApi.retryMessage(
        tenantId, detail.run, clarifications, token)
      notifications.information(automationStatusLabel(run.status))
    }, detail.email.id)
  }

  async function refresh() {
    await act(async () => undefined)
  }

  return { mailbox, messages, detail, selectedId, user, editing, busy, error,
    setEditing, configure, selectMessage, retrySelected, refresh }
}

function useInboxInitialLoad(
  load: (preferredId?: string | null) => Promise<void>,
  setError: Dispatch<SetStateAction<string | null>>,
) {
  useEffect(() => {
    const timer = window.setTimeout(() => {
      void load(null).catch(failure => setError(humanMessage(failure)))
    }, 0)
    return () => window.clearTimeout(timer)
  }, [load, setError])
}

function providerLabel(code: string) {
  return masterDataDefinitions.emailProviders.find(item => item.code === code)?.displayLabel ?? code
}

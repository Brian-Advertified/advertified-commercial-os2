import { useCallback, useEffect, useRef, useState, type Dispatch, type SetStateAction } from 'react'
import { Navigate } from 'react-router-dom'
import { briefApi } from '../api/brief-client'
import { api, humanMessage } from '../api/client'
import { emailAutomationApi } from '../api/email-automation-client'
import { planningApi } from '../api/planning-client'
import type { PlanningWorkspace } from '../api/planning-schemas'
import type {
  EmailAutomationClarification,
  InboundCampaignEmail,
  InboundEmailDetail,
  InboundMailbox,
  InboundMailboxInput,
} from '../api/email-automation-schemas'
import type { CampaignBrief, CurrentUser } from '../api/schemas'
import { useSession } from '../auth/session-state'
import { useWorkspace } from '../auth/workspace-state'
import { CampaignModeBinding } from '../campaign-flow/CampaignFlowBindings'
import { LoadingState, MessageState } from '../components/PageState'
import { InboxMessageList } from '../email-automation/InboxMessageList'
import { MailboxSetupForm } from '../email-automation/MailboxSetupForm'
import { OohCampaignWorkspace } from '../email-automation/OohCampaignWorkspace'
import { automationStatusLabel } from '../email-automation/email-automation-presentation'
import { masterDataCodes } from '../generated/master-data-codes'
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
  return <><CampaignModeBinding mode={masterDataCodes.campaignModes.oohOnly} />
    <OohInboxContent inbox={inbox} /></>
}

type InboxState = ReturnType<typeof useOohInbox>

function OohInboxContent({ inbox }: { inbox: InboxState }) {
  return <section className="ooh-inbox-page" aria-labelledby="ooh-inbox-title">
    <header className="ooh-inbox-hero"><div><p className="eyebrow eyebrow-light">Email to proposal</p>
      <h1 id="ooh-inbox-title">Proposal inbox</h1>
      <p>When this tenant opts in, complete OOH requests can move through Brief interpretation, STP, media planning, verified inventory, proposal approval and PDF delivery to addresses that pass provider and mailbox checks. Anything unclear or unsafe is held for review.</p></div>
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
    <div className="approved-ooh-toolbar">
      <div><strong>{inbox.mailbox!.address}</strong>
        <small>{inbox.messages.length} request{inbox.messages.length === 1 ? '' : 's'}</small>
        <small>{inbox.mailbox!.autoSendEnabled
          ? 'Automatic sending on' : 'Automatic sending off'}</small></div>
      <div><button className="secondary-button" type="button" onClick={() => inbox.setEditing(true)}>Mailbox settings</button>
        <button className="secondary-button" type="button" disabled={inbox.busy} onClick={() => void inbox.refresh()}>{inbox.busy ? 'Refreshing…' : 'Refresh'}</button></div>
    </div>
    {inbox.messages.length > 1 && <div className="approved-ooh-switcher">
      <span>Requests</span><InboxMessageList messages={inbox.messages} selectedId={inbox.selectedId}
        busy={inbox.busy} onSelect={inbox.selectMessage} />
    </div>}
    {inbox.detail
      ? <OohCampaignWorkspace detail={inbox.detail} brief={inbox.brief}
          planning={inbox.planning} busy={inbox.busy}
          onRetry={inbox.retrySelected} onReconcile={inbox.reconcileSelected} />
      : <article className="ooh-detail-empty"><div>↗</div><h2>Select an email</h2>
          <p>Open a request to see the original Brief, shortlist and proposal progress.</p></article>}
  </>
}

function useOohInbox(tenantId: string, token: string) {
  const [mailbox, setMailbox] = useState<InboundMailbox | null>(null)
  const [messages, setMessages] = useState<InboundCampaignEmail[]>([])
  const [detail, setDetail] = useState<InboundEmailDetail | null>(null)
  const [planning, setPlanning] = useState<PlanningWorkspace | null>(null)
  const [brief, setBrief] = useState<CampaignBrief | null>(null)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [user, setUser] = useState<CurrentUser | null>(null)
  const [editing, setEditing] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const requestVersion = useRef(0)

  const load = useCallback(async (preferredId?: string | null) => {
    const requestId = ++requestVersion.current
    const [currentMailbox, page, profile] = await Promise.all([
      emailAutomationApi.getMailbox(tenantId),
      emailAutomationApi.listMessages(tenantId),
      api.getCurrentUser(),
    ])
    const id = preferredId && page.items.some(item => item.id === preferredId)
      ? preferredId : page.items[0]?.id ?? null
    const messageDetail = id ? await emailAutomationApi.getMessage(tenantId, id) : null
    const artifacts = await loadMessageArtifacts(tenantId, messageDetail)
    if (requestId !== requestVersion.current) return
    setMailbox(currentMailbox); setMessages(page.items); setUser(profile.user)
    setSelectedId(id); setDetail(messageDetail)
    setPlanning(artifacts.planning); setBrief(artifacts.brief); setError(null)
  }, [tenantId])

  useInboxInitialLoad(load, setError)

  async function act(action: () => Promise<void>, preferredId = selectedId) {
    setBusy(true); setError(null)
    try { await action(); await load(preferredId) }
    catch (failure) { setError(humanMessage(failure)) }
    finally { setBusy(false) }
  }

  const configure = (configuration: InboundMailboxInput) =>
    configureMailbox(tenantId, token, mailbox, setEditing, act, configuration)
  const selectMessage = (id: string) => {
    const requestId = ++requestVersion.current
    void selectInboxMessage(tenantId, id, requestId, () => requestVersion.current, {
      setSelectedId, setDetail, setPlanning, setBrief, setError,
    })
  }

  async function retrySelected(clarifications: EmailAutomationClarification[]) {
    await retrySelectedMessage(detail, clarifications, tenantId, token, act)
  }

  async function reconcileSelected() {
    await reconcileSelectedMessage(detail, tenantId, token, act)
  }

  async function refresh() {
    await act(async () => undefined)
  }

  return { mailbox, messages, detail, brief, planning, selectedId, user, editing, busy, error,
    setEditing, configure, selectMessage, retrySelected, reconcileSelected, refresh }
}

type InboxSelectionSetters = {
  setSelectedId: Dispatch<SetStateAction<string | null>>
  setDetail: Dispatch<SetStateAction<InboundEmailDetail | null>>
  setPlanning: Dispatch<SetStateAction<PlanningWorkspace | null>>
  setBrief: Dispatch<SetStateAction<CampaignBrief | null>>
  setError: Dispatch<SetStateAction<string | null>>
}

async function configureMailbox(
  tenantId: string,
  token: string,
  mailbox: InboundMailbox | null,
  setEditing: Dispatch<SetStateAction<boolean>>,
  act: InboxActionRunner,
  configuration: InboundMailboxInput,
) {
  await act(async () => {
    await emailAutomationApi.configureMailbox(tenantId, configuration, token, mailbox)
    setEditing(false)
    notifications.success(mailbox
      ? 'Proposal mailbox settings saved.'
      : 'The proposal mailbox is connected.')
  }, null)
}

async function selectInboxMessage(
  tenantId: string,
  id: string,
  requestId: number,
  currentRequest: () => number,
  setters: InboxSelectionSetters,
) {
  setters.setSelectedId(id)
  setters.setDetail(null); setters.setPlanning(null); setters.setBrief(null); setters.setError(null)
  try {
    const detail = await emailAutomationApi.getMessage(tenantId, id)
    if (requestId !== currentRequest()) return
    setters.setDetail(detail)
    const artifacts = await loadMessageArtifacts(tenantId, detail)
    if (requestId !== currentRequest()) return
    setters.setPlanning(artifacts.planning); setters.setBrief(artifacts.brief)
  } catch (failure) {
    if (requestId === currentRequest()) setters.setError(humanMessage(failure))
  }
}

async function loadMessageArtifacts(
  tenantId: string,
  detail: InboundEmailDetail | null,
) {
  const [planning, brief] = await Promise.all([
    detail?.run.briefVersionId
      ? planningApi.getWorkspace(tenantId, detail.run.briefVersionId).catch(() => null)
      : Promise.resolve(null),
    detail?.run.briefId
      ? briefApi.get(tenantId, detail.run.briefId).catch(() => null)
      : Promise.resolve(null),
  ])
  return { planning, brief }
}

type InboxActionRunner = (
  action: () => Promise<void>,
  preferredId?: string | null,
) => Promise<void>

async function retrySelectedMessage(
  detail: InboundEmailDetail | null,
  clarifications: EmailAutomationClarification[],
  tenantId: string,
  token: string,
  act: InboxActionRunner,
) {
  if (!detail || detail.run.deliveryRequestedAtUtc !== null) return
  await act(async () => {
    const run = await emailAutomationApi.retryMessage(
      tenantId, detail.run, clarifications, token)
    notifications.information(automationStatusLabel(run.status))
  }, detail.email.id)
}

async function reconcileSelectedMessage(
  detail: InboundEmailDetail | null,
  tenantId: string,
  token: string,
  act: InboxActionRunner,
) {
  if (!detail || detail.run.deliveryRequestedAtUtc === null &&
    detail.run.status !== masterDataCodes.emailAutomationStatuses.processing) return
  await act(async () => {
    const run = await emailAutomationApi.processMessage(
      tenantId, detail.run, token)
    notifications.information(run.status === masterDataCodes.emailAutomationStatuses.sent
      ? 'Provider acceptance confirmed. The proposal is recorded as sent.'
      : detail.run.deliveryRequestedAtUtc
        ? 'The original delivery was checked without sending another email.'
        : 'Processing resumed from the saved checkpoint.')
  }, detail.email.id)
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

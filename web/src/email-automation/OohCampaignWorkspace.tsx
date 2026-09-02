import { Link } from 'react-router-dom'
import type { CampaignBrief } from '../api/schemas'
import type { PlanningWorkspace } from '../api/planning-schemas'
import type {
  EmailAutomationClarification,
  InboundEmailDetail,
} from '../api/email-automation-schemas'
import { masterDataCodes } from '../generated/master-data-codes'
import { formatMoney, humanizeCode } from '../presentation/format'

type OohCampaignWorkspaceProps = {
  detail: InboundEmailDetail
  brief: CampaignBrief | null
  planning: PlanningWorkspace | null
  busy: boolean
  onRetry: (clarifications: EmailAutomationClarification[]) => Promise<void>
  onReconcile: () => Promise<void>
}

export function OohCampaignWorkspace(props: OohCampaignWorkspaceProps) {
  const view = oohCampaignView(props.brief, props.planning, props.detail)
  return <section className="approved-ooh-campaign ooh-message-detail" aria-labelledby="ooh-campaign-title">
    <header className="approved-ooh-campaign-header"><div>
      <h2 id="ooh-campaign-title">Inbound OOH Brief</h2>
      <span className="approved-live-pill">● Live</span>
      <span className="approved-waiting-pill">OOH-only campaign</span>
    </div><button type="button" aria-label="Close">×</button></header>
    <div className="approved-ooh-received"><span>▣</span><div>
      <strong>New OOH request received</strong>
      <small>From {props.detail.email.senderName ?? props.detail.email.senderEmail} ·
        {props.detail.email.subject}</small>
    </div></div>
    <OohBrief detail={props.detail} facts={view.facts} />
    <OohShortlist detail={props.detail} {...view} />
    <OohProposalPreview detail={props.detail} {...view} />
    <OohReviewActions {...props} needsReview={view.needsReview} />
  </section>
}

function OohBrief({ detail, facts }: {
  detail: InboundEmailDetail
  facts: Array<[string, string]>
}) {
  return <div className="approved-ooh-brief-grid">
    <article><header><h3>Extracted Requirement</h3>
      <small>Advertified extracted the explicit requirement from the email.</small>
    </header>
      <dl>{facts.map(([label, value]) =>
        <div key={label}><dt>{label}</dt><dd>{value}</dd></div>)}</dl>
      {facts.length === 0 &&
        <p>No structured fields were detected in the source text yet.</p>}
      <div className="approved-ooh-primary-action">{detail.run.briefVersionId
        ? <Link className="approved-green-button"
            to={`/planning/${detail.run.briefVersionId}`}>Interpret & Research →</Link>
        : <span className="approved-waiting-pill">Interpretation pending</span>}</div>
    </article>
    <article><header><h3>Original Brief</h3></header>
      <pre>{detail.sourceContent}</pre>
      <details><summary>View full email</summary>
        <p>{detail.email.senderEmail}</p></details>
    </article>
  </div>
}

type OohCampaignView = ReturnType<typeof oohCampaignView>

function OohShortlist({ detail, shortlist, selected, total }: {
  detail: InboundEmailDetail
  shortlist: OohCampaignView['shortlist']
  selected: OohCampaignView['selected']
  total: string
}) {
  return <article className="approved-ooh-shortlist">
    <header><div><h3>AI Shortlist</h3>
      <small>Top OOH inventory options based on the approved requirement.</small></div>
      <div><button className="secondary-button" type="button" disabled>↻ Recalculate</button>
        {detail.run.briefVersionId && <Link className="secondary-button"
          to={`/planning/${detail.run.briefVersionId}`}>Edit Criteria</Link>}</div>
    </header>
    <div className="approved-ooh-shortlist-table">
      {shortlist.length === 0
        ? <p className="approved-empty">
            Shortlist will appear when inventory selection is complete.</p>
        : shortlist.map((item, index) =>
          <div key={item.id}><img src={thumbnails[index % thumbnails.length]} alt="" />
            <div><strong>{item.name}</strong><small>{item.geography}</small>
              <span>{humanizeCode(item.channel, true)}</span></div>
            <dl><div><dt>Score</dt><dd>
              {item.score === null ? '—' : Math.round(item.score)}</dd></div>
              <div><dt>Rate</dt><dd>{inventoryRate(item)}</dd></div></dl>
            <em>{matchLabel(item.score)}</em><b>✓</b>
          </div>)}
    </div>
    <footer><span>Total (selected {selected.length})</span><strong>{total}</strong>
      {detail.run.proposalVersionId
        ? <Link className="approved-green-button"
            to={`/proposals/${detail.run.proposalVersionId}`}>View Proposal →</Link>
        : <span className="approved-waiting-pill">Proposal pending</span>}
    </footer>
  </article>
}

function OohProposalPreview({ detail, selected, shortlist, plan, total, coverage }: {
  detail: InboundEmailDetail
  selected: OohCampaignView['selected']
  shortlist: OohCampaignView['shortlist']
  plan: OohCampaignView['plan']
  total: string
  coverage: string
}) {
  return <div className="approved-ooh-bottom-grid">
    <article className="approved-ooh-proposal-preview">
      <header><h3>Proposal Preview</h3></header>
      <div className="approved-ooh-proposal-metrics">
        <div><span>Estimated Investment</span><strong>{total}</strong></div>
        <div><span>Inventory</span><strong>{selected.length || shortlist.length}</strong></div>
        <div><span>Coverage</span><strong>{coverage}</strong></div>
        <div><span>Supply</span>
          <strong>{plan ? humanizeCode(plan.supplyConfidence, true) : 'Pending'}</strong>
        </div>
      </div>
      <div className="approved-ooh-proposal-actions">
        {detail.run.proposalVersionId && <Link className="secondary-button"
          to={`/proposals/${detail.run.proposalVersionId}`}>View Full Proposal</Link>}
        {detail.run.status === masterDataCodes.emailAutomationStatuses.sent
          ? <h3 className="approved-sent-badge">✓ The proposal was sent automatically</h3>
          : <button className="approved-green-button" type="button" disabled>
              Send to Client</button>}
      </div>
    </article>
    <article className="approved-ooh-next-steps"><header><h3>Next Steps</h3></header><ul>
      <li>Proposal is checked against the approved plan and current rates.</li>
      <li>Any material uncertainty is held for human review.</li>
      <li>Once approved or policy-authorised, booking can proceed.</li>
      <li>Campaign delivery and proof stay on the same commercial record.</li>
    </ul></article>
  </div>
}

function OohReviewActions(props: OohCampaignWorkspaceProps & { needsReview: boolean }) {
  const action = reviewAction(props.detail)
  if (!action) return null
  return <section className="approved-ooh-review-actions" role="status">
    <div><h3>{action.title}</h3><p>{action.detail}</p>
      {action.marker && <strong>{action.marker}</strong>}</div>
    {action.button && <button className="primary-button" type="button"
      disabled={props.busy} onClick={() => void action.run(props)}>
      {props.busy ? action.busyLabel : action.button}</button>}
  </section>
}

type OohReviewAction = {
  title: string
  detail: string
  marker?: string
  button?: string
  busyLabel?: string
  run: (props: OohCampaignWorkspaceProps) => Promise<void>
}

function reviewAction(detail: InboundEmailDetail): OohReviewAction | null {
  const failure = detail.run.failureCode
  if (failure === masterDataCodes.automationFailureReasons.nonOohRequest) {
    return { title: 'Nothing was sent',
      detail: detail.run.failureMessage ??
        'This request includes media beyond OOH. Start a new full campaign instead.',
      run: async () => undefined }
  }
  if (failure === masterDataCodes.automationFailureReasons.deliveryAmbiguous) {
    return { title: 'Provider acceptance is unknown',
      detail: 'The provider may have accepted the original delivery request. Check that same request before taking any further action.',
      marker: 'Not confirmed', button: 'Check original delivery', busyLabel: 'Checking…',
      run: props => props.onReconcile() }
  }
  if (failure === masterDataCodes.automationFailureReasons.deliveryRecordingRequired) {
    return { title: 'Provider acceptance is recorded',
      detail: 'The provider accepted the original delivery. Finish recording it locally without sending another email.',
      button: 'Finish recorded delivery', busyLabel: 'Finishing…',
      run: props => props.onReconcile() }
  }
  if (detail.run.status === masterDataCodes.emailAutomationStatuses.processing) {
    return { title: 'Run can be resumed from its saved checkpoint',
      detail: 'Advertified will reuse completed steps and continue from the saved checkpoint.',
      button: 'Resume from saved checkpoint', busyLabel: 'Resuming…',
      run: props => props.onReconcile() }
  }
  if (detail.questions.length > 0) {
    return { title: 'Confirm what the Brief did not establish',
      detail: 'A person must resolve the material unknowns before preparation can continue.',
      button: 'Open clarification', busyLabel: 'Opening…',
      run: props => props.onRetry([]) }
  }
  if (!reviewStatuses.has(detail.run.status)) return null
  return { title: 'This request needs attention',
    detail: detail.run.failureMessage ?? 'Review the retained evidence before continuing.',
    run: async () => undefined }
}

function oohCampaignView(
  brief: CampaignBrief | null,
  planning: PlanningWorkspace | null,
  detail: InboundEmailDetail,
) {
  const facts = campaignFacts(brief, planning)
  const shortlist = planningShortlist(planning)
  const selected = shortlist.filter(item => item.isSelected !== false)
  const plan = planning?.mediaPlan ?? null
  return {
    facts,
    shortlist,
    selected,
    plan,
    total: planTotal(plan),
    coverage: coverageFact(shortlist, facts),
    needsReview: isReviewRequired(detail),
  }
}

function planningShortlist(planning: PlanningWorkspace | null) {
  return planning?.shortlist?.candidates
    .filter(item => item.isEligible).slice(0, 5) ?? []
}

function planTotal(plan: PlanningWorkspace['mediaPlan']) {
  return plan ? formatMoney(plan.totalMinor, plan.currency, 0) : 'Pending'
}

function coverageFact(
  shortlist: Array<{ geography: string }>,
  facts: Array<[string, string]>,
) {
  return shortlist[0]?.geography ??
    facts.find(([label]) => label === 'Geography')?.[1] ?? 'Pending'
}

function campaignFacts(
  record: CampaignBrief | null,
  planning: PlanningWorkspace | null,
): Array<[string, string]> {
  const version = record?.versions.at(-1)
  if (!version) return []
  return [
    ['Objective', version.objective || 'Not supplied'],
    ['Audience', version.audiences.join(' · ') || 'Not supplied'],
    ['Geography', version.geographies.join(' · ') || 'Not supplied'],
    ['Flight', version.timing || 'Not supplied'],
    ['Budget', budgetFact(version)],
    ['Media Type', campaignModeFact(planning)],
  ]
}

function budgetFact(version: CampaignBrief['versions'][number]) {
  if (version.budgetUnknown || version.budgetMinor === null || !version.currency) {
    return 'Not supplied'
  }
  return formatMoney(version.budgetMinor, version.currency, 0)
}

function campaignModeFact(planning: PlanningWorkspace | null) {
  if (planning?.campaignMode?.mode === masterDataCodes.campaignModes.oohOnly) {
    return 'OOH / DOOH only'
  }
  if (planning?.campaignMode?.mode === masterDataCodes.campaignModes.fullCampaign) {
    return 'Full campaign'
  }
  return 'Not resolved'
}

const reviewStatuses = new Set<string>([
  masterDataCodes.emailAutomationStatuses.reviewRequired,
  masterDataCodes.emailAutomationStatuses.failed,
])

function isReviewRequired(detail: InboundEmailDetail) {
  return reviewStatuses.has(detail.run.status)
}

function inventoryRate(item: OohCampaignView['shortlist'][number]) {
  if (item.rateAmountMinor === null || !item.currency) return 'Confirm'
  return formatMoney(item.rateAmountMinor, item.currency, 0)
}

function matchLabel(score: number | null) {
  return score !== null && score >= 80 ? 'High Match' : 'Good Match'
}

const thumbnails = [
  '/assets/media-inventory/out-of-home-real.jpg',
  '/assets/media-inventory/out-of-home.jpg',
  '/assets/media-inventory/digital-real.jpg',
]

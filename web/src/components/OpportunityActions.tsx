import { useState, type FormEvent } from 'react'
import { humanMessage } from '../api/client'
import { opportunityApi } from '../api/opportunity-client'
import { opportunityCodes } from '../api/opportunity-constants'
import type { OpportunityDetail } from '../api/schemas'
import { useSession } from '../auth/session-state'

type Props = { detail: OpportunityDetail; tenantId: string; reload: () => Promise<void> }
type Runner = { busy: string | null; run: (label: string, action: () => Promise<unknown>) => Promise<void> }
type ControlProps = Props & { token: string; runner: Runner }

export function OpportunityActions(props: Props) {
  const { session } = useSession()
  const [busy, setBusy] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const runner: Runner = {
    busy,
    run: async (label, action) => {
      setBusy(label); setError(null)
      try {
        await action(); await props.reload()
        window.setTimeout(() => void props.reload(), 500)
      } catch (failure) {
        setError(humanMessage(failure))
      } finally {
        setBusy(null)
      }
    },
  }
  const controls = { ...props, token: session?.antiforgeryToken ?? '', runner }
  return <section className="action-panel" aria-labelledby="actions-title">
    <div className="page-heading-split"><div><p className="eyebrow">Available controls</p>
      <h2 id="actions-title">Act on this record</h2></div>
      <button className="text-action" type="button" onClick={() => void props.reload()}>Refresh</button></div>
    {error && <p className="inline-alert" role="alert">{error}</p>}
    <EvidenceControls {...controls} />
    <AgentControls {...controls} />
    <StrategyControls {...controls} />
    <BriefControls {...controls} />
  </section>
}

function EvidenceControls(props: ControlProps) {
  const { detail, tenantId, token, runner } = props
  const [approverId, setApproverId] = useState('')
  const opportunity = detail.opportunity
  if (detail.sources.length === 0) return <SourceForm {...props} />
  return <>
    {opportunity.stage === opportunityCodes.status.created &&
      <ActionButton label="Start qualification" runner={runner}
      action={() => opportunityApi.startQualification(tenantId, opportunity.id, opportunity.version, token)} />}
    {detail.evidenceItems.filter(
      (item) => item.reviewStatus === opportunityCodes.status.pending).map((item) =>
      <ActionButton key={item.id} label="Approve assigned evidence" runner={runner}
        action={() => opportunityApi.reviewEvidence(tenantId, item.id, item.version, token)} />)}
    {canSubmitEvidence(detail) && <>
      <Identifier label="Evidence-set approver user ID" value={approverId} setValue={setApproverId} />
      <ActionButton label="Submit evidence set" runner={runner} disabled={!approverId}
        action={() => opportunityApi.submitEvidence(
          tenantId, opportunity.id, approverId, opportunity.version, token)} />
    </>}
    {detail.evidenceSet?.status === opportunityCodes.status.inReview && <ActionButton
      label="Approve assigned evidence set" runner={runner}
      action={() => opportunityApi.approveEvidence(
        tenantId, detail.evidenceSet!.id, detail.evidenceSet!.version, token)} />}
  </>
}

function SourceForm({ detail, tenantId, token, runner }: ControlProps) {
  const [reviewerId, setReviewerId] = useState('')
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const values = new FormData(event.currentTarget)
    const content = String(values.get('content')).trim()
    void runner.run('Register source', () => opportunityApi.registerSource(
      tenantId, detail.opportunity.id, sourcePayload(detail, values, content, reviewerId), token))
  }
  return <form className="action-form" onSubmit={submit}>
    <label className="field-group">Source title<input name="title" required /></label>
    <label className="field-group">Supplied source text<textarea name="content" required /></label>
    <Identifier label="Evidence reviewer user ID" value={reviewerId} setValue={setReviewerId} />
    <ActionButton label="Register source" runner={runner} disabled={!reviewerId} />
  </form>
}

function sourcePayload(
  detail: OpportunityDetail,
  values: FormData,
  content: string,
  reviewerUserId: string,
) {
  return {
    opportunityId: detail.opportunity.id,
    type: opportunityCodes.sourceType.suppliedText,
    locator: `supplied:web:${crypto.randomUUID()}`,
    title: String(values.get('title')),
    policyBasis: opportunityCodes.policyBasis.ownerSupplied,
    content,
    reviewerUserId,
    claims: [{
      locator: 'supplied:web:claim-1', claimType: opportunityCodes.claimType.businessContext,
      structuredValueJson: JSON.stringify({ statement: content }), excerpt: content, confidence: 1,
    }],
  }
}

function AgentControls({ detail, tenantId, token, runner }: ControlProps) {
  const opportunity = detail.opportunity
  const approvedInterpretation =
    detail.interpretation?.status === opportunityCodes.status.approved
  const selectedAngle = detail.angles.find(
    (item) => item.status === opportunityCodes.angleStatus.selected)
  return <>
    {opportunity.stage === opportunityCodes.status.strategyReady && !detail.interpretation &&
      <ActionButton label="Interpret approved evidence" runner={runner}
        action={() => opportunityApi.queue(tenantId, opportunity.id, 'interpret', token)} />}
    {detail.interpretation?.status === opportunityCodes.status.draft &&
      <ActionButton label="Confirm interpretation" runner={runner}
        action={() => opportunityApi.confirmInterpretation(
          tenantId, detail.interpretation!.id, detail.interpretation!.version, token)} />}
    {approvedInterpretation && detail.angles.length === 0 &&
      <ActionButton label="Generate opportunity angles" runner={runner}
        action={() => opportunityApi.queue(tenantId, opportunity.id, 'angles:generate', token)} />}
    {!selectedAngle && detail.angles.map((angle) =>
      <ActionButton key={angle.id} label={`Select angle ${angle.rank}`} runner={runner}
        action={() => opportunityApi.selectAngle(tenantId, angle.id, angle.version, token)} />)}
  </>
}

function StrategyControls({ detail, tenantId, token, runner }: ControlProps) {
  const [approverId, setApproverId] = useState('')
  const selectedAngle = detail.angles.some(
    (item) => item.status === opportunityCodes.angleStatus.selected)
  const unresolved = detail.strategy?.objections.filter((item) => !item.resolution) ?? []
  return <>
    {selectedAngle && !detail.strategy && <>
      <Identifier label="Strategy approver user ID" value={approverId} setValue={setApproverId} />
      <ActionButton label="Generate strategy and critic" runner={runner} disabled={!approverId}
        action={() => opportunityApi.queue(
          tenantId, detail.opportunity.id, 'strategies:generate', token, approverId)} />
    </>}
    {unresolved.map((item) => <ActionButton key={item.id}
      label={`Resolve ${item.severity.toLowerCase()} objection`} runner={runner}
      action={() => opportunityApi.resolveObjection(tenantId, item.id, item.version, token)} />)}
    {detail.strategy?.status === opportunityCodes.status.draft && unresolved.length === 0 &&
      <ActionButton label="Submit strategy" runner={runner}
        action={() => opportunityApi.submitStrategy(
          tenantId, detail.strategy!.id, detail.strategy!.version, token)} />}
    {detail.strategy?.status === opportunityCodes.status.inReview &&
      <><ActionButton label="Approve assigned strategy" runner={runner}
          action={() => opportunityApi.approveStrategy(
            tenantId, detail.strategy!.id, detail.strategy!.version, token)} />
        <ActionButton label="Reject assigned strategy" runner={runner}
          action={() => opportunityApi.rejectStrategy(
            tenantId, detail.strategy!.id, detail.strategy!.version, token)} /></>}
  </>
}

function BriefControls({ detail, tenantId, token, runner }: ControlProps) {
  if (detail.opportunity.stage !== opportunityCodes.status.briefReady) return null
  if (detail.briefId) return <LinkButton label="Review campaign Brief" to={`/briefs/${detail.briefId}`} />
  return <ActionButton label="Draft campaign Brief" runner={runner}
    action={() => opportunityApi.queue(
      tenantId, detail.opportunity.id, 'briefs:draft', token)} />
}

function LinkButton({ label, to }: { label: string; to: string }) {
  return <a className="primary-button button-link" href={to}>{label}</a>
}

function canSubmitEvidence(detail: OpportunityDetail): boolean {
  return detail.opportunity.stage === opportunityCodes.status.qualifying &&
    detail.evidenceItems.length > 0 &&
    detail.evidenceItems.every(
      (item) => item.reviewStatus !== opportunityCodes.status.pending) && !detail.evidenceSet
}

function Identifier({ label, value, setValue }: { label: string; value: string; setValue: (value: string) => void }) {
  return <label className="field-group identifier-field">{label}
    <input value={value} onChange={(event) => setValue(event.target.value)} required pattern="[0-9a-fA-F-]{36}" />
  </label>
}

function ActionButton({ label, runner, action, disabled = false }: {
  label: string; runner: Runner; action?: () => Promise<unknown>; disabled?: boolean
}) {
  const click = action ? () => void runner.run(label, action) : undefined
  return <button className="primary-button" type={action ? 'button' : 'submit'}
    onClick={click} disabled={Boolean(runner.busy) || disabled}>
    {runner.busy === label ? 'Working…' : label}
  </button>
}

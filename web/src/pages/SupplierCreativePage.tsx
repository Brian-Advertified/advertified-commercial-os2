import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { Navigate, useParams } from 'react-router-dom'
import { z } from 'zod'
import { campaignApi } from '../api/campaign-client'
import type { SupplierCreativeAsset } from '../api/campaign-schemas'
import { humanMessage } from '../api/client'
import { useSession } from '../auth/session-state'
import { useWorkspace } from '../auth/workspace-state'
import { CampaignFlowBinding } from '../campaign-flow/CampaignFlowBindings'
import { supplierCreativeReviewerRoles } from '../campaign/campaign-roles'
import { Icon } from '../components/Icon'
import { LoadingState, MessageState } from '../components/PageState'
import { notifications } from '../notifications/notifications'
import { formatMiB, humanizeCode } from '../presentation/format'

const reviewSchema = z.object({
  evidenceReference: z.string().trim().min(1).max(1000),
  reason: z.string().trim().min(1).max(1000),
}).strict()

export function SupplierCreativePage() {
  const route = z.guid().safeParse(useParams().assetId)
  const { selected, loading } = useWorkspace()
  const { session } = useSession()
  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  if (!session || !route.success) return <Navigate to="/tasks" replace />
  if (!supplierCreativeReviewerRoles.has(selected.roleCode)) {
    return <MessageState title="Creative review is not available"
      message="This workspace role cannot review supplier technical delivery." />
  }
  return <SupplierCreativeRecord tenantId={selected.tenantId} assetId={route.data}
    token={session.antiforgeryToken} />
}

function SupplierCreativeRecord({ tenantId, assetId, token }: {
  tenantId: string
  assetId: string
  token: string
}) {
  const model = useSupplierCreative(tenantId, assetId)
  if (model.error && !model.asset) {
    return <MessageState title="Creative asset could not be opened" message={model.error} />
  }
  if (!model.asset) return <LoadingState label="Loading supplier creative review" />
  return <><CampaignFlowBinding tenantId={tenantId} campaignId={model.asset.campaignId} />
  <section className="supplier-creative-page" aria-labelledby="supplier-creative-title">
    <SupplierCreativeHeader asset={model.asset} />
    {model.error && <p className="inline-alert" role="alert">{model.error}</p>}
    <SupplierCreativeDetails asset={model.asset} />
    {!model.asset.supplierDecision && <SupplierReviewForm asset={model.asset}
      busy={model.busy} review={(approved, evidence, reason) => model.review(
        approved, evidence, reason, token)} />}
  </section></>
}

function useSupplierCreative(tenantId: string, assetId: string) {
  const [asset, setAsset] = useState<SupplierCreativeAsset | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const load = useCallback(async () => {
    const value = await campaignApi.getSupplierCreativeAsset(tenantId, assetId)
    setAsset(value); setError(null)
  }, [tenantId, assetId])
  useEffect(() => {
    let active = true
    void campaignApi.getSupplierCreativeAsset(tenantId, assetId)
      .then(value => { if (active) setAsset(value) })
      .catch((failure: unknown) => { if (active) setError(humanMessage(failure)) })
    return () => { active = false }
  }, [tenantId, assetId])
  async function review(approved: boolean, evidence: string, reason: string, token: string) {
    if (!asset) return
    setBusy(true); setError(null)
    try {
      await campaignApi.reviewCreativeSupplier(
        tenantId, asset, approved, evidence, reason, token)
      await load()
      notifications.success(approved
        ? 'The current creative version passed supplier technical review.'
        : 'The current creative version was rejected for technical correction.')
    } catch (failure) { setError(humanMessage(failure)) }
    finally { setBusy(false) }
  }
  return { asset, error, busy, review }
}

function SupplierCreativeHeader({ asset }: { asset: SupplierCreativeAsset }) {
  return <header className="supplier-creative-hero"><div><p className="eyebrow eyebrow-light">Supplier technical review</p>
    <h1 id="supplier-creative-title">Review the exact production file for your booked format.</h1>
    <p>Approval applies only to this immutable file version and its recorded technical requirement.</p></div>
    <span className={`status-chip ${asset.supplierDecision ? 'status-positive' : 'status-warning'}`}>
      {asset.supplierDecision ? humanizeCode(asset.supplierDecision, true) : 'Review required'}</span></header>
}

function SupplierCreativeDetails({ asset }: { asset: SupplierCreativeAsset }) {
  return <article className="supplier-creative-detail"><header><span><Icon name="proposal" /></span>
    <div><small>{humanizeCode(asset.channel, true)}</small><h2>{asset.formatCode}</h2></div></header>
    <dl><div><dt>Dimensions</dt><dd>{asset.width} × {asset.height}</dd></div>
      <div><dt>Required file type</dt><dd>{asset.requiredMediaType}</dd></div>
      <div><dt>Maximum size</dt><dd>{formatMiB(asset.maximumBytes)}</dd></div>
      <div><dt>Current file</dt><dd>{asset.fileName}</dd></div>
      <div><dt>Current version</dt><dd>{asset.versionNumber}</dd></div>
      <div><dt>File size</dt><dd>{formatMiB(asset.sizeBytes)}</dd></div></dl>
    <section><h3>Supplier instructions</h3><p>{asset.instructions}</p></section>
  </article>
}

function SupplierReviewForm({ asset, busy, review }: {
  asset: SupplierCreativeAsset
  busy: boolean
  review: (approved: boolean, evidence: string, reason: string) => Promise<void>
}) {
  const [error, setError] = useState<string | null>(null)
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const values = new FormData(event.currentTarget)
    const parsed = reviewSchema.safeParse({
      evidenceReference: values.get('evidenceReference'),
      reason: values.get('reason'),
    })
    const submitter = (event.nativeEvent as SubmitEvent).submitter
    if (!parsed.success || !(submitter instanceof HTMLButtonElement)) {
      setError('Record the technical evidence and reason for this decision.')
      return
    }
    setError(null)
    void review(submitter.value === 'approve',
      parsed.data.evidenceReference, parsed.data.reason)
  }
  return <form className="supplier-creative-review" onSubmit={submit}>
    <div><p className="eyebrow">Technical decision</p><h2>Does version {asset.versionNumber} meet the booked requirement?</h2></div>
    {error && <p className="inline-alert" role="alert">{error}</p>}
    <label className="field-group">Technical evidence reference
      <input name="evidenceReference" required maxLength={1000} /></label>
    <label className="field-group">Decision reason
      <textarea name="reason" required maxLength={1000} rows={4} /></label>
    <div><button className="secondary-button" value="reject" disabled={busy}>Reject current version</button>
      <button className="primary-button" value="approve" disabled={busy}>Approve technical delivery</button></div>
  </form>
}

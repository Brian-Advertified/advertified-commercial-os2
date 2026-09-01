import { useState, type FormEvent } from 'react'
import { Navigate, useNavigate, useParams } from 'react-router-dom'
import { z } from 'zod'
import { campaignApi } from '../api/campaign-client'
import { deliveryProofInputSchema } from '../api/campaign-schemas'
import { humanMessage } from '../api/client'
import { useSession } from '../auth/session-state'
import { useWorkspace } from '../auth/workspace-state'
import { deliveryProofSubmitterRoles } from '../campaign/campaign-roles'
import { Icon } from '../components/Icon'
import { LoadingState, MessageState } from '../components/PageState'
import { masterDataDefinitions } from '../generated/master-data-codes'

const routeSchema = z.object({
  campaignId: z.guid(),
  bookingId: z.guid(),
}).strict()

export function DeliveryProofSubmissionPage() {
  const route = routeSchema.safeParse(useParams())
  const { selected, loading } = useWorkspace()
  const { session } = useSession()
  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  if (!session || !route.success) return <Navigate to="/bookings" replace />
  if (!deliveryProofSubmitterRoles.has(selected.roleCode)) {
    return <MessageState title="Proof submission is not available"
      message="This workspace role cannot submit supplier delivery evidence." />
  }
  return <ProofSubmissionForm tenantId={selected.tenantId} token={session.antiforgeryToken}
    campaignId={route.data.campaignId} bookingId={route.data.bookingId} />
}

function ProofSubmissionForm({ tenantId, token, campaignId, bookingId }: {
  tenantId: string
  token: string
  campaignId: string
  bookingId: string
}) {
  const navigate = useNavigate()
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const values = new FormData(event.currentTarget)
    const file = values.get('file')
    const parsed = deliveryProofInputSchema.safeParse(proofInput(values, bookingId))
    if (!parsed.success || !(file instanceof File) || file.size === 0) {
      setError('Complete the proof details and choose the matching evidence file.')
      return
    }
    if (file.size > 25 * 1024 * 1024) {
      setError('The proof file must not exceed 25 MiB.')
      return
    }
    setBusy(true); setError(null)
    try {
      const proof = await campaignApi.submitDeliveryProof(
        tenantId, campaignId, parsed.data, file, token)
      navigate(`/delivery-proofs/${proof.id}`)
    } catch (failure) { setError(humanMessage(failure)) }
    finally { setBusy(false) }
  }
  return <section className="proof-submission-page" aria-labelledby="proof-submit-title">
    <ProofSubmissionHeader />
    <form className="proof-submission-form" onSubmit={submit}>
      {error && <p className="inline-alert" role="alert">{error}</p>}
      <ProofSubmissionFields />
      <label className="field-group">Submission reason
        <textarea name="reason" required maxLength={1000} rows={4} /></label>
      <label className="field-group">Evidence file
        <input name="file" type="file" required
          accept="image/png,image/jpeg,application/pdf" /></label>
      <footer><span><Icon name="shield" /> The stored hash proves file integrity after submission, not the truthfulness of the capture.</span>
        <button className="primary-button" disabled={busy}>{busy ? 'Submitting proof…' : 'Submit delivery proof'}</button></footer>
    </form>
  </section>
}

function ProofSubmissionHeader() {
  return <header className="proof-submission-hero"><div>
    <p className="eyebrow eyebrow-light">Supplier delivery evidence</p>
    <h1 id="proof-submit-title">Submit proof for the exact confirmed Booking.</h1>
    <p>The evidence must have been captured inside the booked flight window and must match the selected proof type.</p>
  </div><span><Icon name="evidence" /></span></header>
}

function ProofSubmissionFields() {
  return <div className="proof-form-grid"><label className="field-group">Proof type
    <select name="proofType" required defaultValue="">
      <option value="" disabled>Choose proof type</option>
      {masterDataDefinitions.deliveryProofTypes.map(item =>
        <option key={item.code} value={item.code}>{item.displayLabel}</option>)}
    </select></label>
    <label className="field-group">Captured at
      <input name="capturedAtUtc" type="datetime-local" required /></label>
    <label className="field-group">Location description
      <input name="locationDescription" required maxLength={500} /></label>
    <label className="field-group">Source reference
      <input name="sourceReference" required maxLength={500} /></label>
    <label className="field-group">Latitude — optional
      <input name="latitude" type="number" min="-90" max="90" step="any" /></label>
    <label className="field-group">Longitude — optional
      <input name="longitude" type="number" min="-180" max="180" step="any" /></label></div>
}

function proofInput(values: FormData, bookingId: string) {
  const latitude = optionalNumber(values.get('latitude'))
  const longitude = optionalNumber(values.get('longitude'))
  return {
    bookingId,
    proofType: String(values.get('proofType') ?? ''),
    capturedAtUtc: toUtc(String(values.get('capturedAtUtc') ?? '')),
    locationDescription: String(values.get('locationDescription') ?? ''),
    latitude,
    longitude,
    sourceReference: String(values.get('sourceReference') ?? ''),
    reason: String(values.get('reason') ?? ''),
  }
}

function optionalNumber(value: FormDataEntryValue | null) {
  const text = String(value ?? '').trim()
  return text ? Number(text) : null
}

function toUtc(value: string) {
  const parsed = new Date(value)
  return Number.isNaN(parsed.getTime()) ? value : parsed.toISOString()
}

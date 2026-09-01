import { useEffect, useState } from 'react'
import { Link, Navigate } from 'react-router-dom'
import { campaignApi } from '../api/campaign-client'
import type { DeliveryProofRequest } from '../api/campaign-schemas'
import { humanMessage } from '../api/client'
import { useWorkspace } from '../auth/workspace-state'
import { deliveryProofSubmitterRoles } from '../campaign/campaign-roles'
import { Icon } from '../components/Icon'
import { LoadingState, MessageState } from '../components/PageState'
import { masterDataCodes } from '../generated/master-data-codes'
import { formatDate, formatDateTime, humanizeCode } from '../presentation/format'

export function DeliveryProofRequestsPage() {
  const { selected, loading } = useWorkspace()
  const [requests, setRequests] = useState<DeliveryProofRequest[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  useEffect(() => {
    if (!selected || !deliveryProofSubmitterRoles.has(selected.roleCode)) return
    let active = true
    void campaignApi.listDeliveryProofRequests(selected.tenantId)
      .then(value => { if (active) setRequests(value) })
      .catch((failure: unknown) => { if (active) setError(humanMessage(failure)) })
    return () => { active = false }
  }, [selected])
  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  if (!deliveryProofSubmitterRoles.has(selected.roleCode)) {
    return <MessageState title="Proof requests are not available"
      message="This workspace role cannot submit supplier delivery evidence." />
  }
  if (error) return <MessageState title="Proof requests could not be loaded" message={error} />
  if (!requests) return <LoadingState label="Loading supplier proof requests" />
  return <ProofRequestList requests={requests} />
}

function ProofRequestList({ requests }: { requests: DeliveryProofRequest[] }) {
  return <section className="proof-request-page" aria-labelledby="proof-requests-title">
    <header className="proof-request-hero"><div><p className="eyebrow eyebrow-light">Supplier delivery queue</p>
      <h1 id="proof-requests-title">Submit proof for completed confirmed Bookings.</h1>
      <p>Each request is derived from an exact buyer campaign and Booking. Evidence remains linked to that delivery window.</p></div>
      <span className="campaign-count"><strong>{requests.length}</strong> requests</span></header>
    {requests.length === 0 ? <article className="campaign-empty"><Icon name="evidence" /><div>
      <h2>No proof is currently requested</h2><p>Requests appear after the buyer records campaign completion.</p></div></article>
      : <div className="proof-request-grid">{requests.map(request =>
        <ProofRequestCard request={request} key={request.bookingId} />)}</div>}
  </section>
}

function ProofRequestCard({ request }: { request: DeliveryProofRequest }) {
  const canSubmit = !request.latestProofId ||
    request.latestProofStatus === masterDataCodes.lifecycleStatuses.rejected
  return <article className="proof-request-card"><header><span><Icon name="evidence" /></span>
    <div><small>{humanizeCode(request.channel, true)}</small><h2>{request.productName}</h2>
      <p>{request.supplierName} · {request.geography}</p></div>
    <span className={`status-chip ${canSubmit ? 'status-warning' : 'status-positive'}`}>
      {request.latestProofStatus ? humanizeCode(request.latestProofStatus, true) : 'Proof requested'}</span></header>
    <dl><div><dt>Booked flight</dt><dd>{formatDate(request.flightStart)} – {formatDate(request.flightEnd)}</dd></div>
      <div><dt>Requested</dt><dd>{formatDateTime(request.proofRequestedAtUtc)}</dd></div></dl>
    <p className="proof-request-reason">{request.proofRequestReason}</p>
    <footer>{canSubmit
      ? <Link className="primary-button"
          to={`/campaigns/${request.campaignId}/bookings/${request.bookingId}/delivery-proof/new`}>
          Submit delivery proof <Icon name="arrow" />
        </Link>
      : request.latestProofId && <Link className="secondary-button"
          to={`/delivery-proofs/${request.latestProofId}`}>Open submitted proof</Link>}</footer>
  </article>
}

import { inventoryApi } from '../api/inventory-client'
import { inventoryCodes } from '../api/inventory-constants'
import type { InventoryImport } from '../api/inventory-schemas'

const retryableStatuses = new Set<string>([
  inventoryCodes.extractionStatus.failedTerminal,
  inventoryCodes.extractionStatus.timedOut,
  inventoryCodes.extractionStatus.cancelled,
])
const cancellableStatuses = new Set<string>([
  inventoryCodes.extractionStatus.pending,
  inventoryCodes.extractionStatus.submitting,
  inventoryCodes.extractionStatus.running,
  inventoryCodes.extractionStatus.failedRetryable,
  inventoryCodes.extractionStatus.reconciliationRequired,
])

type Actions = {
  busy: boolean
  run: (
    action: (token: string) => Promise<unknown>,
    success: string,
  ) => Promise<void>
}

type Props = {
  tenantId: string
  record: InventoryImport
  actions: Actions
  reason: string
  externalTaskId: string
}

export function ExtractionAttemptActions(props: Props) {
  const { record } = props
  if (!record.extractionAttempts[0]) return null
  return <div className="button-row">
    <RefreshAction {...props} />
    <RetryAction {...props} />
    <ReprojectAction {...props} />
    <ReconcileAction {...props} />
    <CancelAction {...props} />
  </div>
}

function RefreshAction({ record, actions }: Props) {
  return <button className="secondary-button" type="button"
    onClick={() => void actions.run(
      async () => record,
      'Extraction state refreshed.',
    )}>Refresh</button>
}

function RetryAction({ tenantId, record, actions, reason }: Props) {
  const attempt = record.extractionAttempts[0]
  if (!attempt ||
      !retryableStatuses.has(attempt.status) ||
      attempt.providerName === 'retained-docling-projection') return null
  return <button className="primary-button"
    disabled={actions.busy || !reason.trim()}
    onClick={() => void actions.run(
      token => inventoryApi.retryExtraction(
        tenantId, record, token, reason,
      ),
      'A new extraction attempt is queued for the same source.',
    )}>Retry as new attempt</button>
}

function ReprojectAction({ tenantId, record, actions, reason }: Props) {
  const canReproject =
    record.status === inventoryCodes.importStatus.reviewRequired &&
    record.candidateCounts.approved === 0 &&
    record.candidateCounts.rejected === 0
  if (!canReproject) return null
  return <button className="primary-button"
    disabled={actions.busy || !reason.trim()}
    onClick={() => void actions.run(
      token => inventoryApi.reprojectExtraction(
        tenantId, record, token, reason,
      ),
      'Retained evidence is queued for current source-linked reprojection.',
    )}>Reproject retained evidence</button>
}

function ReconcileAction({
  tenantId,
  record,
  actions,
  reason,
  externalTaskId,
}: Props) {
  const attempt = record.extractionAttempts[0]
  if (attempt?.status !==
      inventoryCodes.extractionStatus.reconciliationRequired) return null
  return <button className="secondary-button"
    disabled={actions.busy || !reason.trim()}
    onClick={() => void actions.run(
      token => inventoryApi.reconcileExtraction(
        tenantId,
        record,
        token,
        reason,
        externalTaskId.trim() || null,
      ),
      'The reconciliation decision is recorded.',
    )}>Reconcile</button>
}

function CancelAction({ tenantId, record, actions, reason }: Props) {
  const attempt = record.extractionAttempts[0]
  if (!attempt ||
      !cancellableStatuses.has(attempt.status)) return null
  return <button className="secondary-button"
    disabled={actions.busy || !reason.trim()}
    onClick={() => void actions.run(
      token => inventoryApi.cancelExtraction(
        tenantId, record, token, reason,
      ),
      'The extraction attempt is terminal and retained in history.',
    )}>Mark unrecoverable</button>
}

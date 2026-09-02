import { Navigate, useSearchParams } from 'react-router-dom'
import { z } from 'zod'
import { useSession } from '../auth/session-state'
import { useWorkspace } from '../auth/workspace-state'
import { ProposalFlowBinding } from '../campaign-flow/CampaignFlowBindings'
import { LoadingState, MessageState } from '../components/PageState'
import { FundingWorkspace, type FundingSelection } from '../funding/FundingWorkspace'
import {
  fundingAdministratorRoles,
  fundingViewerRoles,
  paymentStarterRoles,
  purchaseOrderSubmitterRoles,
} from '../funding/funding-roles'
import { useFundingWorkspace } from '../funding/useFundingWorkspace'

const selectionSchema = z.object({
  proposalVersionId: z.guid(),
  proposalOptionId: z.guid(),
  amountMinor: z.coerce.number().int().positive(),
  currency: z.string().trim().length(3).transform(value => value.toUpperCase()),
}).strict()

export function FundingPage() {
  const { selected, loading } = useWorkspace()
  const { session } = useSession()
  const [search] = useSearchParams()
  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  if (!session) return <Navigate to="/sign-in" replace />
  if (!fundingViewerRoles.has(selected.roleCode)) {
    return <MessageState title="Funding is not available"
      message="This workspace role cannot view or act on funding records." />
  }
  return <FundingRecord tenantId={selected.tenantId} token={session.antiforgeryToken}
    roleCode={selected.roleCode} selection={selectionFromSearch(search)} />
}

function FundingRecord({ tenantId, token, roleCode, selection }: {
  tenantId: string
  token: string
  roleCode: string
  selection: FundingSelection | null
}) {
  const model = useFundingWorkspace(tenantId)
  if (model.error && !model.workspace) {
    return <MessageState title="Funding could not be loaded" message={model.error} />
  }
  if (!model.workspace) return <LoadingState label="Loading funding records" />
  return <>
    {selection && <ProposalFlowBinding tenantId={tenantId}
      proposalId={selection.proposalVersionId} />}
    {model.error && <p className="inline-alert" role="alert">{model.error}</p>}
    <FundingWorkspace tenantId={tenantId} token={token} workspace={model.workspace}
      selection={selection} busy={model.busy} run={model.run}
      canSubmit={purchaseOrderSubmitterRoles.has(roleCode)}
      canAdminister={fundingAdministratorRoles.has(roleCode)}
      canStartPayment={paymentStarterRoles.has(roleCode)} />
  </>
}

function selectionFromSearch(search: URLSearchParams): FundingSelection | null {
  const parsed = selectionSchema.safeParse({
    proposalVersionId: search.get('proposalVersionId'),
    proposalOptionId: search.get('proposalOptionId'),
    amountMinor: search.get('amountMinor'),
    currency: search.get('currency'),
  })
  return parsed.success ? parsed.data : null
}

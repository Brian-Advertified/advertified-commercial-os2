import type { Proposal } from '../api/proposal-schemas'
import { CommercialValueProof, type CommercialProofMetric } from '../components/CommercialValueProof'

export function ProposalCommercialProof({ proposal }: { proposal: Proposal }) {
  return <CommercialValueProof title="The proposal now carries the planning proof forward"
    description="The client sees executable choices backed by approved plans and retained supply, instead of a disconnected PDF assembled from scratch."
    metrics={proposalProofMetrics(proposal)}
    note="These are retained commercial facts. Advertified does not claim reach, ROI or savings here unless the supporting campaign evidence exists." />
}

function proposalProofMetrics(proposal: Proposal): CommercialProofMetric[] {
  return [
    routeMetric(proposal),
    channelMetric(proposal),
    supplyMetric(proposal),
    decisionMetric(proposal),
  ]
}

function routeMetric(proposal: Proposal): CommercialProofMetric {
  const count = proposal.options.length
  return {
    label: 'Client routes', value: count,
    detail: count > 1 ? 'Materially different approved plan choices are available to compare.' : 'One approved executable plan is presented.',
    icon: 'plan', tone: 'violet',
    why: 'This is the number of retained proposal options linked to approved media-plan versions.',
  }
}

function channelMetric(proposal: Proposal): CommercialProofMetric {
  const count = new Set(proposal.options.flatMap(option => option.channels)).size
  return {
    label: 'Channels represented', value: count,
    detail: 'The proposal carries the approved media roles into client-facing choices.',
    icon: 'chart', tone: count > 1 ? 'blue' : 'neutral',
    why: 'This is a deduplicated count of the channels present across the retained proposal options.',
  }
}

function supplyMetric(proposal: Proposal): CommercialProofMetric {
  const inventory = proposal.options.flatMap(option => option.inventory)
  const products = new Set(inventory.map(item => item.productVersionId)).size
  const supplierNames = [...new Set(inventory.map(item => item.supplierName).filter(Boolean))]
  const sources = supplierNames.length || new Set(inventory.map(item => item.inventoryTenantId)).size
  return {
    label: 'Supplier breadth', value: `${sources} supplier${sources === 1 ? '' : 's'}`,
    detail: supplierNames.length > 0
      ? `${supplierNames.slice(0, 3).join(' · ')}${supplierNames.length > 3 ? ` · +${supplierNames.length - 3} more` : ''} · ${products} inventory version${products === 1 ? '' : 's'}`
      : `${products} distinct inventory product version${products === 1 ? '' : 's'} retained across the choices.`,
    icon: 'inventory', tone: sources > 1 ? 'positive' : 'neutral',
    why: 'Supplier names are carried from the exact selected shortlist into the media plan and proposal. Older proposal versions fall back to the inventory-owner count.',
  }
}

function decisionMetric(proposal: Proposal): CommercialProofMetric {
  if (proposal.decision?.optionId) return {
    label: 'Commercial decision', value: 'Client choice retained',
    detail: 'Funding and execution remain tied to the exact selected option.', icon: 'shield', tone: 'positive',
    why: 'The proposal has a persisted decision with an exact option identifier.',
  }
  return {
    label: 'Commercial decision', value: 'Awaiting client choice',
    detail: 'No client decision is invented before it is explicitly recorded.', icon: 'shield', tone: 'warning',
    why: 'The current proposal has no selected option identifier.',
  }
}

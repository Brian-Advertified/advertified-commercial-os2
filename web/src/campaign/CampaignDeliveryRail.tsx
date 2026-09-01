import type { Campaign } from '../api/campaign-schemas'
import { Icon } from '../components/Icon'
import {
  campaignDeliveryStages,
  type CampaignDeliveryTab,
} from './campaign-delivery-stages'

export function CampaignDeliveryRail({ campaign, activeTab, onSelect }: {
  campaign: Campaign
  activeTab: CampaignDeliveryTab
  onSelect: (tab: CampaignDeliveryTab) => void
}) {
  const stages = campaignDeliveryStages(campaign)
  return <nav className="delivery-stage-rail" aria-label="Campaign delivery progress">
    <div>{stages.map((stage, index) => <a href={`#${stage.id}`}
      aria-current={activeTab === stage.tab ? 'location' : undefined}
      aria-label={`${stage.label}${stage.current ? ', current lifecycle stage' : ''}`}
      className={`${stage.complete ? 'is-complete' : ''}${stage.current ? ' is-current' : ''}${activeTab === stage.tab ? ' is-selected' : ''}`}
      key={stage.id} onClick={() => onSelect(stage.tab)}>
      <span>{stage.complete ? '✓' : index + 1}</span><Icon name={stage.icon} />
      <span><strong>{stage.label}</strong><small>{stage.detail}</small></span>
    </a>)}</div>
  </nav>
}

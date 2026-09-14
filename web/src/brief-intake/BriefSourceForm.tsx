import { useState, type FormEvent } from 'react'
import { Icon } from '../components/Icon'
import { masterDataCodes } from '../generated/master-data-codes'

const campaignTypes = [
  [masterDataCodes.campaignModes.fullCampaign, 'Full Campaign', 'Multi-channel strategy', 'inventory'],
  [masterDataCodes.campaignModes.oohOnly, 'Out-of-Home', 'Billboards, transit, street furniture', 'plan'],
  [masterDataCodes.channels.digital, 'Digital & Social', 'Online, social media, display', 'commercial'],
  [masterDataCodes.channels.radio, 'Radio', 'Broadcast radio', 'reservation'],
  [masterDataCodes.channels.tv, 'Television', 'Television & connected TV', 'plan'],
  [masterDataCodes.channels.influencer, 'Influencers', 'Content creators & influencers', 'users'],
  [masterDataCodes.channels.print, 'Print', 'Newspapers & magazines', 'brief'],
  [masterDataCodes.channels.experiential, 'Experiential', 'Events, activations & brand experiences', 'target'],
] as const

const goals = [
  'Increase brand awareness',
  'Drive sales',
  'Generate leads',
  'Drive website traffic',
  'Change perception',
  'Other',
] as const

export function BriefSourceForm({ busy, source, onSubmit }: {
  busy: boolean
  source: { title: string; content: string }
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
}) {
  const [campaignTypeExplicit, setCampaignTypeExplicit] = useState(false)
  return <form className="brief-source-panel connected-brief-source" onSubmit={onSubmit}>
    <input type="hidden" name="campaignTypeExplicit" value={campaignTypeExplicit ? 'true' : ''} />
    <BriefInputTabs />
    <BriefSourceFields source={source} />
    <CampaignTypeChooser onExplicitSelection={() => setCampaignTypeExplicit(true)} />
    <BriefClarificationFields />
    <CampaignGoalChooser />
    <BriefSourceActions busy={busy} />
  </form>
}

function BriefInputTabs() {
  return <div className="connected-brief-tabs" role="tablist" aria-label="Brief input method">
    <button className="is-active" type="button" role="tab" aria-selected="true">
      <Icon name="brief" /> Type or Paste Brief
    </button>
    <button type="button" role="tab" aria-selected="false" disabled
      title="Document upload will use the governed file-ingestion path when enabled for campaign Briefs.">
      <Icon name="evidence" /> Upload Document
    </button>
    <button type="button" role="tab" aria-selected="false" disabled
      title="Examples are not inserted into live Briefs because the supplied request must remain the source of truth.">
      <Icon name="tasks" /> Use Example
    </button>
  </div>
}

function BriefSourceFields({ source }: { source: { title: string; content: string } }) {
  return <>
    <label className="connected-brief-title-field">Campaign or Brief name
      <input name="sourceTitle" required maxLength={300} defaultValue={source.title}
        placeholder="For example: Summer brand campaign" />
    </label>
    <label className="connected-brief-textarea"><span className="sr-only">Client requirement</span>
      <textarea name="sourceContent" required rows={7} maxLength={262144}
        defaultValue={source.content}
        placeholder="e.g. We are launching a new product in South Africa and want to build awareness and drive sales among young professionals. Include the objective, audience, locations, timing, budget and any required or excluded channels…" />
      <small>The original wording is preserved exactly. Structured choices below are retained separately as your clarifications.</small>
    </label>
  </>
}

function CampaignTypeChooser({ onExplicitSelection }: { onExplicitSelection: () => void }) {
  return <fieldset className="connected-campaign-type"><legend>Campaign type</legend><div>
    {campaignTypes.map(([value, label, copy, icon], index) => <label key={value}
      className="connected-campaign-type-card"><input type="radio" name="campaignType" value={value}
        defaultChecked={index === 0} onClick={onExplicitSelection} /><span className="connected-campaign-type-check">✓</span>
      <Icon name={icon} /><strong>{label}</strong><small>{copy}</small></label>)}
  </div></fieldset>
}

function BriefClarificationFields() {
  return <div className="connected-brief-meta-grid">
    <BriefBudgetField />
    <label>Flight period<input name="timingClarification" placeholder="e.g. Oct 2026 – Dec 2026" /></label>
    <label>Target provinces / cities<input name="geographyClarification" placeholder="e.g. Gauteng, Western Cape" /></label>
  </div>
}

function BriefBudgetField() {
  const [selection, setSelection] = useState('')
  return <div><label>Budget range (ZAR)<select name="budgetClarification" value={selection}
    onChange={event => setSelection(event.target.value)}>
    <option value="">Not specified</option>
    <option value="ZAR 10,000 to ZAR 100,000">R10,000 – R100,000</option>
    <option value="ZAR 100,000 to ZAR 500,000">R100,000 – R500,000</option>
    <option value="ZAR 500,000 to ZAR 1,000,000">R500,000 – R1,000,000</option>
    <option value="ZAR 1,000,000 or more">R1,000,000+</option>
    <option value="confirmed">Enter a confirmed amount</option>
  </select></label>{selection === 'confirmed' && <label>Confirmed budget (ZAR)
    <input name="budgetExactClarification" type="number" min="0" step="0.01" required />
    <small>This amount is retained as your clarification, without changing the original brief.</small>
  </label>}</div>
}

function CampaignGoalChooser() {
  return <fieldset className="connected-goal-grid"><legend>Campaign goals <span>(select up to 3)</span></legend>
    <div>{goals.map(goal => <label key={goal}><input type="checkbox" name="campaignGoal" value={goal} />
      <span>{goal}</span></label>)}</div></fieldset>
}

function BriefSourceActions({ busy }: { busy: boolean }) {
  return <div className="brief-source-actions connected-brief-actions">
    <button className="secondary-button" type="button" disabled={busy}>Cancel</button>
    <button className="primary-button" type="submit" disabled={busy}>
      {busy ? 'Analysing your brief…' : 'Next: AI Interpretation'} {!busy && <Icon name="arrow" />}
    </button>
  </div>
}

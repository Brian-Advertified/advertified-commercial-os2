import type { SuppliedBriefUnderstanding } from '../api/brief-understanding-schemas'

type Props = {
  understanding: SuppliedBriefUnderstanding | null
  busy: boolean
}

const actions = [
  'Analyse your brief and extract key objectives',
  'Identify the right audiences and locations',
  'Recommend the best mix of channels',
  'Create a strategic, data-led media plan',
  'Suggest optimal timing and budget allocation',
  'Flag opportunities and potential risks',
] as const

export function BriefIntakeGuide({ understanding, busy }: Props) {
  const active = busy || understanding !== null
  return <aside className="brief-intake-guide connected-assistant-panel" aria-label="Your AI Campaign Assistant">
    <header><span className="connected-ai-orb" aria-hidden="true">✦</span><div>
      <h2>Your AI Campaign Assistant</h2>
      <p>{active ? 'I’m analysing the supplied Brief and keeping every conclusion tied to source evidence.'
        : 'I’ll interpret your Brief and build a campaign planning path from the information you provide.'}</p>
    </div></header>
    <div className="connected-assistant-rule" />
    <strong>Here’s what I’ll do:</strong>
    <ul>{actions.map(action => <li key={action}><span>✓</span>{action}</li>)}</ul>
    <section className="connected-sa-proof"><span className="connected-sa-flag" aria-hidden="true">🇿🇦</span><div>
      <strong>Built for South Africa</strong>
      <p>Local market intelligence. Real audience data. Verified inventory. Better results.</p>
    </div></section>
    <div className="connected-assistant-image" aria-hidden="true"><span>“Great brands meet people<br />where life happens.”</span></div>
  </aside>
}

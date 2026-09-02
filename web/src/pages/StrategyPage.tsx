import { useEffect, useState } from 'react'
import { Link, Navigate, useParams } from 'react-router-dom'
import { humanMessage } from '../api/client'
import { opportunityApi } from '../api/opportunity-client'
import type { Strategy } from '../api/schemas'
import { useWorkspace } from '../auth/workspace-state'
import { LoadingState, MessageState } from '../components/PageState'
import { humanizeCode } from '../presentation/format'

type StrategyArtifact = {
  diagnosis: string
  growthThesis: string
  objectives: string[]
  audiences: string[]
  proposition: string
  message: string
  channelImplications: string[]
  risks: string[]
}

export function StrategyPage() {
  const { selected, loading } = useWorkspace()
  const { strategyId } = useParams()
  const [strategy, setStrategy] = useState<Strategy | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!selected || !strategyId) return
    let active = true
    void opportunityApi.getStrategy(selected.tenantId, strategyId)
      .then(value => { if (active) setStrategy(value) })
      .catch((failure: unknown) => { if (active) setError(humanMessage(failure)) })
    return () => { active = false }
  }, [selected, strategyId])

  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  if (!strategyId) return <Navigate to="/opportunities" replace />
  if (error) return <MessageState title="Strategy could not be opened" message={error} />
  if (!strategy) return <LoadingState label="Loading strategy" />
  return <StrategyWorkspace strategy={strategy} />
}

function StrategyWorkspace({ strategy }: { strategy: Strategy }) {
  const artifact = parseStrategy(strategy.artifactJson)
  const unresolved = strategy.objections.filter(item => !item.resolution)
  return <section className="approved-strategy-page" aria-labelledby="strategy-title">
    <div className="approved-strategy-pagebar"><Link className="text-action" to={`/opportunities/${strategy.opportunityId}`}>← Opportunity</Link>
      <span className="approved-ai-validated">✦ AI researched · human validation required</span></div>
    <header className="approved-strategy-header"><div><p className="eyebrow">Strategy & STP</p>
      <h1 id="strategy-title">Campaign strategy</h1><p>{artifact.diagnosis}</p></div>
      <span className={`approved-strategy-status ${unresolved.length ? 'needs-review' : ''}`}>{humanizeCode(strategy.status, true)}</span></header>
    <div className="approved-strategy-layout">
      <aside className="approved-strategy-nav">
        {['Summary', 'Segmentation', 'Targeting', 'Positioning', 'Insights', 'Review'].map((label, index) =>
          <a key={label} href={`#strategy-${label.toLowerCase()}`} className={index === 0 ? 'is-active' : ''}><span>{index + 1}</span>{label}</a>)}
      </aside>
      <main className="approved-strategy-content">
        <section className="approved-strategy-summary" id="strategy-summary"><header><div><h2>Strategy summary</h2><p>Evidence-backed campaign direction before media allocation.</p></div></header>
          <div className="approved-strategy-summary-grid"><article><span>Business diagnosis</span><p>{artifact.diagnosis}</p></article>
            <article><span>Growth opportunity</span><p>{artifact.growthThesis}</p></article></div></section>
        <section className="approved-strategy-audiences" id="strategy-segmentation"><header><h2>Who we will reach</h2><span>{artifact.audiences.length} audience hypothesis{artifact.audiences.length === 1 ? '' : 'es'}</span></header>
          <div>{artifact.audiences.length ? artifact.audiences.map((audience, index) => <article key={`${audience}-${index}`}>
            <span className={`approved-audience-icon tone-${(index % 3) + 1}`}>♙</span><div><small>{index === 0 ? 'Primary Audience' : index === 1 ? 'Secondary Audience' : 'Additional Audience'}</small><strong>{audience}</strong><em>Evidence-backed hypothesis</em></div></article>) :
            <p className="approved-empty">No audience hypotheses are recorded.</p>}</div></section>
        <div className="approved-strategy-bottom-grid">
          <section className="approved-positioning-card" id="strategy-positioning"><header><h2>Positioning Statement</h2><span>Edit</span></header><p>{artifact.proposition}</p><blockquote>{artifact.message}</blockquote></section>
          <section className="approved-pillars-card" id="strategy-insights"><header><h2>Campaign Pillars</h2><span>Edit</span></header><div>{artifact.objectives.map(item => <span key={item}>{item}</span>)}</div></section>
        </div>
      </main>
      <aside className="approved-strategy-insights">
        <article><header><h2>Key Insights</h2></header><ul>{artifact.channelImplications.map(item => <li key={item}>✓ {item}</li>)}</ul></article>
        <article id="strategy-review"><header><h2>Review</h2><span>{strategy.objections.length}</span></header>
          {unresolved.length === 0 ? <div className="approved-review-clear">✓ No unresolved material objections</div> :
            <div className="approved-strategy-objections">{unresolved.map(item => <div key={item.id}><strong>{humanizeCode(item.severity, true)}</strong><p>{item.evidenceGap}</p><small>{item.recommendedResolution}</small></div>)}</div>}
          {artifact.risks.length > 0 && <details><summary>Risks and caveats</summary><ul>{artifact.risks.map(item => <li key={item}>{item}</li>)}</ul></details>}
        </article>
      </aside>
    </div>
  </section>
}

function parseStrategy(value: string): StrategyArtifact {
  try {
    const data = JSON.parse(value) as Record<string, unknown>
    return {
      diagnosis: text(data.diagnosis),
      growthThesis: text(data.growth_thesis ?? data.growthThesis),
      objectives: list(data.objectives),
      audiences: list(data.audience_hypotheses ?? data.audienceHypotheses),
      proposition: text(data.proposition),
      message: text(data.message),
      channelImplications: list(data.channel_implications ?? data.channelImplications),
      risks: list(data.risks),
    }
  } catch {
    return { diagnosis: value, growthThesis: 'Not supplied', objectives: [], audiences: [], proposition: 'Not supplied', message: 'Not supplied', channelImplications: [], risks: [] }
  }
}
function text(value: unknown) { return typeof value === 'string' && value.trim() ? value : 'Not supplied' }
function list(value: unknown) { return Array.isArray(value) ? value.map(String).filter(Boolean) : [] }

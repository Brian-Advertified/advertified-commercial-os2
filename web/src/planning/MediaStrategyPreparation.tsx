import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { humanMessage } from '../api/client'
import { mediaStrategyApi, type MediaStrategyRecord } from '../api/media-strategy-client'
import { planningApi } from '../api/planning-client'
import { LoadingState } from '../components/PageState'
import { masterDataCodes } from '../generated/master-data-codes'
import { notifications } from '../notifications/notifications'
import { mediaVisual } from './media-visuals'

type Props = {
  tenantId: string; briefVersionId: string; briefId: string; token: string; busy: boolean
  act: (action: () => Promise<unknown>) => Promise<void>
}

export function MediaStrategyPreparation(props: Props) {
  const [strategy, setStrategy] = useState<MediaStrategyRecord | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  useEffect(() => {
    let active = true
    void mediaStrategyApi.getLatest(props.tenantId, props.briefVersionId)
      .then(value => { if (active) setStrategy(value) })
      .catch((failure: unknown) => { if (active) setError(humanMessage(failure)) })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [props.tenantId, props.briefVersionId])
  if (loading) return <LoadingState label="Loading strategy recommendations" />
  if (error) return <p className="inline-alert" role="alert">{error}</p>
  const analyse = () => props.act(async () => {
    const result = await mediaStrategyApi.analyse(props.tenantId, props.briefVersionId, props.token)
    setStrategy(result)
    notifications.information(result.details.channelRecommendations.length
      ? 'Strategy recommendations are ready for your review.'
      : 'No channel recommendation is available. Review the strategy gaps before continuing.')
  })
  if (!strategy) return <StrategyStart busy={props.busy} onAnalyse={analyse} />
  const approve = () => props.act(async () => {
    setStrategy(await mediaStrategyApi.approve(props.tenantId, props.briefVersionId, strategy, props.token))
    notifications.success('The reviewed strategy version is approved.')
  })
  return <StrategyReview {...props} strategy={strategy} onAnalyse={analyse} onApprove={approve} />
}

function StrategyStart({ busy, onAnalyse }: { busy: boolean; onAnalyse: () => Promise<void> }) {
  return <section className="connected-strategy-empty"><span className="connected-ai-orb">✦</span><div>
    <h2>Build the recommended media strategy</h2>
    <p>Use the approved audience and campaign brief to recommend channel roles. Review the recommendation before creating a media allocation.</p>
  </div><button className="primary-button" type="button" disabled={busy} onClick={() => void onAnalyse()}>
    {busy ? 'Analysing strategy…' : 'Generate strategy recommendations'}</button></section>
}

function StrategyReview(props: Props & {
  strategy: MediaStrategyRecord; onAnalyse: () => Promise<void>; onApprove: () => Promise<void>
}) {
  const { details } = props.strategy
  const hasRecommendations = details.channelRecommendations.length > 0
  const approved = props.strategy.status === masterDataCodes.lifecycleStatuses.approved
  const percentages = details.channelRecommendations.map(item => item.budgetGuidancePercent)
  const hasAllocation = hasRecommendations && percentages.every(value => value !== null)
    && Math.abs(percentages.reduce<number>((total, value) => total + (value ?? 0), 0) - 100) < 0.000001
  return <div className="connected-strategy-grid"><section className="connected-strategy-main-card">
    <header><div><h2>Review channel recommendations</h2><p>{details.summary}</p></div></header>
    <div className="connected-strategy-table">
      {details.channelRecommendations.map(item => <article key={item.channel}>
        <h3>{mediaVisual(item.channel).label}</h3><strong>{item.role}</strong>
        <p>{item.objectiveContribution}</p><p>{item.rationale}</p>
        <p>{item.budgetGuidancePercent === null ? 'Budget allocation not established'
          : `${item.budgetGuidancePercent}% recommended allocation`}</p>
        {item.tradeOffs.map(value => <p key={value}>{value}</p>)}
      </article>)}
      {!hasRecommendations && <p role="status">No channel recommendation is available yet. Strategy must be reviewed before a media allocation can be created.</p>}
      {hasRecommendations && !hasAllocation && <p role="status">A confirmed budget and a complete allocation are required before media planning.</p>}
    </div>
    <footer><Link className="secondary-button" to={`/stp/${props.briefVersionId}`}>← Back: Audience &amp; STP</Link>
      <StrategyReviewAction {...props} hasRecommendations={hasRecommendations}
        hasAllocation={hasAllocation} approved={approved} /></footer>
  </section><aside className="connected-strategy-side"><article><h2>Strategy evidence &amp; gaps</h2>
    {details.strategicPrinciples.map(value => <p key={value}>{value}</p>)}
    {[...new Set([...details.evidenceGaps, ...props.strategy.unknowns])].map(value => <p key={value}>{value}</p>)}
    <Link to={`/briefs/${props.briefId}`}>Review campaign brief</Link>
  </article></aside></div>
}

function StrategyReviewAction(props: Props & {
  hasRecommendations: boolean; hasAllocation: boolean; approved: boolean
  onAnalyse: () => Promise<void>; onApprove: () => Promise<void>
}) {
  if (!props.hasRecommendations) return <button className="secondary-button"
    type="button" disabled={props.busy} onClick={() => void props.onAnalyse()}>Regenerate strategy recommendations</button>
  if (!props.approved) return <button className="primary-button" type="button" disabled={props.busy}
    onClick={() => void props.onApprove()}>{props.busy ? 'Approving…' : 'Approve channel recommendations'}</button>
  if (!props.hasAllocation) return <>
    <Link className="secondary-button" to={`/briefs/${props.briefId}`}>Review budget before allocation</Link>
    <button className="primary-button" type="button" disabled={props.busy}
      onClick={() => void props.onAnalyse()}>
      {props.busy ? 'Preparing revised recommendations…' : 'Create revised strategy recommendations'}</button>
  </>
  return <button className="primary-button" type="button" disabled={props.busy}
    onClick={() => void props.act(() => planningApi.generateMix(props.tenantId, props.briefVersionId, props.token))}>
    {props.busy ? 'Building allocation…' : 'Build media allocation'}</button>
}

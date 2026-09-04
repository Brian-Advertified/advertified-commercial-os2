import { useEffect, useState } from 'react'
import { Navigate } from 'react-router-dom'
import { agentOperationsApi } from '../api/agent-operations-client'
import type { AgentBudget, AgentOperationalRun, AgentOperations, AgentUsage }
  from '../api/agent-operations-schemas'
import { humanMessage } from '../api/client'
import { useWorkspace } from '../auth/workspace-state'
import { LoadingState, MessageState } from '../components/PageState'
import { operationalCopy } from '../content/operational-copy'
import { SettingsNavigation } from '../components/SettingsNavigation'
import { masterDataCodes } from '../generated/master-data-codes'
import { formatDateTime, formatMoney, formatNumber, humanizeCode } from '../presentation/format'

const administratorRoles = new Set<string>([
  masterDataCodes.roles.platformAdmin,
  masterDataCodes.roles.agencyAdmin,
])

export function AgentOperationsPage() {
  const { selected, loading } = useWorkspace()
  const [operations, setOperations] = useState<AgentOperations | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!selected || !administratorRoles.has(selected.roleCode)) return
    let active = true
    void agentOperationsApi.get(selected.tenantId)
      .then(value => { if (active) setOperations(value) })
      .catch((failure: unknown) => { if (active) setError(humanMessage(failure)) })
    return () => { active = false }
  }, [selected])

  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  if (!administratorRoles.has(selected.roleCode)) return <MessageState
    title="Agent operations are not available"
    message="Only an agency or platform administrator can view AI budgets and costs." />
  if (error) return <MessageState title="Agent operations could not be opened" message={error} />
  if (!operations) return <LoadingState label="Loading agent budgets and costs" />
  return <AgentOperationsWorkspace operations={operations} />
}

function AgentOperationsWorkspace({ operations }: { operations: AgentOperations }) {
  const providerState = operations.liveProviderEnabled ? 'Live provider enabled' : 'Paid AI disabled'
  return <section className="operations-page agent-operations-page" aria-labelledby="agent-operations-title">
    <SettingsNavigation />
    <header className="operations-command-header"><div>
      <p className="eyebrow">AI governance and cost oversight</p>
      <h1 id="agent-operations-title">Agent operations</h1>
      <p>Review each specialist agent’s current per-run cap and tenant-attributable usage.</p>
    </div><span className="operations-state-label">Read only</span></header>
    <dl className="operations-context-strip operations-context-four">
      <div><dt>Provider policy</dt><dd>{providerState}</dd></div>
      <div><dt>Recorded cost</dt><dd>{formatMoney(
        operations.totalIncrementalCostMinor, operations.currency)}</dd></div>
      <div><dt>Durable runs</dt><dd>{formatNumber(operations.durableRunCount)}</dd></div>
      <div><dt>Needs attention</dt><dd>{formatNumber(operations.attentionRunCount)}</dd></div>
    </dl>
    {!operations.liveProviderEnabled && <p className="inline-alert" role="status">
      {operationalCopy.providerDisabled}
    </p>}
    <AgentBudgetTable agents={operations.agents} currency={operations.currency} />
    <UsageTable usage={operations.recentUsage} currency={operations.currency} />
    <RunTable runs={operations.recentRuns} currency={operations.currency} />
  </section>
}

function AgentBudgetTable({ agents, currency }: { agents: AgentBudget[]; currency: string }) {
  return <section className="operations-panel" aria-labelledby="agent-budget-title">
    <header className="operations-panel-header"><div><p className="eyebrow">Current controls</p>
      <h2 id="agent-budget-title">Agent budgets and costs</h2></div>
      <span>Per provider attempt</span></header>
    <div className="operations-table-scroll"><table className="operations-table">
      <thead><tr><th>Agent</th><th>Provider / model</th><th>Cost cap</th>
        <th>Recorded cost</th><th>Usage</th><th>Last used</th></tr></thead>
      <tbody>{agents.map(agent => <tr key={agent.agentCode}>
        <td><strong>{agent.displayLabel}</strong><small>{humanizeCode(agent.agentCode)}</small></td>
        <td>{humanizeCode(agent.provider, true)}<small>{agent.model}</small></td>
        <td>{formatMoney(agent.costCapMinor, currency)}</td>
        <td>{formatMoney(agent.incrementalCostMinor, currency)}</td>
        <td>{formatNumber(agent.usageCount)}</td>
        <td>{agent.lastUsedAtUtc ? formatDateTime(agent.lastUsedAtUtc) : 'Not used'}</td>
      </tr>)}</tbody>
    </table></div>
  </section>
}

function UsageTable({ usage, currency }: { usage: AgentUsage[]; currency: string }) {
  return <section className="operations-panel" aria-labelledby="agent-usage-title">
    <header className="operations-panel-header"><div><p className="eyebrow">Tenant ledger</p>
      <h2 id="agent-usage-title">Recent recorded usage</h2></div><span>Newest first</span></header>
    {usage.length === 0 ? <EmptyState title="No agent usage has been recorded"
      detail="Usage will appear after a governed agent creates an attributed result." />
      : <div className="operations-table-scroll"><table className="operations-table">
        <thead><tr><th>Agent / work</th><th>Provider / model</th><th>Units</th>
          <th>Tools</th><th>Cost</th><th>Recorded</th></tr></thead>
        <tbody>{usage.map(item => <UsageRow key={item.id} item={item} currency={currency} />)}</tbody>
      </table></div>}
  </section>
}

function UsageRow({ item, currency }: { item: AgentUsage; currency: string }) {
  return <tr><td><strong>{humanizeCode(item.agentCode, true)}</strong>
    <small>{humanizeCode(item.workType, true)} · {humanizeCode(item.status, true)}</small></td>
    <td>{humanizeCode(item.provider, true)}<small>{item.model}</small></td>
    <td>{item.units === null ? 'Not recorded' : formatNumber(item.units)}</td>
    <td>{item.toolCalls === null ? 'Not recorded' : formatNumber(item.toolCalls)}</td>
    <td>{formatMoney(item.incrementalCostMinor, currency)}</td>
    <td>{formatDateTime(item.recordedAtUtc)}</td></tr>
}

function RunTable({ runs, currency }: { runs: AgentOperationalRun[]; currency: string }) {
  return <section className="operations-panel" aria-labelledby="agent-runs-title">
    <header className="operations-panel-header"><div><p className="eyebrow">Recoverable work</p>
      <h2 id="agent-runs-title">Recent durable runs</h2></div><span>Newest first</span></header>
    {runs.length === 0 ? <EmptyState title="No durable agent runs yet"
      detail="Queued and recoverable workflow runs will appear here." />
      : <div className="operations-table-scroll"><table className="operations-table">
        <thead><tr><th>Workflow</th><th>Status</th><th>Current step</th>
          <th>Attempts</th><th>Cost</th><th>Updated</th></tr></thead>
        <tbody>{runs.map(run => <tr key={run.id}>
          <td><strong>{humanizeCode(run.runKind, true)}</strong>
            {run.errorCode && <small>{humanizeCode(run.errorCode, true)}</small>}</td>
          <td>{humanizeCode(run.status, true)}</td>
          <td>{run.currentStep ? humanizeCode(run.currentStep, true) : 'Complete'}</td>
          <td>{formatNumber(run.attempts)}</td>
          <td>{formatMoney(run.incrementalCostMinor, currency)}</td>
          <td>{formatDateTime(run.updatedAtUtc)}</td>
        </tr>)}</tbody>
      </table></div>}
  </section>
}

function EmptyState({ title, detail }: { title: string; detail: string }) {
  return <div className="operations-empty-row"><strong>{title}</strong><p>{detail}</p></div>
}

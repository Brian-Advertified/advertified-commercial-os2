import { useLocation, useNavigate } from 'react-router-dom'
import { useWorkspace } from '../auth/workspace-state'
import { Icon } from '../components/Icon'
import { LoadingState, MessageState } from '../components/PageState'
import { publicReturnPath } from '../routing/return-path'

export function WorkspacesPage() {
  const { workspaces, loading, error, select, reload } = useWorkspace()
  const location = useLocation()
  const navigate = useNavigate()
  const returnTo = publicReturnPath(location.search)

  if (loading) return <LoadingState label="Finding your workspaces" />
  if (error) {
    return <MessageState title="We could not load your workspaces" message={error}
      action={<button className="secondary-button" onClick={() => void reload()}>Try again</button>} />
  }
  if (workspaces.length === 0) {
    return <MessageState title="No active workspace is available"
      message="Your identity is valid, but it has no active workspace membership. Ask an administrator to review your access." />
  }

  function choose(workspace: (typeof workspaces)[number]) {
    select(workspace)
    navigate(returnTo ?? '/home')
  }

  return <section className="operations-page operations-workspace-page" aria-labelledby="workspace-title">
    <header className="operations-command-header">
      <div><p className="eyebrow">Workspace access</p>
        <h1 id="workspace-title">Where are you working today?</h1>
        <p>Select the organisation whose data, permissions and work queue you need.</p></div>
    </header>
    <dl className="operations-context-strip operations-workspace-context">
      <div><dt>Available workspaces</dt><dd>{workspaces.length}</dd></div>
      <div><dt>Access boundary</dt><dd>One workspace at a time</dd></div>
    </dl>
    <section className="operations-panel operations-workspace-selector" aria-labelledby="workspace-list-title">
      <header className="operations-panel-header"><div><p className="eyebrow">Organisation access</p>
        <h2 id="workspace-list-title">Choose a workspace</h2></div>
        <span>{workspaces.length} available</span></header>
      <div className="operations-workspace-list">
        {workspaces.map((workspace) => <button type="button" key={workspace.membershipId}
          onClick={() => choose(workspace)}>
          <span className="workspace-avatar" aria-hidden="true">{workspace.name.charAt(0)}</span>
          <span><strong>{workspace.name}</strong><small>{formatRole(workspace.roleCode)}</small></span>
          <span><small>Open workspace</small><Icon name="arrow" /></span>
        </button>)}
      </div>
    </section>
  </section>
}

function formatRole(value: string): string {
  return value.split('_').map((part) => part.charAt(0).toUpperCase() + part.slice(1)).join(' ')
}

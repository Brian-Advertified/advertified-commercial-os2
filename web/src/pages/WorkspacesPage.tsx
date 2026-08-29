import { useNavigate } from 'react-router-dom'
import { useWorkspace } from '../auth/workspace-state'
import { Icon } from '../components/Icon'
import { LoadingState, MessageState } from '../components/PageState'

export function WorkspacesPage() {
  const { workspaces, loading, error, select, reload } = useWorkspace()
  const navigate = useNavigate()

  if (loading) return <LoadingState label="Finding your workspaces" />
  if (error) {
    return (
      <MessageState
        title="We could not load your workspaces"
        message={error}
        action={<button className="secondary-button" onClick={() => void reload()}>Try again</button>}
      />
    )
  }
  if (workspaces.length === 0) {
    return (
      <MessageState
        title="No active workspace is available"
        message="Your identity is valid, but it has no active workspace membership. Ask an administrator to review your access."
      />
    )
  }

  function choose(workspace: (typeof workspaces)[number]) {
    select(workspace)
    navigate('/home')
  }

  return (
    <section aria-labelledby="workspace-title">
      <header className="page-heading">
        <div>
          <p className="eyebrow">Workspace access</p>
          <h1 id="workspace-title">Where are you working today?</h1>
          <p>Choose one of your active organisations. You can switch at any time.</p>
        </div>
      </header>
      <div className="workspace-grid">
        {workspaces.map((workspace) => (
          <button
            type="button"
            className="workspace-card"
            key={workspace.membershipId}
            onClick={() => choose(workspace)}
          >
            <span className="workspace-avatar" aria-hidden="true">{workspace.name.charAt(0)}</span>
            <span><strong>{workspace.name}</strong><small>{formatRole(workspace.roleCode)}</small></span>
            <Icon name="arrow" />
          </button>
        ))}
      </div>
    </section>
  )
}

function formatRole(value: string): string {
  return value.split('_').map((part) => part.charAt(0).toUpperCase() + part.slice(1)).join(' ')
}

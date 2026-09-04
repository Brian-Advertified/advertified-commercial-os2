import { useEffect, useState } from 'react'
import { Navigate } from 'react-router-dom'
import { api, humanMessage } from '../api/client'
import type { CurrentUser } from '../api/schemas'
import { useSession } from '../auth/session-state'
import { useWorkspace } from '../auth/workspace-state'
import { LoadingState, MessageState } from '../components/PageState'
import { ProfileEditor } from '../components/ProfileEditor'
import { operationalCopy } from '../content/operational-copy'

function useCurrentUser() {
  const [user, setUser] = useState<CurrentUser | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    let active = true
    void api.getCurrentUser().then(({ user: result }) => {
      if (active) setUser(result)
    }).catch((failure: unknown) => {
      if (active) setError(humanMessage(failure))
    }).finally(() => {
      if (active) setLoading(false)
    })
    return () => { active = false }
  }, [])
  return { user, setUser, error, loading }
}

export function ProfilePage() {
  const { session } = useSession()
  const { selected, loading: workspaceLoading } = useWorkspace()
  const profile = useCurrentUser()

  if (workspaceLoading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  if (profile.loading) return <LoadingState label="Loading your profile" />
  if (profile.error || !profile.user || !session) {
    return <MessageState title="Your profile could not be loaded"
      message={profile.error ?? 'Try again.'} />
  }

  return <section className="operations-page operations-profile-page" aria-labelledby="profile-title">
    <header className="operations-command-header">
      <div><p className="eyebrow">Personal details</p>
        <h1 id="profile-title">Your profile</h1>
        <p>Maintain the identity details your team uses inside the selected workspace.</p></div>
    </header>
    <dl className="operations-context-strip">
      <div><dt>Workspace</dt><dd>{selected.name}</dd></div>
      <div><dt>Workspace role</dt><dd>{formatRole(selected.roleCode)}</dd></div>
      <div><dt>Identity protection</dt><dd>{profile.user.mfaEnabled
        ? 'MFA enabled' : operationalCopy.mfaNotConfirmed}</dd></div>
    </dl>
    <section className="operations-panel operations-profile-workspace" aria-labelledby="profile-details-title">
      <header className="operations-panel-header"><div><p className="eyebrow">Identity record</p>
        <h2 id="profile-details-title">Profile details</h2></div>
        <span>Version {profile.user.version}</span></header>
      <ProfileEditor initialUser={profile.user} tenantId={selected.tenantId}
        antiforgeryToken={session.antiforgeryToken} onUpdated={profile.setUser} />
    </section>
  </section>
}

function formatRole(value: string): string {
  return value.split('_').map((part) => part.charAt(0).toUpperCase() + part.slice(1)).join(' ')
}

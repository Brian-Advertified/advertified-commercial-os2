import { useEffect, useState } from 'react'
import { Navigate } from 'react-router-dom'
import { api, humanMessage } from '../api/client'
import type { CurrentUser } from '../api/schemas'
import { useSession } from '../auth/session-state'
import { useWorkspace } from '../auth/workspace-state'
import { LoadingState, MessageState } from '../components/PageState'
import { ProfileEditor } from '../components/ProfileEditor'

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
    return (
      <MessageState
        title="Your profile could not be loaded"
        message={profile.error ?? 'Try again.'}
      />
    )
  }

  return (
    <section aria-labelledby="profile-title">
      <header className="page-heading">
        <p className="eyebrow">Personal details</p>
        <h1 id="profile-title">Your profile</h1>
        <p>Keep the details your team uses to identify you accurate.</p>
      </header>
      <ProfileEditor
        initialUser={profile.user}
        tenantId={selected.tenantId}
        antiforgeryToken={session.antiforgeryToken}
        onUpdated={profile.setUser}
      />
    </section>
  )
}

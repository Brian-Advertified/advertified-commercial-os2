import { useCallback, useEffect, useMemo, useState } from 'react'
import { api, clearPendingCommandKeys, humanMessage, sessionExpiredEvent } from '../api/client'
import type { BrowserSession } from '../api/schemas'
import { SessionContext } from './session-state'

function useSessionExpiry(reload: () => Promise<void>) {
  useEffect(() => {
    const handleExpiry = () => { void reload() }
    window.addEventListener(sessionExpiredEvent, handleExpiry)
    return () => window.removeEventListener(sessionExpiredEvent, handleExpiry)
  }, [reload])
}

function useSessionStore() {
  const [session, setSession] = useState<BrowserSession | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const reload = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      setSession(await api.getSession())
    } catch (failure) {
      setError(humanMessage(failure))
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    let active = true
    void api.getSession().then((result) => {
      if (active) setSession(result)
    }).catch((failure: unknown) => {
      if (active) setError(humanMessage(failure))
    }).finally(() => {
      if (active) setLoading(false)
    })
    return () => { active = false }
  }, [])
  useSessionExpiry(reload)
  return { session, setSession, loading, setLoading, error, setError, reload }
}

function useSessionActions(store: ReturnType<typeof useSessionStore>) {
  const { session, setSession, setLoading, setError } = store
  const signIn = useCallback(async (returnTo?: string) => {
    if (!session) return false
    if (session.signInPath) {
      const destination = new URL(session.signInPath, window.location.origin)
      destination.searchParams.set('returnTo', returnTo ?? '/workspaces')
      window.location.assign(destination.toString())
      return true
    }
    setLoading(true)
    setError(null)
    try {
      await api.signIn(session.antiforgeryToken)
      setSession(await api.getSession())
      return false
    } catch (failure) {
      setError(humanMessage(failure))
      throw failure
    } finally {
      setLoading(false)
    }
  }, [session, setError, setLoading, setSession])

  const signOut = useCallback(async () => {
    if (!session) return false
    clearPendingCommandKeys()
    sessionStorage.removeItem('advertified.workspace')
    if (session.signOutPath) {
      const redirectUrl = await api.signOutOidc(
        session.signOutPath,
        session.antiforgeryToken,
      )
      window.location.assign(redirectUrl)
      return true
    }
    setLoading(true)
    try {
      await api.signOut(session.antiforgeryToken)
      setSession(await api.getSession())
      return false
    } finally {
      setLoading(false)
    }
  }, [session, setLoading, setSession])
  return { signIn, signOut }
}

export function SessionProvider({ children }: { children: React.ReactNode }) {
  const store = useSessionStore()
  const actions = useSessionActions(store)
  const value = useMemo(
    () => ({ session: store.session, loading: store.loading, error: store.error,
      signIn: actions.signIn, signOut: actions.signOut, reload: store.reload }),
    [actions.signIn, actions.signOut, store.error, store.loading, store.reload, store.session],
  )
  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>
}

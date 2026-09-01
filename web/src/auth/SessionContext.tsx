import { useCallback, useEffect, useMemo, useState } from 'react'
import { api, humanMessage, sessionExpiredEvent } from '../api/client'
import type { BrowserSession } from '../api/schemas'
import { SessionContext } from './session-state'

function useSessionExpiry(reload: () => Promise<void>) {
  useEffect(() => {
    const handleExpiry = () => { void reload() }
    window.addEventListener(sessionExpiredEvent, handleExpiry)
    return () => window.removeEventListener(sessionExpiredEvent, handleExpiry)
  }, [reload])
}

export function SessionProvider({ children }: { children: React.ReactNode }) {
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
  }, [session])

  const signOut = useCallback(async () => {
    if (!session) return false
    sessionStorage.removeItem('advertified.workspace')
    if (session.signOutPath) {
      window.location.assign(session.signOutPath)
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
  }, [session])

  const value = useMemo(
    () => ({ session, loading, error, signIn, signOut, reload }),
    [error, loading, reload, session, signIn, signOut],
  )
  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>
}

import { createContext, useContext } from 'react'
import type { BrowserSession } from '../api/schemas'

export type SessionState = {
  session: BrowserSession | null
  loading: boolean
  error: string | null
  signIn: (returnTo?: string) => Promise<boolean>
  signOut: () => Promise<boolean>
  reload: () => Promise<void>
}

export const SessionContext = createContext<SessionState | null>(null)

export function useSession(): SessionState {
  const context = useContext(SessionContext)
  if (!context) throw new Error('SessionProvider is required.')
  return context
}

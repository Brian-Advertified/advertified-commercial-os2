import { Navigate, useLocation, useNavigate } from 'react-router-dom'
import { useSession } from '../auth/session-state'
import { Icon } from '../components/Icon'
import { LoadingState } from '../components/PageState'
import { operationalCopy } from '../content/operational-copy'
import { publicReturnPath, workspaceSelectionPath } from '../routing/return-path'

export function SignInPage() {
  const { session, loading, error, signIn, reload } = useSession()
  const location = useLocation()
  const navigate = useNavigate()
  const returnTo = publicReturnPath(location.search)
  const managed = Boolean(session?.signInPath)
  const errorMessage = error ?? providerFailureMessage(location.search)

  if (loading && !session) return <LoadingState label="Opening Advertified" />
  if (session?.authenticated) {
    return <Navigate to={workspaceSelectionPath(returnTo)} replace />
  }

  async function continueToAdvertified() {
    try {
      const redirected = await signIn(returnTo ?? undefined)
      if (!redirected) navigate(workspaceSelectionPath(returnTo), { replace: true })
    } catch {
      // The session boundary presents the safe inline message.
    }
  }

  return <main className="sign-in-page">
    <SignInStory />
    <SignInPanel
      managed={managed}
      errorMessage={errorMessage}
      retryAvailable={Boolean(error)}
      disabled={loading || !session}
      onContinue={() => void continueToAdvertified()}
      onReload={() => void reload()}
    />
  </main>
}

function SignInStory() {
  return <section className="sign-in-story" aria-labelledby="sign-in-title">
    <a className="brand-lockup" href="/sign-in" aria-label="Advertified sign in">
      <span className="brand-mark">A</span><span>Advertified</span>
    </a>
    <div>
      <p className="eyebrow eyebrow-light">Commercial clarity, one workspace at a time</p>
      <h1 id="sign-in-title">The calm centre of campaign delivery.</h1>
      <p>Bring client, agency and commercial foundation records into one tenant-safe view.</p>
    </div>
    <div className="trust-note"><Icon name="shield" />
      {operationalCopy.signInTrust}</div>
  </section>
}

function SignInPanel({ managed, errorMessage, retryAvailable, disabled, onContinue, onReload }: {
  managed: boolean
  errorMessage: string | null
  retryAvailable: boolean
  disabled: boolean
  onContinue: () => void
  onReload: () => void
}) {
  const supportingCopy = managed
    ? 'Use your authorised account. Provider credentials and tokens are not stored in browser storage.'
    : operationalCopy.signInDirect
  return <section className="sign-in-panel" aria-label="Sign in">
    <div className="sign-in-card">
      <p className="eyebrow">Welcome back</p>
      <h2>Enter your Advertified workspace</h2>
      <p className="supporting-copy">{supportingCopy}</p>
      {errorMessage && <div className="inline-alert" role="alert">{errorMessage}</div>}
      <button className="primary-button" type="button" disabled={disabled} onClick={onContinue}>
        Continue to Advertified <Icon name="arrow" />
      </button>
      {retryAvailable && <button className="link-button" type="button" onClick={onReload}>
        Try connecting again
      </button>}
      <p className="privacy-note">Access is resolved by the Commercial API and limited to active memberships.</p>
    </div>
  </section>
}

function providerFailureMessage(search: string) {
  return new URLSearchParams(search).get('authentication') === 'failed'
    ? 'Sign in could not be completed. Try again.'
    : null
}

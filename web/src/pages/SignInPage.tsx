import { Navigate, useLocation, useNavigate } from 'react-router-dom'
import { useSession } from '../auth/session-state'
import { Icon } from '../components/Icon'
import { LoadingState } from '../components/PageState'
import { publicReturnPath, workspaceSelectionPath } from '../routing/return-path'

export function SignInPage() {
  const { session, loading, error, signIn, reload } = useSession()
  const location = useLocation()
  const navigate = useNavigate()
  const returnTo = publicReturnPath(location.search)
  const managedSignIn = Boolean(session?.signInPath)
  const providerFailure = new URLSearchParams(location.search).get('authentication') === 'failed'

  if (loading && !session) return <LoadingState label="Opening Advertified" />
  if (session?.authenticated) {
    return <Navigate to={workspaceSelectionPath(returnTo)} replace />
  }

  async function continueToAdvertified() {
    try {
      const redirected = await signIn(returnTo ?? undefined)
      if (!redirected) {
        navigate(workspaceSelectionPath(returnTo), { replace: true })
      }
    } catch {
      // The session boundary presents the safe inline message.
    }
  }

  return (
    <main className="sign-in-page">
      <section className="sign-in-story" aria-labelledby="sign-in-title">
        <a className="brand-lockup" href="/sign-in" aria-label="Advertified sign in">
          <span className="brand-mark">A</span><span>Advertified</span>
        </a>
        <div>
          <p className="eyebrow eyebrow-light">Commercial clarity, one workspace at a time</p>
          <h1 id="sign-in-title">The calm centre of campaign delivery.</h1>
          <p>Bring client, agency and commercial foundation records into one tenant-safe view.</p>
        </div>
        <div className="trust-note"><Icon name="shield" />
          {managedSignIn ? 'Secure managed sign in' : 'Local development session'}</div>
      </section>
      <section className="sign-in-panel" aria-label="Sign in">
        <div className="sign-in-card">
          <p className="eyebrow">Welcome back</p>
          <h2>Enter your Advertified workspace</h2>
          <p className="supporting-copy">{managedSignIn
            ? 'Use your authorised account. Provider credentials and tokens are not stored in browser storage.'
            : 'This development build uses the approved local identity. No provider credentials or bearer tokens are stored in the browser.'}
          </p>
          {(error || providerFailure) && <div className="inline-alert" role="alert">
            {error ?? 'Sign in could not be completed. Try again.'}</div>}
          <button
            className="primary-button"
            type="button"
            disabled={loading || !session}
            onClick={() => void continueToAdvertified()}
          >
            Continue to Advertified <Icon name="arrow" />
          </button>
          {error && (
            <button className="link-button" type="button" onClick={() => void reload()}>
              Try connecting again
            </button>
          )}
          <p className="privacy-note">Access is resolved by the Commercial API and limited to active memberships.</p>
        </div>
      </section>
    </main>
  )
}

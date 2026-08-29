import { Navigate, useNavigate } from 'react-router-dom'
import { useSession } from '../auth/session-state'
import { Icon } from '../components/Icon'
import { LoadingState } from '../components/PageState'

export function SignInPage() {
  const { session, loading, error, signIn, reload } = useSession()
  const navigate = useNavigate()

  if (loading && !session) return <LoadingState label="Opening Advertified" />
  if (session?.authenticated) return <Navigate to="/home" replace />

  async function continueToAdvertified() {
    try {
      await signIn()
      navigate('/workspaces', { replace: true })
    } catch {
      // The provider presents the safe inline message.
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
        <div className="trust-note"><Icon name="shield" /> Local development session</div>
      </section>
      <section className="sign-in-panel" aria-label="Local sign in">
        <div className="sign-in-card">
          <p className="eyebrow">Welcome back</p>
          <h2>Enter your Advertified workspace</h2>
          <p className="supporting-copy">
            This development build uses the approved local identity. No provider credentials
            or bearer tokens are stored in the browser.
          </p>
          {error && <div className="inline-alert" role="alert">{error}</div>}
          <button
            className="primary-button"
            type="button"
            disabled={loading || !session}
            onClick={() => void continueToAdvertified()}
          >
            Continue to local workspace <Icon name="arrow" />
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

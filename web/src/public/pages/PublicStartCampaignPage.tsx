import { Link } from '../../routing/router'
import { PublicPageHero } from '../components/PublicPageHero'

export function PublicStartCampaignPage() {
  return (
    <>
      <PublicPageHero
        eyebrow="PLAN A CAMPAIGN"
        title="Bring the business challenge. We’ll help shape the campaign."
        introduction="You do not need a finished media plan. Start with the supplied client brief and Advertified will help structure the next step."
      />
      <section className="section">
        <div className="shell contact-grid">
          <article className="contact-form">
            <span className="eyebrow">SUPPLIED CLIENT BRIEF</span>
            <h2>Start with the words you already have.</h2>
            <p>
              Sign in and paste or type the brief. Advertified keeps the source wording,
              extracts the campaign detail and asks for human input only where something
              important is unclear.
            </p>
            <Link className="btn primary large" href="/sign-in?returnTo=/briefs/new">
              Sign in to add a brief
            </Link>
          </article>
          <aside className="contact-side">
            <span className="eyebrow">USEFUL CONTEXT</span>
            <h2>Tell us what you know so far.</h2>
            <ol>
              <li>The business challenge and desired outcome</li>
              <li>The people or organisations you need to reach</li>
              <li>The priority geography and campaign timing</li>
              <li>The working investment range and important constraints</li>
            </ol>
            <p>
              Still working some of this out? That is fine. Advertified marks it as
              unclear instead of inventing an answer.
            </p>
          </aside>
        </div>
      </section>
    </>
  )
}

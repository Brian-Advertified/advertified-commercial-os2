import { Mail } from 'lucide-react'

import type { RegistrationType } from '../data/registrationTypes'

const relationshipLabels: Record<RegistrationType, string> = {
  advertiser: 'advertiser',
  agency: 'media agency',
  'media-owner': 'media-owner',
  creator: 'creator',
}

export function GovernedOnboardingForm({ type }: {
  type: RegistrationType
  organisationLabel: string
  relationshipLabel: string
}) {
  const relationship = relationshipLabels[type]
  const subject = encodeURIComponent(`Advertified ${relationship} access request`)

  return (
    <article className="registration-details__form">
      <header className="registration-details__heading">
        <span className="eyebrow">GOVERNED ONBOARDING</span>
        <h2>Request the right Advertified access.</h2>
        <p>
          Online onboarding is not connected in this build. Email Advertified so an
          administrator can verify your organisation and the access you need.
        </p>
      </header>
      <div className="public-form-status" role="status">
        No account, membership or campaign access is created automatically.
      </div>
      <a className="btn primary large" href={`mailto:ad@advertified.com?subject=${subject}`}>
        <Mail size={17} aria-hidden="true" /> Email Advertified
      </a>
    </article>
  )
}

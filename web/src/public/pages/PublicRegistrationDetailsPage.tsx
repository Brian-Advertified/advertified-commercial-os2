import { ArrowLeft, CheckCircle2, LockKeyhole } from 'lucide-react';

import { Link } from '../../routing/router';
import '../../styles/public-forms.css';
import { PublicPageHero } from '../components/PublicPageHero';
import { GovernedOnboardingForm } from '../components/GovernedOnboardingForm';
import { registrationTypes, type RegistrationType } from '../data/registrationTypes';

const reviewSteps = [
  'Confirm the person and organisation requesting access.',
  'Review the commercial relationship and intended workspace scope.',
  'Issue secure account access only after the required checks are complete.',
] as const;

export function PublicRegistrationDetailsPage({ type }: { type: RegistrationType }) {
  const content = registrationTypes[type];

  return (
    <>
      <PublicPageHero
        eyebrow="CREATE YOUR ADVERTIFIED PROFILE"
        title={content.title}
        introduction={content.introduction}
        actions={<Link className="btn secondary large" href="/register"><ArrowLeft size={17} aria-hidden="true" /> Change registration type</Link>}
      />
      <section className="registration-details">
        <div className="shell registration-details__layout">
          <GovernedOnboardingForm type={type} organisationLabel={content.organisationLabel} relationshipLabel={content.relationshipLabel} />

          <aside className="registration-details__aside">
            <LockKeyhole aria-hidden="true" />
            <h2>Access follows verification.</h2>
            <p>Submitting this request does not create a login or expose campaign, client, inventory or payment records.</p>
            <ul>
              {reviewSteps.map((step) => <li key={step}><CheckCircle2 aria-hidden="true" /> {step}</li>)}
            </ul>
            <Link className="btn secondary" href="/sign-in">Already registered? Log in</Link>
          </aside>
        </div>
      </section>
    </>
  );
}

import {
  ArrowRight,
  BriefcaseBusiness,
  Building2,
  Check,
  FileCheck2,
  LockKeyhole,
  RadioTower,
  ShieldCheck,
  UsersRound,
} from 'lucide-react';
import '../../styles/public-forms.css';
import { useState } from 'react';
import { Link } from '../../routing/router';
import { PublicPageHero } from '../components/PublicPageHero';

const registrationPaths = [
  {
    id: 'advertiser',
    title: 'Brand or advertiser',
    shortTitle: 'Advertiser',
    description: 'For businesses planning and funding campaigns for their own brands.',
    action: 'Start advertiser onboarding',
    href: '/register/advertiser',
    icon: Building2,
    specificTitle: 'Campaign and brand profile',
    specific: ['Brand or trading name', 'Industry and business stage', 'Campaign goal and priority markets', 'Indicative investment range', 'Primary decision-maker'],
    verification: ['Organisation details when workspace access is needed', 'Authority to approve campaign decisions', 'Billing details before payment'],
  },
  {
    id: 'media-agency',
    title: 'Agency',
    shortTitle: 'Agency',
    description: 'For agencies representing clients and collaborating on assigned campaigns.',
    action: 'Request agency onboarding',
    href: '/register/agency',
    icon: BriefcaseBusiness,
    specificTitle: 'Agency operating profile',
    specific: ['Agency services and specialities', 'Client or campaign relationship', 'Primary markets and coverage', 'Team contact responsible for the account', 'Access scope being requested'],
    verification: ['Registered agency organisation', 'Authority to represent the named client', 'Client and campaign-specific access approval'],
  },
  {
    id: 'supplier',
    title: 'Media owner or supplier',
    shortTitle: 'Media owner',
    description: 'For media owners, inventory suppliers and production partners.',
    action: 'Become a media partner',
    href: '/register/media-owner',
    icon: RadioTower,
    specificTitle: 'Media and inventory profile',
    specific: ['Media channels and inventory types', 'Geographic coverage and availability contact', 'Rate cards and commercial conditions', 'Authority to sell or represent inventory', 'Operations and proof-of-delivery contact'],
    verification: ['Company and tax records', 'Applicable contracts, permits and site authority', 'Banking details through independent verification later'],
  },
  {
    id: 'creator',
    title: 'Creator or influencer',
    shortTitle: 'Creator',
    description: 'For creators, influencers and specialist content partners.',
    action: 'Request creator onboarding',
    href: '/register/creator',
    icon: UsersRound,
    specificTitle: 'Creator and audience profile',
    specific: ['Public name, channels and profile links', 'Content categories and brand-fit preferences', 'Audience size, geography and engagement evidence', 'Portfolio and previous commercial work', 'Indicative services and fees'],
    verification: ['Identity and profile ownership', 'Audience and performance evidence', 'Master agreement, rights and disclosure obligations'],
  },
] as const;

const commonDetails = [
  'Full legal name and preferred contact details',
  'Business email and mobile number',
  'Registered or trading organisation name',
  'Registration and VAT details where applicable',
  'Business address, city and country',
] as const;

type RegistrationPath = (typeof registrationPaths)[number];

export function PublicRegisterPage() {
  const [selectedId, setSelectedId] = useState<RegistrationPath['id']>('advertiser');
  const selected = registrationPaths.find((path) => path.id === selectedId) ?? registrationPaths[0];

  return (
    <>
      <RegistrationHero />
      <section className="public-registration">
        <div className="shell public-registration__surface">
          <header className="public-registration__intro">
            <span className="eyebrow">CHOOSE YOUR REGISTRATION TYPE</span>
            <h2>Who are you registering as?</h2>
            <p>Your choice changes the information and verification required. It does not create a broad internal account or expose records belonging to another organisation.</p>
          </header>

          <div className="public-registration__selector" role="radiogroup" aria-label="Registration type">
            {registrationPaths.map(({ id, shortTitle, description, icon: Icon }) => (
              <button
                type="button"
                role="radio"
                aria-checked={selectedId === id}
                className={`public-registration__choice${selectedId === id ? ' is-selected' : ''}`}
                key={id}
                onClick={() => setSelectedId(id)}
              >
                <span><Icon aria-hidden="true" /></span>
                <strong>{shortTitle}</strong>
                <small>{description}</small>
                {selectedId === id && <Check aria-hidden="true" className="public-registration__choice-check" />}
              </button>
            ))}
          </div>

          <div className="public-registration__profile" aria-live="polite">
            <div className="public-registration__profile-head">
              <span className="public-registration__icon"><selected.icon aria-hidden="true" /></span>
              <div><span className="eyebrow">SELECTED PATH</span><h2>{selected.title}</h2><p>{selected.description}</p></div>
            </div>

            <div className="public-registration__sections">
              <RegistrationSection icon={Building2} number="01" title="Contact and organisation details" items={commonDetails} />
              <RegistrationSection icon={FileCheck2} number="02" title={selected.specificTitle} items={selected.specific} />
              <RegistrationSection icon={ShieldCheck} number="03" title="Verification before access" items={selected.verification} />
              <section className="public-registration__section public-registration__security">
                <div className="public-registration__section-title"><span><LockKeyhole aria-hidden="true" /></span><div><small>04</small><h3>Secure account access</h3></div></div>
                <p>After the relationship and organisation scope are confirmed, the invited user sets a strong password and accepts the terms and POPIA-aligned privacy notice. One login can support more than one approved organisation membership without mixing their records.</p>
              </section>
            </div>

            <div className="public-registration__continue">
              <div><strong>Ready to begin as {selected.shortTitle.toLowerCase()}?</strong><p>The next step starts the appropriate conversation or onboarding journey. Access is issued only after the required checks.</p></div>
              <Link className="btn primary large" href={selected.href}>{selected.action}<ArrowRight size={18} aria-hidden="true" /></Link>
            </div>
          </div>

          <aside className="public-registration__note">
            <strong>Why the paths stay separate</strong>
            <span>Advertisers approve campaigns, agencies act for assigned clients, suppliers provide verified inventory, and creators accept specific assignments. Their responsibilities and access must not be treated as interchangeable.</span>
          </aside>
        </div>
      </section>
    </>
  );
}

function RegistrationHero() {
  return (
    <PublicPageHero
      eyebrow="CREATE YOUR ADVERTIFIED PROFILE"
      title="Register once. Get the access that matches how you work."
      introduction="Choose your relationship with Advertified first. We will ask for the right organisation, campaign, inventory or creator information—then verify it before private workspace access is enabled."
      actions={<Link className="btn secondary large" href="/sign-in">Already registered? Log in</Link>}
    />
  );
}

function RegistrationSection({ icon: Icon, number, title, items }: {
  icon: typeof Building2;
  number: string;
  title: string;
  items: readonly string[];
}) {
  return (
    <section className="public-registration__section">
      <div className="public-registration__section-title"><span><Icon aria-hidden="true" /></span><div><small>{number}</small><h3>{title}</h3></div></div>
      <ul>{items.map((item) => <li key={item}><Check size={16} aria-hidden="true" />{item}</li>)}</ul>
    </section>
  );
}

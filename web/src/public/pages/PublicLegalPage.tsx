import { Link } from '../../routing/router';
import { PublicPageHero } from '../components/PublicPageHero';

const policies = {
  privacy: {
    title: 'Privacy Policy',
    introduction: 'How Advertified collects, uses, stores, and protects personal and business information.',
    paragraphs: [
      'The approved Privacy Policy is not published on this environment yet. Advertified will not present a short marketing summary as the governing legal document.',
      'Contact ad@advertified.com to request the current approved policy and information about exercising privacy rights.',
    ],
    related: [{ label: 'Cookie Policy', href: '/cookie-policy' }, { label: 'Terms and Conditions', href: '/terms-of-service' }],
  },
  terms: {
    title: 'Terms and Conditions',
    introduction: 'Commercial terms governing proposals, bookings, payments, campaign execution, and related obligations.',
    paragraphs: [
      'The approved Terms and Conditions are not published on this environment yet. Proposal approval, client acceptance, booking and payment remain separate governed actions.',
      'Contact ad@advertified.com to request the current approved commercial terms before relying on this service.',
    ],
    related: [{ label: 'Privacy Policy', href: '/privacy' }, { label: 'Cookie Policy', href: '/cookie-policy' }],
  },
  cookies: {
    title: 'Cookie Policy',
    introduction: 'How Advertified uses necessary cookies and manages optional analytics and marketing preferences.',
    paragraphs: [
      'The approved Cookie Policy is not published on this environment yet. Necessary authentication and security storage must remain distinct from optional analytics or marketing consent.',
      'Contact ad@advertified.com to request the current approved cookie information.',
    ],
    related: [{ label: 'Privacy Policy', href: '/privacy' }, { label: 'Terms and Conditions', href: '/terms-of-service' }],
  },
} as const;

export function PublicLegalPage({ kind }: { kind: keyof typeof policies }) {
  const policy = policies[kind];
  return (
    <>
      <PublicPageHero eyebrow="POLICY INFORMATION" title={policy.title} introduction={policy.introduction} />
      <section className="section public-legal-section"><div className="shell"><article className="public-legal-copy">
        {policy.paragraphs.map((paragraph) => <p key={paragraph}>{paragraph}</p>)}
        <nav aria-label="Related legal policies"><h2>Related policies</h2><ul>
          {policy.related.map((item) => <li key={item.href}><Link href={item.href}>{item.label}</Link></li>)}
        </ul></nav>
      </article></div></section>
    </>
  );
}

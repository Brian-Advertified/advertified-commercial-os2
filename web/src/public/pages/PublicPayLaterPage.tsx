import { PublicCta } from '../components/PublicCta';
import { PublicPageHero } from '../components/PublicPageHero';

const steps = [
  ['01', 'Plan the campaign', 'The campaign and investment are shaped first so the funding conversation is tied to a clear proposal.'],
  ['02', 'Approve the preferred proposal', 'The client chooses the campaign direction before any finance referral is recorded.'],
  ['03', 'Request a finance referral', 'With the client’s consent, Advertified records a referral to an independent finance provider.'],
  ['04', 'Receive the provider’s decision', 'The provider contacts and assesses the client directly and makes its own approval or decline decision.'],
] as const;

export function PublicPayLaterPage() {
  return (
    <>
      <PublicPageHero eyebrow="ADVERTISE NOW, PAY LATER" title="A campaign funding route when cash flow needs more room." introduction="After an approved campaign proposal, Advertified can connect an interested client to an independent finance provider for assessment." />
      <section className="section"><div className="shell process-grid four">{steps.map(([number, title, description]) => <article key={number}><span>{number}</span><h3>{title}</h3><p>{description}</p></article>)}</div></section>
      <section className="section muted"><div className="shell split"><div><span className="eyebrow">THE IMPORTANT BOUNDARY</span><h2>The campaign comes first. The finance decision remains independent.</h2></div><p>Advertified records and coordinates the referral; it is not the lender, underwriter or finance approver. Finance approval also does not by itself confirm media bookings or campaign readiness.</p></div></section>
      <PublicCta title="Start with the campaign opportunity." description="Once the campaign direction and investment are clear, we can explain whether a finance referral is an appropriate next step." />
    </>
  );
}

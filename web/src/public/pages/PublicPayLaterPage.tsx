import { PublicCta } from '../components/PublicCta';
import { PublicPageHero } from '../components/PublicPageHero';

const steps = [
  ['01', 'Plan the campaign', 'The campaign and investment are shaped first so any future funding conversation is tied to a clear proposal.'],
  ['02', 'Approve the preferred proposal', 'The client chooses the campaign direction before a finance referral could be considered.'],
  ['03', 'Referral route becomes available', 'Advertified will only offer the referral after an approved independent finance-provider integration is activated.'],
  ['04', 'Provider makes the decision', 'The independent provider would contact and assess the client directly and make its own approval or decline decision.'],
] as const;

export function PublicPayLaterPage() {
  return (
    <>
      <PublicPageHero eyebrow="ADVERTISE NOW, PAY LATER · NOT CURRENTLY AVAILABLE" title="A planned campaign-funding referral route." introduction="Advertise Now, Pay Later is not currently active. Advertified will only offer this route after an approved independent finance-provider integration has been activated and verified." />
      <section className="section"><div className="shell process-grid four">{steps.map(([number, title, description]) => <article key={number}><span>{number}</span><h3>{title}</h3><p>{description}</p></article>)}</div></section>
      <section className="section muted"><div className="shell split"><div><span className="eyebrow">CURRENT STATUS</span><h2>Plan the campaign now. Funding referral remains unavailable.</h2></div><p>Advertified currently supports its active funding and payment workflows only. A future finance referral will remain independent: Advertified will not be the lender, underwriter or finance approver, and finance approval will not by itself confirm media bookings or campaign readiness.</p></div></section>
      <PublicCta title="Start with the campaign opportunity." description="Tell us what the campaign needs to achieve. If the finance-referral route becomes available later, its availability will be shown explicitly." />
    </>
  );
}

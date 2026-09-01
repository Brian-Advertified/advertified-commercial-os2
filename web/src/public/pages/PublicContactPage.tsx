import { PublicContactForm } from '../components/PublicContactForm';
import { PublicPageHero } from '../components/PublicPageHero';

export function PublicContactPage() {
  return (
    <>
      <PublicPageHero eyebrow="CONTACT ADVERTIFIED" title="Start the conversation that fits your opportunity." introduction="Talk to us about a campaign, agency relationship, media partnership or another way Advertified could help." />
      <section className="section"><div className="shell contact-grid"><PublicContactForm kind="general-enquiry" /><aside className="contact-side"><span className="eyebrow">WHAT HAPPENS NEXT</span><h2>Your message reaches a real person.</h2><ol><li>Tell us who you are and what you would like to discuss.</li><li>An Advertified team member reviews the context.</li><li>We contact you to clarify the opportunity.</li><li>Together, we agree on the most useful next step.</li></ol><p><strong>Prefer to email directly?</strong><br /><a href="mailto:ad@advertified.com">ad@advertified.com</a></p></aside></div></section>
    </>
  );
}

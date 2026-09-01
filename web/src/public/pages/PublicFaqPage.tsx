import { PublicCta } from '../components/PublicCta';
import { PublicPageHero } from '../components/PublicPageHero';
import { faqItems } from '../data/faqContent';

export function PublicFaqPage() {
  return (
    <>
      <PublicPageHero eyebrow="FREQUENTLY ASKED QUESTIONS" title="The practical things you should know before starting." introduction="Understand who Advertified works with, what to prepare, how recommendations are shaped and what happens after the proposal." />
      <section className="section"><div className="shell faq-list">{faqItems.map((item) => <details key={item.question}><summary>{item.question}<span aria-hidden="true">+</span></summary><p>{item.answer}</p></details>)}</div></section>
      <PublicCta />
    </>
  );
}

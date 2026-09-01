import { PublicCta } from '../components/PublicCta';
import { PublicPageHero } from '../components/PublicPageHero';

export function PublicAboutPage() {
  return (
    <>
      <PublicPageHero eyebrow="ABOUT ADVERTIFIED" title="Advertising has extraordinary reach—and unnecessary complexity." introduction="Advertified exists to make that landscape easier to understand, easier to plan across and more accountable from the first decision to the final evidence." />
      <section className="section"><div className="shell split"><div><span className="eyebrow">WHY WE EXIST</span><h2>Strong campaign thinking should not be lost between spreadsheets, supplier documents and disconnected channels.</h2><p>Advertified brings campaign intelligence, media context, experienced judgement and delivery coordination into one coherent journey. The goal is simple: help brands and agencies make stronger decisions and help quality media opportunities find the right campaign role.</p></div><div className="values"><article><b>01</b><h3>Clarity before activity</h3><p>Start with the business outcome and make the campaign logic easy to understand.</p></article><article><b>02</b><h3>Intelligence with accountability</h3><p>Use rich media and campaign information while keeping the reasoning and evidence visible.</p></article><article><b>03</b><h3>Human judgement matters</h3><p>Intelligence strengthens experienced people; it does not replace their responsibility.</p></article><article><b>04</b><h3>Learning should compound</h3><p>Every supplier input and completed campaign should improve the next advertising decision.</p></article></div></div></section>
      <PublicCta title="Let’s shape a stronger route through the media landscape." />
    </>
  );
}

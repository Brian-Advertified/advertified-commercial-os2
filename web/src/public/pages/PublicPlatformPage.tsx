import { CheckCircle2 } from 'lucide-react';
import { PublicCta } from '../components/PublicCta';
import { PublicPageHero } from '../components/PublicPageHero';

const platformCapabilities = [
  ['01', 'Understand the opportunity', 'Bring the commercial goal, audience, market context and investment into one focused Campaign Brief.'],
  ['02', 'Apply campaign intelligence', 'Use media, audience, geographic and commercial context to find a stronger route through the available choices.'],
  ['03', 'Build a connected media plan', 'Give every recommended channel a clear job and show how the pieces work together around the campaign outcome.'],
  ['04', 'Present meaningful choices', 'Turn the strategy into professional investment options with the unlocked value, trade-offs and assumptions explained.'],
  ['05', 'Carry decisions into delivery', 'Coordinate campaign readiness, execution evidence and measurement without losing the reasoning behind the approved direction.'],
] as const;

export function PublicPlatformPage() {
  return (
    <>
      <PublicPageHero eyebrow="THE ADVERTIFIED PLATFORM" title="One connected view of the campaign—from ambition to evidence." introduction="Advertified combines campaign intelligence, experienced judgement and structured delivery so better advertising decisions do not get lost between planning and execution." />
      <section className="section"><div className="shell"><div className="process-grid">{platformCapabilities.map(([number, title, description]) => <article key={number}><span>{number}</span><h3>{title}</h3><p>{description}</p></article>)}</div></div></section>
      <section className="section muted"><div className="shell split feature-matrix"><div><span className="eyebrow">INTELLIGENCE YOU CAN UNDERSTAND</span><h2>See why the plan makes sense.</h2><p>Advertified connects recommendations to the brief, the audience, the market and the campaign constraints. The result is not a mysterious answer—it is a route that experienced people can review, explain and improve.</p></div><div className="glass-panel platform-gates"><h3>What stays visible</h3>{['The campaign outcome and audience','The role of every media channel','Investment value and trade-offs','Assumptions that still need confirmation','Evidence required to prove delivery'].map((item) => <div key={item}><CheckCircle2 aria-hidden="true" /><span>{item}</span></div>)}</div></div></section>
      <PublicCta />
    </>
  );
}

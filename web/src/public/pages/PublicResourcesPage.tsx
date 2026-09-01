import { PublicCta } from '../components/PublicCta';
import { PublicPageHero } from '../components/PublicPageHero';

const resources = [
  ['01', 'Start with the business problem', 'A useful brief explains what must change in the market—not merely which media the advertiser wants to buy.', ['What outcome matters?', 'Who needs to respond?', 'Where and when must the campaign work?', 'What investment and constraints are real?']],
  ['02', 'Give every channel a job', 'A strong media mix is not a list of channels. Each inclusion should make a distinct contribution to the audience journey.', ['Build awareness', 'Create relevance', 'Prompt response', 'Reinforce or retarget']],
  ['03', 'Compare investment meaningfully', 'Useful options show what additional investment unlocks and where the trade-offs sit.', ['Strategic purpose', 'Included campaign work', 'Expected contribution', 'Important risks and assumptions']],
  ['04', 'Plan the proof before launch', 'Decide how delivery and performance will be evidenced before the campaign begins.', ['Proof of execution', 'Measurement source', 'Reporting frequency', 'Known measurement limits']],
] as const;

export function PublicResourcesPage() {
  return (
    <>
      <PublicPageHero eyebrow="CAMPAIGN GUIDANCE" title="Four principles for making a stronger advertising decision." introduction="Good campaigns become clearer when the business goal, media roles, investment choices and evidence plan are considered together." />
      <section className="section"><div className="shell resource-grid">{resources.map(([number, title, description, points]) => <article key={number}><span>GUIDE {number}</span><h3>{title}</h3><p>{description}</p><ul>{points.map((point) => <li key={point}>{point}</li>)}</ul></article>)}</div></section>
      <PublicCta title="Ready to apply these principles to a real campaign?" />
    </>
  );
}

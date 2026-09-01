import { CircleDot, MonitorPlay, Newspaper, Radio, Smartphone, UsersRound } from 'lucide-react';
import { Link } from '../../routing/router';
import { PublicCta } from '../components/PublicCta';
import { PublicPageHero } from '../components/PublicPageHero';
import { investmentBands } from '../data/publicContent';

export function PublicPackagesPage() {
  return (
    <>
      <PublicPageHero eyebrow="CAMPAIGN INVESTMENT" title="Different ambitions need different levels of campaign weight." introduction="These bands are useful starting points for the conversation. Your final recommendation is shaped by the brief, the market opportunity and what the investment needs to achieve—not by a fixed media bundle." />
      <section className="section"><div className="shell pricing-grid">{investmentBands.map((band) => <article key={band.name}><small>CAMPAIGN INVESTMENT</small><h3>{band.name}</h3><strong>{band.range}</strong><p>{band.description}</p><div className="package-media-icons" aria-label="Illustrative media mix">{band.mediaExamples.map((item) => <span key={item}>{mediaIcon(item)}{item}</span>)}</div><ul>{band.unlocks.map((item) => <li key={item}>{item}</li>)}</ul><Link className="btn primary" href="/start">Explore {band.name}</Link></article>)}</div><p className="fine-print">These bands describe campaign ambition, not fixed prices or bundles. Final currency, media selection, pricing, tax treatment and availability are confirmed during planning.</p></section>
      <PublicCta title="Not sure what the campaign should cost?" description="Tell us what the business needs to achieve. We will help frame a realistic investment conversation." />
    </>
  );
}


function mediaIcon(label: string) {
  if (label === 'Radio') return <Radio size={15} aria-hidden="true" />;
  if (label === 'Television') return <MonitorPlay size={15} aria-hidden="true" />;
  if (label === 'Print') return <Newspaper size={15} aria-hidden="true" />;
  if (label === 'Influencer' || label === 'Creators') return <UsersRound size={15} aria-hidden="true" />;
  if (label.includes('Digital') || label.includes('Social')) return <Smartphone size={15} aria-hidden="true" />;
  return <CircleDot size={15} aria-hidden="true" />;
}

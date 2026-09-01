import { MonitorUp, Newspaper, RadioTower, Smartphone, Tv, Users } from 'lucide-react';
import { Link } from '../../routing/router';
import { PublicCta } from '../components/PublicCta';
import { PublicPageHero } from '../components/PublicPageHero';
import { channels } from '../data/channelContent';

const icons = [MonitorUp, RadioTower, Tv, Newspaper, Smartphone, Users] as const;

export function PublicSolutionsPage() {
  return (
    <>
      <PublicPageHero eyebrow="CROSS-MEDIA SOLUTIONS" title="Build the media mix around the campaign job - not the loudest channel." introduction="Advertified considers traditional, digital and creator media together, then gives each recommended channel a clear role around the audience, geography, investment and outcome." />
      <section className="section"><div className="shell solution-grid">{channels.map((channel, index) => { const Icon = icons[index]; return <Link href={`/solutions/${channel.slug}`} className="solution-card" key={channel.slug}><Icon aria-hidden="true" /><div><h3>{channel.name}</h3><p>{channel.introduction}</p><ul>{channel.roles.map((role) => <li key={role}>{role}</li>)}</ul><span>Explore solution →</span></div></Link>; })}</div></section>
      <PublicCta title="Not sure which media belong in the plan?" description="Start with the business outcome. Advertified will help determine which channels deserve a role and why." />
    </>
  );
}

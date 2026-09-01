import { MonitorUp, Newspaper, RadioTower, Smartphone, Tv, Users } from 'lucide-react';
import { Link } from '../../routing/router';
import { PublicCta } from '../components/PublicCta';
import { PublicPageHero } from '../components/PublicPageHero';
import type { ChannelDefinition } from '../data/channelContent';

const icons = { ooh: MonitorUp, radio: RadioTower, television: Tv, print: Newspaper, digital: Smartphone, influencers: Users } as const;

export function PublicChannelPage({ channel }: { channel: ChannelDefinition }) {
  const Icon = icons[channel.slug as keyof typeof icons] ?? MonitorUp;
  return (
    <>
      <PublicPageHero
        eyebrow={channel.eyebrow}
        title={`${channel.name} advertising with a clear campaign role`}
        introduction={channel.introduction}
        actions={<Link className="btn primary large" href="/start">Discuss {channel.slug === 'ooh' ? 'an' : 'a'} {channel.shortName} campaign →</Link>}
      />
      <section className="section"><div className="shell split channel-detail"><div className="channel-art"><Icon aria-hidden="true" /></div><div><span className="eyebrow">HOW ADVERTIFIED MAKES IT WORK</span><h2>Use the medium with purpose, not guesswork.</h2><p>Advertified connects the channel to the business goal, shows why it belongs in the plan and carries its practical requirements into campaign coordination.</p><div className="feature-list">{channel.evidence.map((item, index) => <article key={item}><span>0{index + 1}</span><div><h3>{item}</h3><p>Considered during planning and carried forward into the campaign’s delivery requirements.</p></div></article>)}</div></div></div></section>
      <PublicCta title={`Could ${channel.shortName} help move your campaign forward?`} description="Let us assess the role it could play alongside the rest of the media mix." />
    </>
  );
}

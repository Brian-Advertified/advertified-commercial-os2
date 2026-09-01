import {
  BadgeCheck,
  BriefcaseBusiness,
  Building2,
  PlayCircle,
  RadioTower,
  Route,
  ShieldCheck,
  Users,
} from 'lucide-react';
import { useEffect, useState, type ReactNode } from 'react';
import { Link } from '../../routing/router';
import { getPublicInventorySummary, type PublicInventorySummary } from '../api/publicInventory';
import { MediaInventoryPartnersStrip } from '../components/MediaInventoryPartnersStrip';
import { ProductPreview } from '../components/ProductPreview';
import { getPublicInventoryChannelPresentation } from '../data/publicInventoryChannels';

type InventoryState =
  | { status: 'loading' }
  | { status: 'ready'; data: PublicInventorySummary }
  | { status: 'unavailable' };

export function PublicHomePage() {
  const inventory = usePublicInventory();
  return <>
    <HomeHero />
    <MediaInventoryPartnersStrip />
    <InventoryProof inventory={inventory} />
    <ParticipantSection />
  </>;
}

function usePublicInventory(): InventoryState {
  const [inventory, setInventory] = useState<InventoryState>({ status: 'loading' });
  useEffect(() => {
    const controller = new AbortController();
    getPublicInventorySummary(controller.signal)
      .then((data) => setInventory({ status: 'ready', data }))
      .catch((error: unknown) => {
        if (!(error instanceof DOMException && error.name === 'AbortError')) {
          setInventory({ status: 'unavailable' });
        }
      });
    return () => controller.abort();
  }, []);
  return inventory;
}

function HomeHero() {
  return <section className="mock-hero">
    <div className="mock-hero__glow mock-hero__glow--left" />
    <div className="mock-hero__glow mock-hero__glow--right" />
    <div className="shell mock-hero__grid">
      <div className="mock-hero__copy">
        <h1><span className="mock-hero__message-line">Intelligence layer for modern advertising.</span></h1>
        <p>Advertified turns a business problem into a media plan that can be approved, financed, bought, executed and measured.</p>
        <div className="mock-hero__actions">
          <Link href="/how-it-works" className="btn primary mock-hero__primary">
            <PlayCircle size={19} aria-hidden="true" /> See how it works
          </Link>
        </div>
        <div className="mock-hero__proofs">
          <span><BadgeCheck size={16} aria-hidden="true" /> Experienced human guidance</span>
          <span><Route size={16} aria-hidden="true" /> Explainable campaign intelligence</span>
          <span><ShieldCheck size={16} aria-hidden="true" /> Evidence-led delivery</span>
        </div>
      </div>
      <ProductPreview />
    </div>
  </section>;
}

function InventoryProof({ inventory }: { inventory: InventoryState }) {
  const channels = inventory.status === 'ready' ? inventory.data.channels : [];
  return <section className="public-inventory-proof" aria-labelledby="media-network-title">
    <div className="shell">
      <header className="public-section-heading public-section-heading--title-only public-inventory-proof__header">
        <h2 id="media-network-title">MEDIA NETWORK</h2>
      </header>
      <div className="public-inventory-proof__grid">
        {channels.map((item) => <InventoryChannelCard key={item.channel} item={item} />)}
        <InventoryStateMessage state={inventory} />
      </div>
    </div>
  </section>;
}

function InventoryChannelCard({ item }: {
  item: PublicInventorySummary['channels'][number];
}) {
  const visual = getPublicInventoryChannelPresentation(item.channel);
  const count = item.count.toLocaleString();
  return <Link href={`/media-network/${item.channel}`}
    className={`public-inventory-card public-inventory-card--${item.channel}`}
    aria-label={`${visual.label}: ${count} media owners. View logos.`}>
    {visual.image && <img className="public-inventory-card__image" src={visual.image}
      alt="" loading="lazy" decoding="async" />}
    <div className="public-inventory-card__count"><strong>{count}</strong><span>{visual.label}</span></div>
  </Link>;
}

function InventoryStateMessage({ state }: { state: InventoryState }) {
  if (state.status === 'loading') {
    return <div className="public-inventory-proof__loading">Media owner counts are loading from the published catalogue.</div>;
  }
  if (state.status === 'unavailable') {
    return <div className="public-inventory-proof__loading">Current media owner counts are temporarily unavailable.</div>;
  }
  return state.data.channels.length === 0
    ? <div className="public-inventory-proof__loading">Media owners will appear here when published inventory is available.</div>
    : null;
}

function ParticipantSection() {
  return <section className="section muted public-audiences"><div className="shell">
    <span className="eyebrow">BUILT AROUND THE REAL ADVERTISING ECOSYSTEM</span>
    <h2>One campaign journey. Clear value for every participant.</h2>
    <div className="partner-cards">
      <Participant icon={<Building2 aria-hidden="true" />} title="For brands and advertisers"
        copy="Turn a growth challenge into a clear campaign direction without having to become a media-planning expert."
        href="/start" action="Plan a campaign" />
      <Participant icon={<BriefcaseBusiness aria-hidden="true" />} title="For agencies"
        copy="Add structured campaign and media intelligence to planning, proposals and accountable delivery."
        href="/register/agency" action="Register your agency" />
      <Participant icon={<RadioTower aria-hidden="true" />} title="For media owners"
        copy="Help planners understand where your media fits and connect relevant opportunities to better-shaped demand."
        href="/register/media-owner" action="Register as a media owner" />
      <Participant icon={<Users aria-hidden="true" />} title="For creators and specialists"
        copy="Participate in suitable assignments with the campaign role, deliverables, rights and evidence made clear."
        href="/register/creator" action="Register as a creator" />
    </div>
  </div></section>;
}

function Participant({ icon, title, copy, href, action }: {
  icon: ReactNode; title: string; copy: string; href: string; action: string;
}) {
  return <article>{icon}<h3>{title}</h3><p>{copy}</p><Link href={href}>{action} →</Link></article>;
}

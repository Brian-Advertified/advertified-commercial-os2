import {
  ArrowUpRight,
  BadgeCheck,
  CalendarRange,
  ChartNoAxesCombined,
  Columns3,
  Database,
  FileCheck2,
  Handshake,
  LockKeyhole,
  Map,
  MessageSquareText,
  Palette,
  Radio,
  RadioTower,
  Rocket,
  Route,
  ScanSearch,
  ShieldCheck,
  SlidersHorizontal,
  Smartphone,
  Target,
  Tv,
  Users,
  WalletCards,
  Waypoints,
  Workflow,
} from 'lucide-react';
import { useEffect, useRef, useState } from 'react';
import { Link } from '../../routing/router';

const stages = [
  {
    shortTitle: 'Brief',
    eyebrow: 'Your ambition',
    title: 'Tell us what success should look like.',
    description: 'Share the campaign objective, audience, locations, timing and investment range—online or with an Advertified media expert.',
    points: [
      { icon: Target, text: 'Business objective and desired action' },
      { icon: Users, text: 'Audience, language and market priorities' },
      { icon: CalendarRange, text: 'Timing, constraints and investment range' },
    ],
    icon: MessageSquareText,
    outputLabel: 'You receive',
    outputTitle: 'A structured campaign brief',
    outputText: 'A shared definition of the opportunity—clear enough for strategy, media planning and commercial review.',
    visual: 'brief',
  },
  {
    shortTitle: 'Understand',
    eyebrow: 'Inventory intelligence',
    title: 'We understand what the market can genuinely offer.',
    description: 'Advertified evaluates relevant inventory, commercial evidence, geography and audience fit. Media experts review the evidence and remain in control.',
    points: [
      { icon: Database, text: 'Real media inventory and rate information' },
      { icon: Map, text: 'Geographic, language and audience relevance' },
      { icon: BadgeCheck, text: 'Evidence, validity and confidence' },
    ],
    icon: ScanSearch,
    outputLabel: 'You receive',
    outputTitle: 'A grounded opportunity view',
    outputText: 'Not a directory of listings—a commercially useful understanding of what could fit the brief and why.',
    visual: 'inventory',
  },
  {
    shortTitle: 'Plan',
    eyebrow: 'Media strategy',
    title: 'Your channel mix takes shape around the brief.',
    description: 'The planning engine and Advertified media experts balance reach, frequency, relevance, geography, timing and budget across channels.',
    points: [
      { icon: SlidersHorizontal, text: 'Expert-adjustable channel allocations' },
      { icon: Waypoints, text: 'Media items that work together as one campaign' },
      { icon: BadgeCheck, text: 'Clear strategic and commercial rationale' },
    ],
    icon: Waypoints,
    outputLabel: 'You receive',
    outputTitle: 'A connected media plan',
    outputText: 'Every channel has a job to do, with the plan shaped around the brief rather than a fixed media bundle.',
    visual: 'plan',
  },
  {
    shortTitle: 'Choose',
    eyebrow: 'Your decision',
    title: 'Choose how far you want the campaign to go.',
    description: 'Compare three genuinely different campaign options. Each option has its own exact media allocation, deliverables and executable plan.',
    points: [
      { icon: Columns3, text: 'See exactly what changes between options' },
      { icon: LockKeyhole, text: 'Your selected plan and investment stay linked' },
      { icon: MessageSquareText, text: 'Approve or request changes with confidence' },
    ],
    icon: Columns3,
    outputLabel: 'You compare',
    outputTitle: 'Three clear ways forward',
    outputText: 'The differences are practical and visible—not just three prices attached to the same plan.',
    visual: 'options',
  },
  {
    shortTitle: 'Prepare',
    eyebrow: 'Campaign preparation',
    title: 'We coordinate every moving part.',
    description: 'Once the plan is approved, Advertified coordinates the commercial, creative and operational work required to launch the exact campaign selected.',
    points: [
      { icon: Handshake, text: 'Media-partner and creator bookings' },
      { icon: Palette, text: 'Creative assets and production requirements' },
      { icon: ShieldCheck, text: 'Contracts, permits and launch readiness' },
    ],
    icon: Workflow,
    outputLabel: 'You see',
    outputTitle: 'One readiness view',
    outputText: 'Funding, bookings, creative, agreements and launch requirements remain visible as the campaign moves towards launch.',
    visual: 'readiness',
  },
  {
    shortTitle: 'Go live',
    eyebrow: 'Live delivery',
    title: 'Your campaign goes live—and the learning begins.',
    description: 'Follow campaign delivery, proof of performance and outcomes in one place. Every completed campaign strengthens the next planning decision.',
    points: [
      { icon: RadioTower, text: 'Live campaign status and delivery' },
      { icon: BadgeCheck, text: 'Proof linked to the actual booked media' },
      { icon: ChartNoAxesCombined, text: 'Outcomes, measurement and learnings' },
    ],
    icon: Rocket,
    outputLabel: 'You receive',
    outputTitle: 'A campaign you can follow',
    outputText: 'From the original brief to the proof received, the commercial and operational journey stays connected.',
    visual: 'delivery',
  },
] as const;

function StageVisual({ type }: { type: (typeof stages)[number]['visual'] }) {
  if (type === 'brief') {
    return (
      <div className="hiw-brief-preview" aria-label="Campaign brief summary">
        <span><Target size={17} aria-hidden="true" /> Build consideration</span>
        <span><Users size={17} aria-hidden="true" /> Priority audience</span>
        <span><Map size={17} aria-hidden="true" /> Target markets</span>
        <span><CalendarRange size={17} aria-hidden="true" /> Campaign timing</span>
      </div>
    );
  }
  if (type === 'inventory') {
    return (
      <div className="hiw-inventory-preview" aria-label="Media channels considered together">
        <figure><img src="/assets/media-inventory/television.jpg" alt="" /><figcaption>Television</figcaption></figure>
        <figure><img src="/assets/media-inventory/radio.jpg" alt="" /><figcaption>Radio</figcaption></figure>
        <figure><img src="/assets/media-inventory/out-of-home.jpg" alt="" /><figcaption>Out of home</figcaption></figure>
      </div>
    );
  }
  if (type === 'plan') {
    return (
      <div className="hiw-plan-preview" aria-label="Illustrative media roles">
        <span><Tv size={17} aria-hidden="true" /> Broad attention</span>
        <span><Radio size={17} aria-hidden="true" /> Repetition and relevance</span>
        <span><Smartphone size={17} aria-hidden="true" /> Response and retargeting</span>
        <span><Map size={17} aria-hidden="true" /> Place-based presence</span>
      </div>
    );
  }
  if (type === 'options') {
    return (
      <div className="hiw-options-preview" aria-label="Three proposal choices">
        <div><WalletCards size={18} aria-hidden="true" /><span><strong>Client-aligned</strong><small>Focused on the initial investment</small></span></div>
        <div className="is-recommended"><BadgeCheck size={18} aria-hidden="true" /><span><strong>Recommended</strong><small>The strongest balanced plan</small></span><em>Recommended</em></div>
        <div><ArrowUpRight size={18} aria-hidden="true" /><span><strong>Opportunity</strong><small>More reach, depth or duration</small></span></div>
      </div>
    );
  }
  if (type === 'readiness') {
    return (
      <div className="hiw-readiness-preview" aria-label="Campaign readiness areas">
        <span><BadgeCheck size={18} aria-hidden="true" /> Approved plan</span>
        <span><BadgeCheck size={18} aria-hidden="true" /> Funding</span>
        <span><BadgeCheck size={18} aria-hidden="true" /> Bookings</span>
        <span><BadgeCheck size={18} aria-hidden="true" /> Creative</span>
        <span><FileCheck2 size={18} aria-hidden="true" /> Launch evidence</span>
      </div>
    );
  }
  return (
    <div className="hiw-delivery-preview" aria-label="Campaign delivery journey">
      <span><RadioTower size={18} aria-hidden="true" /><small>Live</small></span>
      <i aria-hidden="true" />
      <span><BadgeCheck size={18} aria-hidden="true" /><small>Proof</small></span>
      <i aria-hidden="true" />
      <span><ChartNoAxesCombined size={18} aria-hidden="true" /><small>Learn</small></span>
    </div>
  );
}

export function PublicHowItWorksPage() {
  const [activeStage, setActiveStage] = useState(0);
  const stageRefs = useRef<Array<HTMLElement | null>>([]);

  useEffect(() => {
    if (!('IntersectionObserver' in window)) return undefined;
    const observer = new IntersectionObserver((entries) => {
      const visible = entries
        .filter(entry => entry.isIntersecting)
        .sort((left, right) => right.intersectionRatio - left.intersectionRatio)[0];
      if (visible) setActiveStage(Number((visible.target as HTMLElement).dataset.stage));
    }, { rootMargin: '-28% 0px -46% 0px', threshold: [0.1, 0.35, 0.6] });
    stageRefs.current.forEach(stage => stage && observer.observe(stage));
    return () => observer.disconnect();
  }, []);

  const jumpToStage = (index: number) => {
    setActiveStage(index);
    stageRefs.current[index]?.scrollIntoView({
      behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth',
      block: 'center',
    });
  };

  const progress = `${(activeStage / (stages.length - 1)) * 100}%`;

  return (
    <div className="hiw-page" style={{ '--hiw-progress': progress } as React.CSSProperties}>
      <HowItWorksHero />
      <JourneyOverview activeStage={activeStage} onSelect={jumpToStage} />
      <JourneyStages activeStage={activeStage} stageRefs={stageRefs} />
      <HowItWorksCta />
    </div>
  );
}

function HowItWorksHero() {
  return (
    <section className="hiw-hero">
      <div className="shell">
        <span className="hiw-kicker"><Route size={16} aria-hidden="true" /> How Advertified works</span>
        <h1>One clear route from campaign ambition to live media.</h1>
        <p>You bring the business objective. Advertified brings together market intelligence, expert media planning and accountable delivery in the selected market.</p>
        <div className="hiw-channels" aria-label="Supported media channels">
          <span><Radio size={17} aria-hidden="true" /> Radio</span>
          <span><Tv size={17} aria-hidden="true" /> Television</span>
          <span><Map size={17} aria-hidden="true" /> Out of home</span>
          <span><FileCheck2 size={17} aria-hidden="true" /> Print</span>
          <span><Smartphone size={17} aria-hidden="true" /> Digital</span>
          <span><Users size={17} aria-hidden="true" /> Influencer</span>
        </div>
      </div>
    </section>
  );
}

function JourneyOverview({ activeStage, onSelect }: { activeStage: number; onSelect: (index: number) => void }) {
  return (
    <nav className="hiw-overview" aria-label="Campaign journey">
      <div className="shell hiw-overview__inner">
        <span className="hiw-overview__track" aria-hidden="true"><i /></span>
        {stages.map((stage, index) => (
          <button type="button" key={stage.shortTitle} className="hiw-overview__step" aria-current={activeStage === index ? 'step' : undefined} onClick={() => onSelect(index)}>
            <span>{index + 1}</span><small>{stage.shortTitle}</small>
          </button>
        ))}
      </div>
    </nav>
  );
}

function JourneyStages({ activeStage, stageRefs }: { activeStage: number; stageRefs: { current: Array<HTMLElement | null> } }) {
  return (
    <section className="shell hiw-journey" aria-label="The Advertified campaign journey">
      <span className="hiw-journey__spine" aria-hidden="true"><i /></span>
      {stages.map((stage, index) => {
        const StageIcon = stage.icon;
        return (
          <article className={`hiw-stage${activeStage === index ? ' is-active' : ''}`} data-stage={index} key={stage.shortTitle} ref={element => { stageRefs.current[index] = element; }}>
            <div className="hiw-stage__copy">
              <span className="hiw-stage__eyebrow">{String(index + 1).padStart(2, '0')} · {stage.eyebrow}</span>
              <h2>{stage.title}</h2><p>{stage.description}</p>
              <ul>{stage.points.map(point => { const PointIcon = point.icon; return <li key={point.text}><PointIcon size={18} aria-hidden="true" /><span>{point.text}</span></li>; })}</ul>
            </div>
            <div className="hiw-stage__node" aria-hidden="true"><StageIcon size={25} /></div>
            <div className="hiw-stage__output">
              <span className="hiw-stage__output-label"><ArrowUpRight size={15} aria-hidden="true" /> {stage.outputLabel}</span>
              <strong>{stage.outputTitle}</strong><p>{stage.outputText}</p><StageVisual type={stage.visual} />
            </div>
          </article>
        );
      })}
    </section>
  );
}

function HowItWorksCta() {
  return (
    <section className="hiw-cta"><div className="shell">
      <span className="hiw-kicker"><Target size={16} aria-hidden="true" /> Start with the objective</span>
      <h2>You do not need to know which station, screen or publication to buy.</h2>
      <p>Tell us what the campaign must achieve. Advertified and its media experts will help shape the route.</p>
      <Link href="/start" className="btn primary">Start your campaign brief <ArrowUpRight size={18} aria-hidden="true" /></Link>
    </div></section>
  );
}

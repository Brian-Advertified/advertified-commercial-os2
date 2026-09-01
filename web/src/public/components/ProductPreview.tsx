import {
  BadgeCheck,
  ClipboardCheck,
  FileCheck2,
  LayoutDashboard,
  Megaphone,
  PackageSearch,
  SearchCheck,
  ShieldCheck,
  UsersRound,
} from 'lucide-react';

const navigation = [
  { label: 'Command Centre', icon: LayoutDashboard },
  { label: 'Campaigns', icon: Megaphone },
  { label: 'Inventory', icon: PackageSearch },
  { label: 'Proposals', icon: FileCheck2 },
  { label: 'Human Tasks', icon: UsersRound },
] as const;

const workflow = [
  { label: 'Brief and evidence', detail: 'Business context stays linked to its source.', icon: SearchCheck },
  { label: 'Strategy and scenarios', detail: 'Recommendations remain reviewable before consequential decisions.', icon: ClipboardCheck },
  { label: 'Media and supply', detail: 'Rates and availability must be commercially verified.', icon: PackageSearch },
  { label: 'Proposal and release', detail: 'The exact PDF version is approved before a separate send action.', icon: BadgeCheck },
] as const;

export function ProductPreview() {
  return (
    <div className="public-workflow-preview" aria-label="Illustrative Advertified governed campaign workflow">
      <aside className="public-workflow-preview__sidebar">
        <img src="/advertified-wordmark.png" width="2000" height="220" alt="Advertified" />
        <nav aria-label="Illustrative product navigation">
          {navigation.map(({ label, icon: Icon }, index) => (
            <span className={index === 1 ? 'is-active' : ''} key={label}><Icon size={14} />{label}</span>
          ))}
        </nav>
      </aside>

      <section className="public-workflow-preview__content">
        <header>
          <div>
            <span className="public-workflow-preview__eyebrow">GOVERNED CAMPAIGN JOURNEY</span>
            <h2>From commercial objective to client-ready proposal</h2>
          </div>
          <span className="public-workflow-preview__label">Illustrative product view</span>
        </header>

        <div className="public-workflow-preview__objective">
          <span>Commercial objective</span>
          <strong>Build the right campaign around the business outcome.</strong>
          <p>Advertified coordinates the machine work while evidence, supply truth and human authority remain visible.</p>
        </div>

        <div className="public-workflow-preview__stages">
          {workflow.map(({ label, detail, icon: Icon }, index) => (
            <article key={label}>
              <span className="public-workflow-preview__number">0{index + 1}</span>
              <span className="public-workflow-preview__icon"><Icon size={17} /></span>
              <div><strong>{label}</strong><p>{detail}</p></div>
            </article>
          ))}
        </div>

        <footer>
          <ShieldCheck size={18} />
          <p><strong>Human authority stays intact.</strong> No silent proposal send, media booking or spend commitment.</p>
        </footer>
      </section>
    </div>
  );
}

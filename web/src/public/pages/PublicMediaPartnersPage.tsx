import { PublicCta } from '../components/PublicCta';
import { PublicPageHero } from '../components/PublicPageHero';
import { mediaInventoryPartners } from '../data/mediaInventoryPartners';

export function PublicMediaPartnersPage() {
  return (
    <>
      <PublicPageHero eyebrow="FOR MEDIA OWNERS" title="Put quality media opportunities in front of better-shaped advertiser demand." introduction="Advertified helps campaign planners understand where your media fits, why it matters and what is required to turn a recommendation into a confirmed campaign opportunity." />
      <section className="section"><div className="shell"><h2 className="partner-page-heading">MEDIA PARTNERS</h2><p className="lead">These brands are represented by current source records in Advertified's inventory catalogue. Placement, price and availability are verified for each campaign before recommendation or release.</p><div className="partner-cards">{mediaInventoryPartners.map((partner) => <article key={partner.name}><img className="partner-card-logo" src={partner.assetPath} alt={`${partner.name} logo`} width={partner.width} height={partner.height} /><small>Catalogue source</small><p>Inventory records help planners understand formats, locations and potential campaign roles without implying current availability.</p></article>)}</div></div></section>
      <section className="section muted"><div className="shell split"><div><span className="eyebrow">WHY PARTNER WITH ADVERTIFIED</span><h2>Make your inventory easier to understand, recommend and activate.</h2><p>Share useful commercial and inventory information once, keep it current and work directly with Advertified when a campaign opportunity becomes relevant.</p></div><ol className="number-list"><li>Introduce your organisation and media offering</li><li>Complete the agreements relevant to your media type</li><li>Share useful audience, inventory and commercial information</li><li>Keep important changes and availability current</li><li>Coordinate suitable campaign opportunities with Advertified</li></ol></div></section>
      <PublicCta
        title="Want Advertified planners to understand your media offering?"
        description="Start a partner conversation and tell us what you represent, where it is available and the audiences it can help brands reach."
        actionLabel="Become a media partner"
        href="/register/media-owner"
      />
    </>
  );
}

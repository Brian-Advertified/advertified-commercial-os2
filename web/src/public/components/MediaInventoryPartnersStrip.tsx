import { campaignSocialPlatforms, mediaInventoryPartners } from '../data/mediaInventoryPartners';

const displayedPartners = [...mediaInventoryPartners, ...campaignSocialPlatforms];

function PartnerLogoSet({ duplicate = false }: { duplicate?: boolean }) {
  return (
    <div className="media-partner-set" aria-hidden={duplicate || undefined}>
      {displayedPartners.map((partner) => (
        <div
          className="media-partner-slot mock-partner-strip__slot"
          key={partner.name}
        >
          <img
            src={partner.assetPath}
            alt={duplicate ? '' : `${partner.name} logo`}
            width={partner.width}
            height={partner.height}
            loading={duplicate ? 'lazy' : 'eager'}
          />
        </div>
      ))}
    </div>
  );
}

export function MediaInventoryPartnersStrip({ expanded = false }: { expanded?: boolean }) {
  return (
    <section className={`mock-partner-strip media-inventory-partners${expanded ? ' is-expanded' : ''}`} aria-labelledby={expanded ? 'partners-page-title' : 'partners-strip-title'}>
      <div className="shell">
        <header className="public-section-heading public-section-heading--title-only media-inventory-partners__header">
          <h2 id={expanded ? 'partners-page-title' : 'partners-strip-title'}>MEDIA PARTNERS</h2>
        </header>
        <div className="media-partner-scroll" tabIndex={0} aria-label="Media brands and supported social channels">
          <div className="media-partner-track">
            <PartnerLogoSet />
            <PartnerLogoSet duplicate />
          </div>
        </div>
      </div>
    </section>
  );
}

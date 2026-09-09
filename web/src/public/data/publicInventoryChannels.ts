export interface PublicInventoryChannelPresentation {
  label: string;
  directoryTitle: string;
  image?: string;
}

const publicInventoryChannels: Record<string, PublicInventoryChannelPresentation> = {
  radio: { label: 'Radio', directoryTitle: 'Radio stations', image: '/assets/media-inventory/radio-real.jpg' },
  television: { label: 'Television', directoryTitle: 'Television channels', image: '/assets/media-inventory/television-real.jpg' },
  print: { label: 'Print', directoryTitle: 'Print publications', image: '/assets/media-inventory/print-real.jpg' },
  out_of_home: { label: 'Outdoor advertising', directoryTitle: 'Static billboard and digital screen sites', image: '/assets/media-inventory/out-of-home-real.jpg' },
  digital: { label: 'Digital', directoryTitle: 'Digital publishers and platforms', image: '/assets/media-inventory/digital-real.jpg' },
  social_media: { label: 'Social media', directoryTitle: 'Social accounts', image: '/assets/media-inventory/digital-real.jpg' },
  experiential: { label: 'Experiential', directoryTitle: 'Activations', image: '/assets/media-inventory/experiential-real.jpg' },
  influencer: { label: 'Influencer', directoryTitle: 'Creators and influencers' },
  multi_channel: { label: 'Multi-channel', directoryTitle: 'Multi-channel media units' },
};

export function getPublicInventoryChannelPresentation(channel: string): PublicInventoryChannelPresentation {
  const fallbackLabel = channel.replaceAll('_', ' ');
  return publicInventoryChannels[channel] ?? {
    label: fallbackLabel,
    directoryTitle: `${fallbackLabel} media units`,
  };
}

const inventoryCountLabels: Record<string, string> = {
  canonical_radio_stations: 'Radio stations', canonical_television_channels: 'TV channels',
  canonical_publications: 'Publications', published_inventory_products: 'Published inventory products',
};

export function publicInventoryCountLabel(basis: string, channel: string): string {
  if (basis === 'published_inventory_products') return `${getPublicInventoryChannelPresentation(channel).label} products`;
  return inventoryCountLabels[basis] ?? 'Published media units';
}

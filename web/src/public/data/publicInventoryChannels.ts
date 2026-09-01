export interface PublicInventoryChannelPresentation {
  label: string;
  directoryTitle: string;
  image?: string;
}

const publicInventoryChannels: Record<string, PublicInventoryChannelPresentation> = {
  radio: { label: 'Radio', directoryTitle: 'Radio stations', image: '/assets/media-inventory/radio-real.jpg' },
  television: { label: 'Television', directoryTitle: 'Television channels', image: '/assets/media-inventory/television-real.jpg' },
  print: { label: 'Print', directoryTitle: 'Print publications', image: '/assets/media-inventory/print-real.jpg' },
  out_of_home: { label: 'Out of home', directoryTitle: 'Out-of-home media owners', image: '/assets/media-inventory/out-of-home-real.jpg' },
  digital: { label: 'Digital', directoryTitle: 'Digital publishers and platforms', image: '/assets/media-inventory/digital-real.jpg' },
  social_media: { label: 'Social media', directoryTitle: 'Social media platforms', image: '/assets/media-inventory/digital-real.jpg' },
  experiential: { label: 'Experiential', directoryTitle: 'Experiential media owners', image: '/assets/media-inventory/experiential-real.jpg' },
  influencer: { label: 'Influencer', directoryTitle: 'Creator and influencer media owners' },
  multi_channel: { label: 'Multi-channel', directoryTitle: 'Multi-channel media owners' },
};

export function getPublicInventoryChannelPresentation(channel: string): PublicInventoryChannelPresentation {
  const fallbackLabel = channel.replaceAll('_', ' ');
  return publicInventoryChannels[channel] ?? {
    label: fallbackLabel,
    directoryTitle: `${fallbackLabel} media owners`,
  };
}

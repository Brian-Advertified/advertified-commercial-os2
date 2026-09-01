export const registrationTypes = {
  advertiser: {
    title: 'Advertiser registration',
    introduction: 'Tell us about your business, decision-maker and intended use of Advertified.',
    organisationLabel: 'Registered or trading business name',
    profileLabel: 'Brand, industry and primary markets',
    relationshipLabel: 'Your role in approving campaigns',
  },
  agency: {
    title: 'Agency registration',
    introduction: 'Tell us about your agency, services and the clients or campaigns you are authorised to represent.',
    organisationLabel: 'Registered agency name',
    profileLabel: 'Services, specialities and markets',
    relationshipLabel: 'Client relationship and requested access scope',
  },
  'media-owner': {
    title: 'Media owner registration',
    introduction: 'Tell us about your organisation, media inventory and authority to offer it.',
    organisationLabel: 'Registered media owner or supplier name',
    profileLabel: 'Channels, inventory types and geographic coverage',
    relationshipLabel: 'Authority to sell or represent the inventory',
  },
  creator: {
    title: 'Creator registration',
    introduction: 'Tell us about your public profile, audience and commercial content work.',
    organisationLabel: 'Public or trading name',
    profileLabel: 'Channels, content categories and profile links',
    relationshipLabel: 'Audience geography and commercial services',
  },
} as const;

export type RegistrationType = keyof typeof registrationTypes;

export function registrationTypeForSlug(slug: string): RegistrationType | null {
  return slug in registrationTypes ? slug as RegistrationType : null;
}

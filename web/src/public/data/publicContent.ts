export interface PublicLink {
  label: string;
  href: string;
}

export const primaryNavigation: readonly PublicLink[] = [
  { label: 'Solutions', href: '/solutions' },
  { label: 'Advertise Now, Pay Later', href: '/pay-later' },
];

export const footerNavigation = {
  explore: [
    { label: 'How it works', href: '/how-it-works' },
    { label: 'Solutions', href: '/solutions' },
    { label: 'Media partners', href: '/media-partners' },
    { label: 'Packages', href: '/packages' },
  ],
  payLater: [
    { label: 'How it works', href: '/pay-later' },
    { label: 'Start a campaign', href: '/start' },
  ],
  workWithUs: [
    { label: 'Join Advertified', href: '/register' },
    { label: 'Start a campaign', href: '/start' },
    { label: 'Become a media partner', href: '/register/media-owner' },
    { label: 'Contact Advertified', href: '/contact' },
    { label: 'FAQ', href: '/faq' },
  ],
} satisfies Record<string, readonly PublicLink[]>;

export const journeySteps = [
  { number: '01', title: 'Share the campaign ambition', description: 'Turn the objective, audience, geography, timing and investment range into a focused brief.' },
  { number: '02', title: 'Understand the opportunity', description: 'Evaluate relevant inventory, commercial evidence, geography and audience fit with expert review.' },
  { number: '03', title: 'Build the media strategy', description: 'Shape an expert-adjustable channel mix around what the campaign must achieve.' },
  { number: '04', title: 'Choose a clear proposal', description: 'Compare three executable campaign options with visible trade-offs and exact allocations.' },
  { number: '05', title: 'Coordinate campaign readiness', description: 'Bring funding, bookings, production, contracts and launch requirements together.' },
  { number: '06', title: 'Go live, prove and learn', description: 'Follow delivery evidence, measurement and learning back to the original brief.' },
] as const;

export const investmentBands = [
  {
    name: 'Launch',
    range: 'Entry investment',
    description: 'A focused campaign around one clear objective, audience or market opportunity.',
    unlocks: ['A disciplined channel role', 'Focused geographic or audience reach', 'A clear response or awareness objective'],
    mediaExamples: ['Radio', 'Digital', 'Social media', 'Local out of home'],
  },
  {
    name: 'Boost',
    range: 'Growth investment',
    description: 'More room to coordinate channels, strengthen production and extend campaign reach.',
    unlocks: ['A more connected media mix', 'Broader market coverage', 'Stronger creative and delivery support'],
    mediaExamples: ['Radio', 'Out of home', 'Digital', 'Influencer'],
  },
  {
    name: 'Scale',
    range: 'Expanded investment',
    description: 'A larger campaign with greater reach, sequencing and measurement ambition.',
    unlocks: ['Multi-channel campaign roles', 'Deeper reach or frequency', 'More robust evidence and measurement'],
    mediaExamples: ['Television', 'Radio', 'Out of home', 'Digital'],
  },
  {
    name: 'Dominance',
    range: 'Bespoke investment',
    description: 'A bespoke, high-ambition campaign with no fixed upper limit.',
    unlocks: ['Sustained market presence', 'Broader channel and market coordination', 'A bespoke delivery and learning plan'],
    mediaExamples: ['Television', 'Radio', 'Out of home', 'Print', 'Digital', 'Creators'],
  },
] as const;

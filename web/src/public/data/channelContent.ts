export interface ChannelDefinition {
  slug: string;
  name: string;
  shortName: string;
  eyebrow: string;
  introduction: string;
  roles: readonly string[];
  evidence: readonly string[];
}

export const channels: readonly ChannelDefinition[] = [
  {
    slug: 'ooh', name: 'Out-of-home and digital screens', shortName: 'Out of home',
    eyebrow: 'Be seen where the market moves',
    introduction: 'Build visible presence around the places your audience lives, travels, shops and works—with each location chosen for a clear campaign role.',
    roles: ['Own priority locations', 'Reach people close to retail or commuter moments', 'Create high-impact launch visibility'],
    evidence: ['Audience and location fit', 'The right format, placement and campaign period', 'Production, installation and site requirements'],
  },
  {
    slug: 'radio', name: 'Radio', shortName: 'Radio',
    eyebrow: 'Build familiarity through sound',
    introduction: 'Use language, personality, frequency and regional relevance to make the campaign part of the audience’s daily rhythm.',
    roles: ['Reach regional or community audiences', 'Reinforce a memorable campaign message', 'Prompt conversation, visits or response'],
    evidence: ['Station and audience fit', 'The role of frequency and scheduling', 'Creative, production and booking requirements'],
  },
  {
    slug: 'television', name: 'Television', shortName: 'Television',
    eyebrow: 'Tell a bigger story at scale',
    introduction: 'Combine sight, sound and cultural context to build broad awareness, demonstrate value and create a high-attention brand moment.',
    roles: ['Build broad-market awareness', 'Demonstrate a product or proposition', 'Create high-attention brand storytelling'],
    evidence: ['Audience and programme context', 'Flighting, production and material deadlines', 'Rights, clearances and confirmed placement'],
  },
  {
    slug: 'print', name: 'Print and newspaper', shortName: 'Print',
    eyebrow: 'Add detail, context and credibility',
    introduction: 'Reach readers in a trusted editorial environment when the campaign needs considered attention, useful detail or community relevance.',
    roles: ['Build credibility around a detailed message', 'Reach professional or community audiences', 'Support tactical announcements and offers'],
    evidence: ['Publication, audience and placement fit', 'Format, material specification and deadline', 'Distribution and campaign timing'],
  },
  {
    slug: 'digital', name: 'Digital and social', shortName: 'Digital',
    eyebrow: 'Connect attention to measurable action',
    introduction: 'Use digital and social media to move audiences from discovery to response while keeping targeting, creative and measurement tied to the campaign goal.',
    roles: ['Capture demand and response', 'Sequence messages across the customer journey', 'Reconnect with interested audiences'],
    evidence: ['Audience, platform and placement fit', 'Creative and response journey', 'Measurement source and reporting limits'],
  },
  {
    slug: 'influencers', name: 'Influencer marketing', shortName: 'Influencers',
    eyebrow: 'Build relevance through trusted voices',
    introduction: 'Work with suitable creators to translate the campaign into credible stories, demonstrations and community conversations.',
    roles: ['Reach relevant creator communities', 'Demonstrate products in a human way', 'Create platform-native campaign stories'],
    evidence: ['Creator and audience fit', 'Deliverables, usage rights and disclosures', 'Published content and performance evidence'],
  },
] as const;

export function channelForSlug(slug: string) {
  return channels.find((channel) => channel.slug === slug);
}

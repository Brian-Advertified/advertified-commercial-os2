export interface PublicRouteMetadata {
  path: string;
  title: string;
  description: string;
}

export const publicRoutes = [
  { path: '/', title: 'Advertified | From business challenge to campaign impact', description: 'Plan and coordinate stronger campaigns across local and international media inventory with experienced guidance, clear investment choices and accountable delivery.' },
  { path: '/platform', title: 'Campaign intelligence and delivery platform | Advertified', description: 'See how Advertified connects the campaign brief, media intelligence, strategy, proposals and delivery evidence in one guided journey.' },
  { path: '/how-it-works', title: 'How Advertified works', description: 'Follow the guided campaign journey from business goal and media strategy to proposal, coordination, delivery evidence and learning.' },
  { path: '/solutions', title: 'Cross-media advertising solutions | Advertified', description: 'Build the right role for traditional, digital and creator media around your audience, geography, investment and campaign outcome.' },
  { path: '/solutions/ooh', title: 'OOH and digital screen advertising | Advertified', description: 'Plan a purposeful out-of-home campaign around the locations, audiences and moments that matter.' },
  { path: '/solutions/radio', title: 'Radio advertising | Advertified', description: 'Use radio language, frequency and regional relevance to build familiarity and response.' },
  { path: '/solutions/television', title: 'Television advertising | Advertified', description: 'Use television to combine sight, sound and cultural context in a high-attention campaign role.' },
  { path: '/solutions/print', title: 'Print and newspaper advertising | Advertified', description: 'Use print media to add detail, trusted context and credibility to the campaign.' },
  { path: '/solutions/digital', title: 'Digital and social advertising | Advertified', description: 'Connect digital attention to measurable action with a clear audience, creative and measurement plan.' },
  { path: '/solutions/influencers', title: 'Influencer marketing | Advertified', description: 'Build creator-led relevance through suitable voices, clear deliverables and connected campaign evidence.' },
  { path: '/media-partners', title: 'Media catalogue sources | Advertified', description: 'See media brands represented by source records in the Advertified catalogue and learn how media owners can begin governed onboarding.' },
  { path: '/packages', title: 'Campaign investment | Advertified', description: 'Explore campaign investment bands and understand what different levels of media weight can unlock.' },
  { path: '/pay-later', title: 'Advertise Now, Pay Later | Advertified', description: 'Learn about the independent campaign finance referral available after an approved Advertified proposal.' },
  { path: '/resources', title: 'Campaign planning guidance | Advertified', description: 'Use four practical principles to strengthen the brief, media roles, investment choices and campaign evidence plan.' },
  { path: '/register', title: 'Join Advertified', description: 'Choose the right Advertified access path for an advertiser, agency, media owner, supplier, creator or influencer.' },
  { path: '/register/advertiser', title: 'Advertiser onboarding | Advertified', description: 'Contact Advertified to begin governed advertiser onboarding and access review.' },
  { path: '/register/agency', title: 'Agency onboarding | Advertified', description: 'Contact Advertified to begin governed agency onboarding and access review.' },
  { path: '/register/media-owner', title: 'Media owner onboarding | Advertified', description: 'Contact Advertified to begin governed media-owner onboarding and inventory verification.' },
  { path: '/register/creator', title: 'Creator onboarding | Advertified', description: 'Contact Advertified to begin governed creator onboarding and profile verification.' },
  { path: '/about', title: 'About Advertified', description: 'Learn how Advertified makes advertising easier to understand and connects businesses with media opportunities across markets.' },
  { path: '/faq', title: 'Frequently asked questions | Advertified', description: 'Answers about starting a campaign, media recommendations, investment options, delivery evidence and campaign finance.' },
  { path: '/contact', title: 'Contact Advertified', description: 'Start a conversation about a campaign, agency relationship, media partnership or another Advertified opportunity.' },
  { path: '/start', title: 'Plan a campaign | Advertified', description: 'Share your business challenge, audience, geography, timing and investment range with an Advertified campaign specialist.' },
  { path: '/privacy', title: 'Privacy policy status | Advertified', description: 'Check publication status for Advertified privacy information and find the privacy contact.' },
  { path: '/terms-of-service', title: 'Terms and conditions status | Advertified', description: 'Check publication status for Advertified commercial terms and find the commercial contact.' },
  { path: '/cookie-policy', title: 'Cookie policy status | Advertified', description: 'Check publication status for Advertified cookie information and find the privacy contact.' },
] as const satisfies readonly PublicRouteMetadata[];

const publicPaths = new Set<string>(publicRoutes.map((route) => route.path));
const publicMediaNetworkPath = /^\/media-network\/[a-z0-9_]+$/u;
const publicAliases = new Map<string, string>([
  ['/terms', '/terms-of-service'],
  ['/cookies', '/cookie-policy'],
] as const);

export const publicNotFoundMetadata: PublicRouteMetadata = {
  path: '',
  title: 'Page not found | Advertified',
  description: 'The requested Advertified public page could not be found.',
};

export function isPublicMarketingPath(path: string) {
  const normalizedPath = normalizePath(path);
  return publicPaths.has(normalizedPath) || publicAliases.has(normalizedPath) || publicMediaNetworkPath.test(normalizedPath);
}

const authenticatedApplicationPrefixes = [
  '/command', '/campaigns', '/inventory', '/suppliers', '/influencers', '/market',
  '/proposal', '/human-tasks', '/opportunities', '/accounts', '/library',
  '/pilot-report', '/settings', '/orchestration', '/agent',
  '/commercial-runs', '/proof', '/delivery', '/reconciliation', '/transaction-history',
  '/transactions', '/media-requirements', '/media-plan', '/workspace',
] as const;

export function isAuthenticatedApplicationPath(path: string) {
  const normalizedPath = normalizePath(path);
  return authenticatedApplicationPrefixes.some((prefix) => (
    normalizedPath === prefix || normalizedPath.startsWith(`${prefix}/`)
  ));
}

export function metadataForPath(path: string): PublicRouteMetadata {
  const normalizedPath = normalizePath(path);
  const canonicalPath = publicAliases.get(normalizedPath) ?? normalizedPath;
  const staticMetadata = publicRoutes.find((route) => route.path === canonicalPath);
  if (staticMetadata) return staticMetadata;
  const networkMatch = normalizedPath.match(/^\/media-network\/([a-z0-9_]+)$/u);
  if (networkMatch) {
    const channel = networkMatch[1].replaceAll('_', ' ');
    return {
      path: normalizedPath,
      title: `${channel} media owners | Advertified`,
      description: `Browse the ${channel} stations, channels, publications, platforms and media owners represented by active published Advertified inventory.`,
    };
  }
  return publicNotFoundMetadata;
}

function normalizePath(path: string) {
  if (path === '/') return path;
  return path.replace(/\/+$/u, '');
}

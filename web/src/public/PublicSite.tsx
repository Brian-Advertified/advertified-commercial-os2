import { lazy, Suspense, useEffect, type ReactNode } from 'react';
import { PublicLayout } from './components/PublicLayout';
import { channelForSlug } from './data/channelContent';
import { PublicAboutPage } from './pages/PublicAboutPage';
import { PublicChannelPage } from './pages/PublicChannelPage';
import { PublicFaqPage } from './pages/PublicFaqPage';
import { PublicHowItWorksPage } from './pages/PublicHowItWorksPage';
import { PublicMediaPartnersPage } from './pages/PublicMediaPartnersPage';
import { PublicMediaNetworkPage } from './pages/PublicMediaNetworkPage';
import { PublicLegalPage } from './pages/PublicLegalPage';
import { PublicNotFoundPage } from './pages/PublicNotFoundPage';
import { PublicPackagesPage } from './pages/PublicPackagesPage';
import { PublicPayLaterPage } from './pages/PublicPayLaterPage';
import { PublicPlatformPage } from './pages/PublicPlatformPage';
import { PublicResourcesPage } from './pages/PublicResourcesPage';
import { registrationTypeForSlug } from './data/registrationTypes';
import { PublicSolutionsPage } from './pages/PublicSolutionsPage';
import { metadataForPath, publicNotFoundMetadata } from './publicRoutes';

const PublicHomePage = lazy(() => import('./pages/PublicHomePage').then(module => ({ default: module.PublicHomePage })));
const PublicContactPage = lazy(() => import('./pages/PublicContactPage').then(module => ({ default: module.PublicContactPage })));
const PublicRegisterPage = lazy(() => import('./pages/PublicRegisterPage').then(module => ({ default: module.PublicRegisterPage })));
const PublicRegistrationDetailsPage = lazy(() => import('./pages/PublicRegistrationDetailsPage').then(module => ({ default: module.PublicRegistrationDetailsPage })));
const PublicStartCampaignPage = lazy(() => import('./pages/PublicStartCampaignPage').then(module => ({ default: module.PublicStartCampaignPage })));

const staticPages: Record<string, () => ReactNode> = {
  '/': () => <PublicHomePage />,
  '/platform': () => <PublicPlatformPage />,
  '/how-it-works': () => <PublicHowItWorksPage />,
  '/solutions': () => <PublicSolutionsPage />,
  '/media-partners': () => <PublicMediaPartnersPage />,
  '/packages': () => <PublicPackagesPage />,
  '/pay-later': () => <PublicPayLaterPage />,
  '/resources': () => <PublicResourcesPage />,
  '/register': () => <PublicRegisterPage />,
  '/about': () => <PublicAboutPage />,
  '/faq': () => <PublicFaqPage />,
  '/contact': () => <PublicContactPage />,
  '/start': () => <PublicStartCampaignPage />,
  '/privacy': () => <PublicLegalPage kind="privacy" />,
  '/terms-of-service': () => <PublicLegalPage kind="terms" />,
  '/terms': () => <PublicLegalPage kind="terms" />,
  '/cookie-policy': () => <PublicLegalPage kind="cookies" />,
  '/cookies': () => <PublicLegalPage kind="cookies" />,
};

export function PublicSite({ path }: { path: string }) {
  const normalizedPath = path === '/' ? path : path.replace(/\/+$/u, '');
  const content = pageForPath(normalizedPath);
  const notFound = content === null;

  useEffect(() => {
    window.scrollTo({ top: 0, behavior: 'auto' });
  }, [normalizedPath]);

  return (
    <PublicLayout metadata={notFound ? publicNotFoundMetadata : metadataForPath(normalizedPath)} notFound={notFound}>
      <Suspense fallback={<div className="public-route-loading" aria-label="Loading page" />}>
        {content ?? <PublicNotFoundPage />}
      </Suspense>
    </PublicLayout>
  );
}

function pageForPath(path: string): ReactNode | null {
  const staticPage = staticPages[path];
  if (staticPage) return staticPage();

  const mediaNetworkMatch = path.match(/^\/media-network\/([^/]+)$/u);
  if (mediaNetworkMatch) return <PublicMediaNetworkPage channel={mediaNetworkMatch[1]} />;

  const registrationMatch = path.match(/^\/register\/([^/]+)$/u);
  if (registrationMatch) {
    const registrationType = registrationTypeForSlug(registrationMatch[1]);
    return registrationType ? <PublicRegistrationDetailsPage type={registrationType} /> : null;
  }

  const channelMatch = path.match(/^\/solutions\/([^/]+)$/u);
  if (channelMatch) {
    const channel = channelForSlug(channelMatch[1]);
    return channel ? <PublicChannelPage channel={channel} /> : null;
  }
  return null;
}

import type { ReactNode } from 'react';
import type { PublicRouteMetadata } from '../publicRoutes';
import { CookieConsent } from './CookieConsent';
import { PublicFooter } from './PublicFooter';
import { PublicHeader } from './PublicHeader';
import { PublicSeo } from './PublicSeo';

export function PublicLayout({ children, metadata, notFound = false }: { children: ReactNode; metadata: PublicRouteMetadata; notFound?: boolean }) {
  return (
    <div className="advertified-public">
      <PublicSeo metadata={metadata} notFound={notFound} />
      <a className="public-skip-link" href="#public-main">Skip to main content</a>
      <PublicHeader />
      <main id="public-main">{children}</main>
      <PublicFooter />
      <CookieConsent />
    </div>
  );
}

import '../styles/public-site.css';
import '../styles/public-how-it-works-all.css';
import '../styles/public-registration.css';
import '../styles/public-chrome.css';
import '../styles/public-cookie-consent.css';
import '../styles/public-product-preview.css';
import '../styles/public-surfaces.css';
import { useLocation } from 'react-router-dom';
import { PublicSite } from './PublicSite';

export function PublicSiteRoute() {
  return <PublicSite path={useLocation().pathname} />;
}

import { BrandMark } from '../../components/BrandMark';
import { Link } from '../../routing/router';
import { footerNavigation } from '../data/publicContent';
import { OPEN_COOKIE_PREFERENCES_EVENT } from './CookieConsent';

export function PublicFooter() {
  return (
    <footer className="footer public-footer">
      <div className="shell footer-grid">
        <div>
          <Link href="/" className="public-logo-link public-logo-link--footer" aria-label="Advertified home"><BrandMark /></Link>
          <p>A guided evidence-backed operating system for media buying</p>
        </div>
        <FooterColumn title="Platform" links={footerNavigation.explore} />
        <FooterColumn title="Advertise Now, Pay Later" links={footerNavigation.payLater} />
        <FooterColumn title="Work with us" links={footerNavigation.workWithUs} />
      </div>
      <div className="shell footer-bottom">
        <span>© {new Date().getFullYear()} Advertified. All rights reserved.</span>
        <nav className="public-footer__legal" aria-label="Legal links">
          <Link href="/privacy">Privacy Policy</Link>
          <Link href="/terms-of-service">Terms and Conditions</Link>
          <Link href="/cookie-policy">Cookie Policy</Link>
          <button type="button" onClick={() => window.dispatchEvent(new Event(OPEN_COOKIE_PREFERENCES_EVENT))}>Cookie settings</button>
        </nav>
        <span className="public-footer__locations">Stellenbosch <b aria-hidden="true">•</b> Johannesburg <b aria-hidden="true">•</b> Nairobi</span>
      </div>
    </footer>
  );
}

function FooterColumn({ title, links }: { title: string; links: readonly { label: string; href: string }[] }) {
  return (
    <nav aria-label={`${title} links`}>
      <h4>{title}</h4>
      {links.map((item) => <Link key={`${item.href}:${item.label}`} href={item.href}>{item.label}</Link>)}
    </nav>
  );
}

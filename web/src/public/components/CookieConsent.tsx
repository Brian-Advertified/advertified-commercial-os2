import { Cookie, ShieldCheck } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Link } from '../../routing/router';

export const COOKIE_CONSENT_COOKIE_NAME = 'advertified_cookie_consent';
export const OPEN_COOKIE_PREFERENCES_EVENT = 'advertified:open-cookie-preferences';

const necessaryConsentValue = 'necessary-v1';

function hasStoredConsent(): boolean {
  return document.cookie.split(';').some((entry) => {
    const [name, value] = entry.trim().split('=', 2);
    return name === COOKIE_CONSENT_COOKIE_NAME && value === necessaryConsentValue;
  });
}

function storeNecessaryConsent() {
  const secure = window.location.protocol === 'https:' ? '; Secure' : '';
  document.cookie = `${COOKIE_CONSENT_COOKIE_NAME}=${necessaryConsentValue}; Path=/; Max-Age=31536000; SameSite=Lax${secure}`;
}

export function CookieConsent() {
  const [open, setOpen] = useState(() => !hasStoredConsent());

  useEffect(() => {
    const reopen = () => setOpen(true);
    window.addEventListener(OPEN_COOKIE_PREFERENCES_EVENT, reopen);
    return () => window.removeEventListener(OPEN_COOKIE_PREFERENCES_EVENT, reopen);
  }, []);

  const acceptNecessary = () => {
    storeNecessaryConsent();
    window.dispatchEvent(new CustomEvent('advertified:cookie-consent-changed', {
      detail: { consent: necessaryConsentValue, acknowledgedAt: new Date().toISOString() },
    }));
    setOpen(false);
  };

  if (!open) return null;

  return (
    <aside className="public-cookie-consent" role="dialog" aria-labelledby="cookie-consent-title" aria-describedby="cookie-consent-description">
      <div className="public-cookie-consent__icon" aria-hidden="true"><Cookie size={21} /></div>
      <div className="public-cookie-consent__copy">
        <h2 id="cookie-consent-title">Cookie preferences</h2>
        <p id="cookie-consent-description">
          We use essential browser storage for secure sign-in and to remember this choice.
          Optional analytics and marketing cookies are not currently in use.
        </p>
        <span><ShieldCheck size={14} aria-hidden="true" /> Necessary storage only</span>
      </div>
      <div className="public-cookie-consent__actions">
        <button type="button" className="btn primary" onClick={acceptNecessary}>Accept necessary</button>
        <Link href="/cookie-policy" className="public-cookie-consent__policy">Cookie policy</Link>
      </div>
    </aside>
  );
}

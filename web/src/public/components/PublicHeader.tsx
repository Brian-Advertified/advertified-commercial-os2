import { Menu, X } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';
import { BrandMark } from '../../components/BrandMark';
import { usePathname } from '../../routing/location';
import { Link } from '../../routing/router';
import { primaryNavigation } from '../data/publicContent';

export function PublicHeader() {
  const path = usePathname();
  const [open, setOpen] = useState(false);
  const menuButton = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    if (!open) return;
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setOpen(false);
        menuButton.current?.focus();
      }
    };
    document.addEventListener('keydown', closeOnEscape);
    return () => document.removeEventListener('keydown', closeOnEscape);
  }, [open]);

  const closeNavigation = () => setOpen(false);

  return (
    <header className="site-header public-header">
      <div className="shell header-inner">
        <Link href="/" className="public-logo-link" aria-label="Advertified home" onClick={closeNavigation}>
          <BrandMark />
        </Link>
        <button
          ref={menuButton}
          className="menu-btn"
          type="button"
          aria-label={open ? 'Close navigation' : 'Open navigation'}
          aria-expanded={open}
          aria-controls="public-navigation"
          onClick={() => setOpen((current) => !current)}
        >
          {open ? <X aria-hidden="true" /> : <Menu aria-hidden="true" />}
        </button>
        <nav id="public-navigation" className={`nav${open ? ' open' : ''}`} aria-label="Primary navigation">
          {primaryNavigation.map((item) => <Link key={item.href} href={item.href} onClick={closeNavigation} className={currentPath(path, item.href) ? 'active' : undefined} aria-current={currentPath(path, item.href) ? 'page' : undefined}>{item.label}</Link>)}
          <div className="mobile-actions">
            <Link className="btn ghost" href="/sign-in" onClick={closeNavigation}>Log in</Link>
            <Link className="btn secondary" href="/register" onClick={closeNavigation}>Register</Link>
          </div>
        </nav>
        <div className="header-actions">
          <Link className="btn ghost" href="/sign-in">Log in</Link>
          <Link className="btn primary" href="/register">Register <span aria-hidden="true">→</span></Link>
        </div>
      </div>
    </header>
  );
}

function currentPath(path: string, href: string) {
  return path === href || (href === '/solutions' && path.startsWith('/solutions/'));
}

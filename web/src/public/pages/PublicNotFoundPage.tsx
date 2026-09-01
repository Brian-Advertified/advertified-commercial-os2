import { Link } from '../../routing/router';

export function PublicNotFoundPage() {
  return (
    <section className="auth-page public-not-found"><div className="auth-card"><h1>Page not found</h1><p>The public page you requested does not exist or has moved.</p><Link className="btn primary" href="/">Return home</Link></div></section>
  );
}

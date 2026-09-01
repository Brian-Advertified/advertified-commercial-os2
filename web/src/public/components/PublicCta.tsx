import { ArrowUpRight } from 'lucide-react';
import { Link } from '../../routing/router';

export function PublicCta({
  title = 'Ready to turn the business challenge into a campaign?',
  description = 'Start with a conversation. An Advertified campaign specialist will help shape the brief and the right next step.',
  actionLabel = 'Plan a campaign',
  href = '/start',
}: {
  title?: string;
  description?: string;
  actionLabel?: string;
  href?: string;
}) {
  return (
    <section className="cta">
      <div className="shell cta-box">
          <div>
            <h2>{title}</h2>
            <p>{description}</p>
          </div>
          <Link href={href} className="btn primary large">{actionLabel} <ArrowUpRight size={17} aria-hidden="true" /></Link>
      </div>
    </section>
  );
}

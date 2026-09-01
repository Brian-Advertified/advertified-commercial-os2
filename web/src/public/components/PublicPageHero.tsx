import type { ReactNode } from 'react';

export function PublicPageHero({ title, introduction, actions }: {
  eyebrow: string;
  title: ReactNode;
  introduction: string;
  actions?: ReactNode;
}) {
  return (
    <section className="page-hero">
      <div className="shell">
        <h1>{title}</h1>
        <p>{introduction}</p>
        {actions}
      </div>
    </section>
  );
}

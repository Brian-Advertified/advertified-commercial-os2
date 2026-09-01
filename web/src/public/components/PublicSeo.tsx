import { useEffect } from 'react';
import type { PublicRouteMetadata } from '../publicRoutes';

export function PublicSeo({ metadata, notFound = false }: { metadata: PublicRouteMetadata; notFound?: boolean }) {
  useEffect(() => {
    document.title = metadata.title;

    upsertMeta('name', 'description', metadata.description);
    upsertMeta('property', 'og:title', metadata.title);
    upsertMeta('property', 'og:description', metadata.description);
    upsertMeta('property', 'og:type', 'website');
    upsertMeta('property', 'og:url', window.location.href);
    upsertMeta('name', 'twitter:card', 'summary');
    upsertMeta('name', 'twitter:title', metadata.title);
    upsertMeta('name', 'twitter:description', metadata.description);
    upsertMeta('name', 'robots', notFound ? 'noindex, nofollow' : 'index, follow');
    upsertCanonical(new URL(metadata.path || window.location.pathname, window.location.origin).href);

  }, [metadata, notFound]);

  return null;
}

function upsertMeta(attribute: 'name' | 'property', key: string, content: string) {
  let element = document.head.querySelector<HTMLMetaElement>(`meta[${attribute}="${key}"]`);
  if (!element) {
    element = document.createElement('meta');
    element.setAttribute(attribute, key);
    document.head.append(element);
  }
  element.content = content;
}

function upsertCanonical(href: string) {
  let element = document.head.querySelector<HTMLLinkElement>('link[rel="canonical"]');
  if (!element) {
    element = document.createElement('link');
    element.rel = 'canonical';
    document.head.append(element);
  }
  element.href = href;
}

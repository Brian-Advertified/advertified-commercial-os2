import { useState } from 'react';
import { publicInventoryAssetUrl } from '../api/publicInventory';

interface MediaOwnerLogoProps {
  name: string;
  logoUrl: string | null;
}

export function MediaOwnerLogo({ name, logoUrl }: MediaOwnerLogoProps) {
  const resolvedLogoUrl = publicInventoryAssetUrl(logoUrl);

  return <MediaOwnerLogoImage key={resolvedLogoUrl} name={name} url={resolvedLogoUrl} />;
}

function MediaOwnerLogoImage({ name, url }: { name: string; url: string | null }) {
  const [imageFailed, setImageFailed] = useState(false);

  return (
    <div className="media-brand-mark">
      {url && !imageFailed ? (
        <img
          src={url}
          alt={`${name} logo`}
          loading="lazy"
          decoding="async"
          onError={() => setImageFailed(true)}
        />
      ) : (
        <span className="media-brand-mark__wordmark" aria-label={`${name} logo`}>{name}</span>
      )}
      <strong>{name}</strong>
    </div>
  );
}

const tagline = 'Media Intelligence · Commercial Planning'

export function BrandMark() {
  return (
    <div className="brand" aria-label="Advertified">
      <img
        src="/advertified-wordmark.png"
        width="2000"
        height="220"
        alt="Advertified"
        className="brand-logo"
      />
      <span className="brand-tagline" aria-label={tagline}>
        <span className="brand-tagline-track" aria-hidden="true">{tagline}</span>
      </span>
    </div>
  )
}

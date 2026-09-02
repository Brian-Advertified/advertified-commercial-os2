import type { InventoryProduct } from '../api/inventory-schemas'

type AudienceProfile = NonNullable<InventoryProduct['audienceProfile']>

export function InventoryAudienceProfile({ profile }: { profile: AudienceProfile | null }) {
  if (!profile) return <section className="inventory-record-section" id="audience-profile">
    <p className="eyebrow">Audience evidence</p><h2>No measured audience profile supplied</h2>
    <p>Audience fit will remain insufficient evidence until a sourced profile is published.</p>
  </section>
  return <section className="inventory-record-section" id="audience-profile">
    <p className="eyebrow">Audience evidence</p><h2>Published audience profile</h2>
    <div className="inventory-audience-grid">
      <Segments label="Spoken languages" values={profile.spokenLanguages} />
      <Segments label="Understood languages" values={profile.understoodLanguages} />
      <Segments label="Life stages" values={profile.lifeStages} />
      <Segments label="LSM / SEM" values={profile.lsmSemSegments} />
      {profile.measurements.map(item => <article key={item.metricType}>
        <strong>{item.metricType.replaceAll('_', ' ')}</strong>
        <p>{item.value === null ? 'Insufficient evidence' : `${item.value} ${item.unit ?? ''}`.trim()}</p>
        <small>{item.universe ?? profile.universe ?? 'Universe not supplied'}</small>
      </article>)}
    </div>
    <dl>
      <Fact label="Universe" value={profile.universe} />
      <Fact label="Measurement source" value={profile.measurementSource} />
      <Fact label="Measurement period" value={profile.measurementPeriod} />
      <Fact label="Methodology" value={profile.methodology} />
      <Fact label="Limitations" value={profile.limitations} />
      <Fact label="Taxonomy" value={taxonomy(profile)} />
      <Fact label="Source locator" value={profile.sourceLocator} />
    </dl>
  </section>
}

function Segments({ label, values }: {
  label: string
  values: AudienceProfile['spokenLanguages']
}) {
  const value = values.length === 0 ? 'Insufficient evidence' : values
    .map(item => item.sharePercent === null ? item.label : `${item.label} ${item.sharePercent}%`)
    .join(' · ')
  return <article><strong>{label}</strong><p>{value}</p></article>
}

function Fact({ label, value }: { label: string; value: string | null }) {
  return <div className="product-fact"><dt>{label}</dt><dd>{value ?? 'Not supplied'}</dd></div>
}

function taxonomy(profile: AudienceProfile) {
  if (!profile.taxonomyName) return null
  return profile.taxonomyVersion
    ? `${profile.taxonomyName} ${profile.taxonomyVersion}`
    : profile.taxonomyName
}

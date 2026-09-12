import type { AudienceStrategy } from '../api/planning-schemas'

export type AudiencePlanningRole = 'primary' | 'secondary' | 'excluded'

export function AudienceDecisionComparison({ audience, roles }: {
  audience: AudienceStrategy
  roles: Record<string, AudiencePlanningRole>
}) {
  if (audience.definitions.length < 2) return null
  return <section className="audience-decision-comparison" aria-labelledby="audience-comparison-title">
    <header><div><p className="eyebrow">Audience comparison</p>
      <h3 id="audience-comparison-title">Compare the audience choices before committing media budget</h3>
      <p>The differences below come from the retained audience definitions. Evidence gaps stay visible instead of being hidden behind a single score.</p></div></header>
    <div className="audience-comparison-scroll"><table>
      <thead><tr><th scope="col">Decision factor</th>{audience.definitions.map(item =>
        <th scope="col" key={item.id}>{item.name}<span className={`is-${roles[item.id] ?? 'excluded'}`}>
          {roleLabel(roles[item.id] ?? 'excluded')}</span></th>)}</tr></thead>
      <tbody>
        <Row label="Need" values={audience.definitions.map(item => item.needState)} />
        <Row label="Buying context" values={audience.definitions.map(item => item.buyingContext)} />
        <Row label="Geography" values={audience.definitions.map(item =>
          item.geographies.join(', ') || 'Not established')} />
        <Row label="Life stage" values={audience.definitions.map(item => item.lifeStage || 'Not evidenced')} />
        <Row label="LSM / SEM" values={audience.definitions.map(item => item.lsmSem || 'Not evidenced')} />
        <Row label="Evidence" values={audience.definitions.map(item => item.evidenceItemIds.length > 0
          ? `${item.evidenceItemIds.length} retained item${item.evidenceItemIds.length === 1 ? '' : 's'}`
          : 'Hypothesis — no retained evidence')} />
        <Row label="Do not assume" values={audience.definitions.map(item =>
          item.exclusions.join('; ') || 'No explicit exclusion retained')} />
      </tbody>
    </table></div>
  </section>
}

function Row({ label, values }: { label: string; values: Array<string | null> }) {
  return <tr><th scope="row">{label}</th>{values.map((value, index) =>
    <td key={`${label}-${index}`}>{value ?? 'Not established / research required'}</td>)}</tr>
}

function roleLabel(role: AudiencePlanningRole) {
  if (role === 'primary') return 'Primary'
  if (role === 'secondary') return 'Secondary'
  return 'Excluded'
}

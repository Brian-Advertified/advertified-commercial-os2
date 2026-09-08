export function audienceSourceValues(values: FormData) {
  const fields = ['audienceName', 'language', 'lifeStage', 'lsmSem', 'lsmSemTaxonomy',
    'lsmSemTaxonomyVersion', 'needState', 'buyingContext', 'messageContext', 'momentContext']
  return Object.fromEntries(fields.map(key => [key, String(values.get(key) ?? '').trim() || null]))
}

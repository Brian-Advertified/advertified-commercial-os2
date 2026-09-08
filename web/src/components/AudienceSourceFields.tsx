export function AudienceSourceFields() {
  return <details><summary>Audience research details (optional)</summary>
    <p>Enter only details stated by this source. Match the audience name used in the Brief. A human must review this source before it can inform planning.</p>
    <label className="field-group">Audience name<input name="audienceName" maxLength={300} /></label>
    <label className="field-group">Supported language<input name="language" maxLength={100} /></label>
    <label className="field-group">Supported life stage<input name="lifeStage" maxLength={200} /></label>
    <label className="field-group">Segmentation group<input name="lsmSem" maxLength={100} /></label>
    <label className="field-group">Segmentation taxonomy<input name="lsmSemTaxonomy" maxLength={200} /></label>
    <label className="field-group">Taxonomy version<input name="lsmSemTaxonomyVersion" maxLength={100} /></label>
    <label className="field-group">Consumer need supported by source<textarea name="needState" maxLength={1000} /></label>
    <label className="field-group">Buying context supported by source<textarea name="buyingContext" maxLength={500} /></label>
    <label className="field-group">Message context supported by source<input name="messageContext" maxLength={200} /></label>
    <label className="field-group">Relevant moment supported by source<input name="momentContext" maxLength={200} /></label>
  </details>
}
